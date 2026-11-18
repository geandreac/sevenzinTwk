using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Registro persistido quando o aplicativo sofre uma falha inesperada ou fechamento abrupto.</summary>
public sealed record RegistroRecuperacaoFalha(
    string Motivo,
    string Detalhe,
    string? EmailUsuario,
    string? ContextoOperacao,
    DateTime OcorreuEmUtc);

/// <summary>Sentinela de sessão ativa para detectar encerramentos abruptos mesmo quando o processo morre sem finalizador gerenciado.</summary>
public sealed record SentinelaSessaoAtiva(
    int Pid,
    string? EmailUsuario,
    string? UltimaOperacao,
    DateTime IniciadoEmUtc);

/// <summary>
/// Gerencia a detecção de fechamento inesperado e a recuperação rápida de sessão, registrando o motivo exato
/// para exibição automática em Notificações na reabertura.
/// </summary>
public sealed class RecuperacaoFalhaStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    static readonly Lazy<RecuperacaoFalhaStore> PadraoLazy = new(() => new RecuperacaoFalhaStore());

    public static RecuperacaoFalhaStore Padrao => PadraoLazy.Value;

    readonly object _lock = new();
    readonly string _arqCrash;
    readonly string _arqSentinela;

    public RecuperacaoFalhaStore(string? pastaEstado = null)
    {
        var pasta = pastaEstado ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");
        Directory.CreateDirectory(pasta);
        _arqCrash = Path.Combine(pasta, "crash-recovery.json");
        _arqSentinela = Path.Combine(pasta, "sessao-ativa.json");
    }

    /// <summary>Registra o início ou atualização da operação atual na sessão ativa.</summary>
    public void MarcarSessaoAtiva(string? emailUsuario, string? operacaoAtual = null, int? pid = null)
    {
        lock (_lock)
        {
            try
            {
                var s = new SentinelaSessaoAtiva(pid ?? Environment.ProcessId, emailUsuario, operacaoAtual, DateTime.UtcNow);
                var tmp = _arqSentinela + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(s, Opts));
                File.Move(tmp, _arqSentinela, overwrite: true);
            }
            catch { }
        }
    }

    /// <summary>Atualiza apenas o contexto da operação em curso (ex.: ajuste avançado sendo aplicado).</summary>
    public void DefinirOperacaoEmCurso(string? operacao)
    {
        lock (_lock)
        {
            try
            {
                SentinelaSessaoAtiva? atual = null;
                if (File.Exists(_arqSentinela))
                    atual = JsonSerializer.Deserialize<SentinelaSessaoAtiva>(File.ReadAllText(_arqSentinela), Opts);
                var novo = new SentinelaSessaoAtiva(
                    atual?.Pid ?? Environment.ProcessId,
                    atual?.EmailUsuario,
                    operacao,
                    atual?.IniciadoEmUtc ?? DateTime.UtcNow);
                var tmp = _arqSentinela + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(novo, Opts));
                File.Move(tmp, _arqSentinela, overwrite: true);
            }
            catch { }
        }
    }

    /// <summary>Marca encerramento intencional/limpo pelo usuário, removendo a sentinela de sessão ativa.</summary>
    public void MarcarEncerramentoNormal()
    {
        lock (_lock)
        {
            try { if (File.Exists(_arqSentinela)) File.Delete(_arqSentinela); }
            catch { }
        }
    }

    /// <summary>Grava o motivo de uma falha crítica imediatamente antes da reabertura automática.</summary>
    public void RegistrarFalha(string motivo, string detalhe, string? emailUsuario = null)
    {
        lock (_lock)
        {
            try
            {
                string? op = null;
                string? emailSentinela = null;
                if (File.Exists(_arqSentinela))
                {
                    var s = JsonSerializer.Deserialize<SentinelaSessaoAtiva>(File.ReadAllText(_arqSentinela), Opts);
                    op = s?.UltimaOperacao;
                    emailSentinela = s?.EmailUsuario;
                }
                var reg = new RegistroRecuperacaoFalha(
                    string.IsNullOrWhiteSpace(motivo) ? "Erro inesperado em tempo de execução" : motivo.Trim(),
                    detalhe ?? "",
                    emailUsuario ?? emailSentinela,
                    op,
                    DateTime.UtcNow);
                var tmp = _arqCrash + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(reg, Opts));
                File.Move(tmp, _arqCrash, overwrite: true);
                if (File.Exists(_arqSentinela)) File.Delete(_arqSentinela);
            }
            catch { }
        }
    }

    /// <summary>
    /// Consome e apaga qualquer registro de falha pendente (explícita em `crash-recovery.json` ou abrupta via `sessao-ativa.json`
    /// de um processo que já não existe mais). Retorna null quando a última execução encerrou normalmente.
    /// </summary>
    public RegistroRecuperacaoFalha? ConsumirPendente(Func<int, bool>? processoAindaVivo = null)
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_arqCrash))
                {
                    var txt = File.ReadAllText(_arqCrash);
                    File.Delete(_arqCrash);
                    if (File.Exists(_arqSentinela)) File.Delete(_arqSentinela);
                    return JsonSerializer.Deserialize<RegistroRecuperacaoFalha>(txt, Opts);
                }

                if (File.Exists(_arqSentinela))
                {
                    var s = JsonSerializer.Deserialize<SentinelaSessaoAtiva>(File.ReadAllText(_arqSentinela), Opts);
                    if (s is not null && s.Pid != Environment.ProcessId)
                    {
                        bool vivo = processoAindaVivo?.Invoke(s.Pid) ?? ProcessoExiste(s.Pid);
                        if (!vivo)
                        {
                            File.Delete(_arqSentinela);
                            var motivo = !string.IsNullOrWhiteSpace(s.UltimaOperacao)
                                ? $"Encerramento abrupto durante: {s.UltimaOperacao}"
                                : "O processo anterior foi encerrado inesperadamente pelo sistema";
                            return new RegistroRecuperacaoFalha(
                                motivo,
                                "A sessão anterior terminou sem passar pelo fluxo normal de saída.",
                                s.EmailUsuario,
                                s.UltimaOperacao,
                                DateTime.UtcNow);
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    static bool ProcessoExiste(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited && p.ProcessName.Contains("Sevenzin", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
