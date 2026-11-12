using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sevenzin.Diagnostics;

/// <summary>Prioridade de processo oferecida a jogos. Deliberadamente NÃO inclui "Realtime" (§21.4):
/// Realtime pode travar áudio, captura e o próprio SO.</summary>
public enum PrioridadeJogo { Normal, AcimaDoNormal, Alta }

/// <summary>Perfil reversível por jogo: afinidade de CPU (máscara de bits; 0 = todos os núcleos), prioridade e
/// três otimizações OPT-IN com aviso (§5.3 do roadmap): <paramref name="TrimWorkingSet"/> libera as páginas
/// residentes ao aplicar (ação pontual, sem estado a reverter), <paramref name="TimerAltaResolucao"/> sobe a
/// resolução do timer do sistema (1 ms) enquanto o jogo estiver aplicado — revertida ao fechar —,
/// <paramref name="PrefereGpuAlta"/> marca o .exe como "alto desempenho" no UserGpuPreferences (HKCU, removido
/// ao reverter) e <paramref name="PriorityBoost"/> ativa o boost de prioridade do processo enquanto aplicado.</summary>
public sealed record PerfilJogo(string Nome, string Executavel, long MascaraAfinidade, PrioridadeJogo Prioridade,
    bool TrimWorkingSet = false, bool TimerAltaResolucao = false, bool PrefereGpuAlta = false, bool PriorityBoost = false);

/// <summary>Estado de afinidade/prioridade de um processo, guardado para restaurar depois.</summary>
public sealed record EstadoProcesso(long Afinidade, ProcessPriorityClass Prioridade, bool Boost);

/// <summary>Aplica e reverte um perfil a um processo em execução. O estado anterior é devolvido por
/// <see cref="Aplicar"/> para que <see cref="Reverter"/> restaure exatamente (§21.4: temporário e reversível).</summary>
public static class AplicadorPerfil
{
    public static long MascaraTodosNucleos =>
        Environment.ProcessorCount >= 64 ? -1L : (1L << Environment.ProcessorCount) - 1;

    public static ProcessPriorityClass Mapear(PrioridadeJogo prio) => prio switch
    {
        PrioridadeJogo.Alta => ProcessPriorityClass.High,
        PrioridadeJogo.AcimaDoNormal => ProcessPriorityClass.AboveNormal,
        _ => ProcessPriorityClass.Normal, // nunca Realtime
    };

    public static EstadoProcesso Aplicar(Process p, long mascaraAfinidade, PrioridadeJogo prio, bool boost = false)
    {
        var anterior = new EstadoProcesso((long)p.ProcessorAffinity, p.PriorityClass, p.PriorityBoostEnabled);
        long mascara = mascaraAfinidade == 0 ? MascaraTodosNucleos : (mascaraAfinidade & MascaraTodosNucleos);
        if (mascara == 0) mascara = MascaraTodosNucleos; // nunca deixar o processo sem nenhum núcleo
        p.ProcessorAffinity = (IntPtr)mascara;
        p.PriorityClass = Mapear(prio);
        if (boost) p.PriorityBoostEnabled = true;
        return anterior;
    }

    public static void Reverter(Process p, EstadoProcesso anterior)
    {
        p.ProcessorAffinity = (IntPtr)anterior.Afinidade;
        p.PriorityClass = anterior.Prioridade;
        p.PriorityBoostEnabled = anterior.Boost;
    }

    /// <summary>Trim do working set (SetProcessWorkingSetSize -1/-1): libera as páginas residentes do processo.
    /// Ação pontual e reversível por natureza — a memória volta a crescer conforme o jogo trabalha. Opt-in.</summary>
    public static bool TrimWorkingSet(Process p) =>
        SetProcessWorkingSetSize(p.Handle, unchecked((IntPtr)(-1)), unchecked((IntPtr)(-1)));

    [DllImport("kernel32.dll")]
    static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr minimo, IntPtr maximo);
}

/// <summary>Catálogo de perfis por jogo, persistido localmente (JSON). Sem rede, sem identificadores.</summary>
public sealed class CatalogoJogos
{
    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly string _arquivo;
    readonly List<PerfilJogo> _jogos;

    public CatalogoJogos(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "jogos.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _jogos = Carregar();
    }

    public IReadOnlyList<PerfilJogo> Todos => _jogos;

    public void Adicionar(PerfilJogo p)
    {
        _jogos.RemoveAll(x => x.Executavel.Equals(p.Executavel, StringComparison.OrdinalIgnoreCase)); // substitui por executável
        _jogos.Add(p);
        Salvar();
    }

    public bool Remover(string executavel)
    {
        bool algum = _jogos.RemoveAll(x => x.Executavel.Equals(executavel, StringComparison.OrdinalIgnoreCase)) > 0;
        if (algum) Salvar();
        return algum;
    }

    List<PerfilJogo> Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<PerfilJogo>>(File.ReadAllText(_arquivo), Opts) ?? new() : new(); }
        catch { return new(); }
    }
    void Salvar() => File.WriteAllText(_arquivo, JsonSerializer.Serialize(_jogos, Opts));
}

/// <summary>Controle dos processos de jogo (quais estão rodando, aplicar/reverter). Interface para testar o
/// gestor sem processos reais; a implementação de produção usa <see cref="AplicadorPerfil"/>.</summary>
public interface IControleProcesso
{
    IReadOnlyCollection<string> ExesRodando(IEnumerable<string> alvos);
    bool Aplicar(PerfilJogo jogo);   // acha o processo e aplica; false se não está rodando / falhou
    bool Reverter(string executavel); // restaura o estado anterior; false se não havia estado guardado
}

/// <summary>Implementação real: localiza o processo pelo nome do executável e aplica/reverte o perfil,
/// guardando o estado anterior por jogo. Trim de working set é aplicado uma vez (opt-in) e o timer de alta
/// resolução sobe enquanto houver jogo usando e baixa quando o último for revertido. A preferência de GPU
/// (HKCU UserGpuPreferences) é gravada ao aplicar e REMOVIDA ao reverter.</summary>
public sealed class ControleProcessoWindows : IControleProcesso
{
    readonly Dictionary<string, (Process proc, EstadoProcesso estado)> _estados = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _usandoTimer = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _gpuPref = new(StringComparer.OrdinalIgnoreCase);
    readonly TimerAltaResolucao _timer;
    readonly IRegistro _registro;

    public ControleProcessoWindows(TimerAltaResolucao? timer = null, IRegistro? registro = null)
    {
        _timer = timer ?? new TimerAltaResolucao();
        _registro = registro ?? new RegistroUsuario();
    }

    static string Nome(string exe) => Path.GetFileNameWithoutExtension(exe);

    public IReadOnlyCollection<string> ExesRodando(IEnumerable<string> alvos)
    {
        var vivos = new List<string>();
        foreach (var exe in alvos)
            if (Process.GetProcessesByName(Nome(exe)).Length > 0) vivos.Add(exe);
        return vivos;
    }

    public bool Aplicar(PerfilJogo jogo)
    {
        var procs = Process.GetProcessesByName(Nome(jogo.Executavel));
        if (procs.Length == 0) return false;
        try
        {
            var estado = AplicadorPerfil.Aplicar(procs[0], jogo.MascaraAfinidade, jogo.Prioridade, jogo.PriorityBoost);
            if (jogo.TrimWorkingSet) { try { AplicadorPerfil.TrimWorkingSet(procs[0]); } catch { } }
            _estados[jogo.Executavel] = (procs[0], estado);
            if (jogo.TimerAltaResolucao) { _usandoTimer.Add(jogo.Executavel); _timer.Iniciar(); }
            if (jogo.PrefereGpuAlta)
            {
                _registro.EscreverString(Colmeia.UsuarioAtual, @"Software\Microsoft\DirectX\UserGpuPreferences",
                    Path.GetFileName(jogo.Executavel), "GpuPreference=2;");
                _gpuPref.Add(jogo.Executavel);
            }
            return true;
        }
        catch { return false; } // acesso negado / anti-cheat / exige admin
    }

    public bool Reverter(string executavel)
    {
        if (!_estados.TryGetValue(executavel, out var e)) return false;
        try { if (!e.proc.HasExited) AplicadorPerfil.Reverter(e.proc, e.estado); } catch { }
        _estados.Remove(executavel);
        if (_usandoTimer.Remove(executavel)) _timer.Parar();
        if (_gpuPref.Remove(executavel))
        {
            try { _registro.ApagarValor(Colmeia.UsuarioAtual, @"Software\Microsoft\DirectX\UserGpuPreferences", Path.GetFileName(executavel)); }
            catch { }
        }
        return true;
    }
}

/// <summary>Orquestra os perfis: aplicar/reverter manual e <see cref="Sincronizar"/> (monitor), que aplica
/// os jogos que abriram e reverte os que fecharam. Mantém o conjunto de aplicados.</summary>
public sealed class GestorJogos
{
    readonly CatalogoJogos _catalogo;
    readonly IControleProcesso _controle;
    readonly HashSet<string> _aplicados = new(StringComparer.OrdinalIgnoreCase);

    public GestorJogos(CatalogoJogos catalogo, IControleProcesso controle)
    {
        _catalogo = catalogo;
        _controle = controle;
    }

    public bool EstaAplicado(string executavel) => _aplicados.Contains(executavel);

    public bool AplicarAgora(PerfilJogo jogo)
    {
        if (!_controle.Aplicar(jogo)) return false;
        _aplicados.Add(jogo.Executavel);
        return true;
    }

    public bool Reverter(PerfilJogo jogo)
    {
        if (!_aplicados.Remove(jogo.Executavel)) return false;
        _controle.Reverter(jogo.Executavel);
        return true;
    }

    /// <summary>Tick do monitor: reverte os perfis cujo jogo fechou e aplica os que abriram.</summary>
    public void Sincronizar()
    {
        var alvos = _catalogo.Todos.Select(j => j.Executavel).ToList();
        var rodando = new HashSet<string>(_controle.ExesRodando(alvos), StringComparer.OrdinalIgnoreCase);

        foreach (var exe in _aplicados.ToList())
            if (!rodando.Contains(exe)) { _controle.Reverter(exe); _aplicados.Remove(exe); }

        foreach (var jogo in _catalogo.Todos)
            if (rodando.Contains(jogo.Executavel) && !_aplicados.Contains(jogo.Executavel) && _controle.Aplicar(jogo))
                _aplicados.Add(jogo.Executavel);
    }
}
