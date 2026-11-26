using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Um ponto de restauração existente (como o Windows o lista).</summary>
public sealed record PontoRestauracaoInfo(uint Sequencia, string Descricao, DateTime CriadoUtc);

/// <summary>O que foi lido antes de criar. Null = não deu para ler.</summary>
public sealed record EstadoProtecao(
    bool Admin,
    bool? ProtecaoAtiva,
    bool BloqueadoPorPolitica,
    bool VssDesativado,
    long? LivreBytes,
    PontoRestauracaoInfo? Ultimo,
    int FrequenciaMinutos);

public enum MotivoPonto { Nenhum, SemAdmin, ProtecaoDesligada, BloqueadoPorPolitica, VssDesativado, PoucoEspaco, LimiteDeFrequencia, ErroDoWindows, NaoConfirmado }

/// <summary>Resultado de uma tentativa. <paramref name="Criado"/> só é verdadeiro quando o ponto novo aparece na
/// lista do Windows (nunca pelo código de retorno sozinho).</summary>
public sealed record ResultadoPonto(bool Criado, MotivoPonto Motivo, string Mensagem, PontoRestauracaoInfo? Ponto);

/// <summary>Ponto de restauração do Windows antes de otimizar. Verifica antes (admin, Proteção do Sistema, política,
/// serviço VSS, espaço, limite de frequência), cria via WMI (SystemRestore.CreateRestorePoint) e confirma
/// relendo a lista. Cobre configurações do sistema, registro e drivers — NÃO cobre arquivos pessoais.</summary>
public static class PontoRestauracao
{
    public const long EspacoMinimoBytes = 300L * 1024 * 1024; // abaixo disso o Windows não cria ponto
    const int FrequenciaPadraoMinutos = 1440;                 // padrão do Windows: um ponto a cada 24 h
    const string ChaveSr = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    const string ChavePolitica = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";

    /// <summary>Decide se dá para tentar criar agora e, se não, por quê. Função pura.</summary>
    public static MotivoPonto Avaliar(EstadoProtecao e, DateTime agoraUtc)
    {
        if (!e.Admin) return MotivoPonto.SemAdmin;
        if (e.BloqueadoPorPolitica) return MotivoPonto.BloqueadoPorPolitica;
        if (e.ProtecaoAtiva == false) return MotivoPonto.ProtecaoDesligada;
        if (e.VssDesativado) return MotivoPonto.VssDesativado;
        if (e.LivreBytes is { } l && l < EspacoMinimoBytes) return MotivoPonto.PoucoEspaco;
        // Dentro do intervalo o Windows aceita a chamada mas NÃO cria o ponto: melhor avisar antes.
        if (e.FrequenciaMinutos > 0 && e.Ultimo is { } u && agoraUtc - u.CriadoUtc < TimeSpan.FromMinutes(e.FrequenciaMinutos))
            return MotivoPonto.LimiteDeFrequencia;
        return MotivoPonto.Nenhum;
    }

    /// <summary>O ponto que surgiu entre as duas leituras: sequência maior que todas as anteriores e criado depois
    /// do início da tentativa (com 2 min de folga de relógio). Null = não foi criado. Função pura.</summary>
    public static PontoRestauracaoInfo? Confirmar(IReadOnlyList<PontoRestauracaoInfo> antes, IReadOnlyList<PontoRestauracaoInfo> depois, DateTime inicioUtc)
    {
        uint maiorAntes = antes.Count == 0 ? 0 : antes.Max(p => p.Sequencia);
        return depois.Where(p => p.Sequencia > maiorAntes && p.CriadoUtc >= inicioUtc.AddMinutes(-2))
                     .OrderByDescending(p => p.Sequencia).FirstOrDefault();
    }

    /// <summary>Texto para o usuário, com o que fazer. Função pura.</summary>
    public static string Explicar(MotivoPonto m, EstadoProtecao? e = null) => m switch
    {
        MotivoPonto.Nenhum => "Pronto para criar o ponto de restauração.",
        MotivoPonto.SemAdmin => "É preciso abrir o SevenzinTwks como administrador para criar pontos de restauração.",
        MotivoPonto.BloqueadoPorPolitica => "Uma política do Windows (Diretiva de Grupo) desativou a Restauração do Sistema neste PC.",
        MotivoPonto.ProtecaoDesligada => "A Proteção do Sistema está desligada no disco do Windows. Ative em: Configurações > Sistema > Sobre > Proteção do sistema > Configurar.",
        MotivoPonto.VssDesativado => "O serviço Cópias de Sombra de Volume (VSS) está desativado; sem ele o Windows não cria pontos de restauração.",
        MotivoPonto.PoucoEspaco => $"Há pouco espaço livre no disco do Windows (menos de {EspacoMinimoBytes / (1024 * 1024)} MB).",
        MotivoPonto.LimiteDeFrequencia => e?.Ultimo is { } u
            ? $"O Windows só cria um ponto a cada {Horas(e.FrequenciaMinutos)}, e o último foi em {u.CriadoUtc.ToLocalTime():dd/MM 'às' HH:mm} (\"{u.Descricao}\"). Esse ponto ainda pode ser usado para voltar atrás."
            : "O Windows limita a criação de pontos de restauração por intervalo de tempo.",
        MotivoPonto.NaoConfirmado => "O Windows aceitou o pedido, mas nenhum ponto novo apareceu na lista. Considere que NÃO há ponto de restauração.",
        _ => "O Windows recusou a criação do ponto de restauração.",
    };

    static string Horas(int minutos) => minutos % 60 == 0 ? $"{minutos / 60} h" : $"{minutos} min";

    // ── Leitura real do sistema ──

    public static EstadoProtecao LerEstado()
    {
        bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        bool? ativa = null; bool politica = false, vssOff = false; int freq = FrequenciaPadraoMinutos;
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(ChaveSr);
            // RPSessionInterval = 1 quando a proteção está ligada em algum volume (0 = desligada).
            if (k?.GetValue("RPSessionInterval") is int s) ativa = s != 0;
            if (k?.GetValue("DisableSR") is int d && d == 1) ativa = false;
            if (k?.GetValue("SystemRestorePointCreationFrequency") is int f) freq = f;
        }
        catch { }
        try
        {
            using var p = Registry.LocalMachine.OpenSubKey(ChavePolitica);
            politica = p?.GetValue("DisableSR") is int ds && ds == 1 || p?.GetValue("DisableConfig") is int dc && dc == 1;
        }
        catch { }
        try
        {
            using var v = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\VSS");
            vssOff = v?.GetValue("Start") is int st && st == 4;
        }
        catch { }
        long? livre = null;
        try { livre = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace; } catch { }
        var ultimo = Listar().OrderByDescending(p => p.Sequencia).FirstOrDefault();
        return new(admin, ativa, politica, vssOff, livre, ultimo, freq);
    }

    public static List<PontoRestauracaoInfo> Listar()
    {
        var r = new List<PontoRestauracaoInfo>();
        try
        {
            using var s = new ManagementObjectSearcher(@"root\default", "SELECT SequenceNumber, Description, CreationTime FROM SystemRestore");
            foreach (ManagementObject o in s.Get())
            {
                using (o)
                {
                    var criado = ManagementDateTimeConverter.ToDateTime((string)o["CreationTime"]).ToUniversalTime();
                    r.Add(new((uint)o["SequenceNumber"], (string?)o["Description"] ?? "", criado));
                }
            }
        }
        catch { }
        return r;
    }

    /// <summary>Cria e confirma. Demora de 30 s a alguns minutos: chame fora da UI. Nunca lança.</summary>
    public static ResultadoPonto Criar(string descricao)
    {
        var estado = LerEstado();
        var motivo = Avaliar(estado, DateTime.UtcNow);
        if (motivo != MotivoPonto.Nenhum) return new(false, motivo, Explicar(motivo, estado), null);

        var antes = Listar();
        var inicio = DateTime.UtcNow;
        uint codigo;
        try
        {
            using var classe = new ManagementClass(@"\\.\root\default", "SystemRestore", null);
            // 12 = MODIFY_SETTINGS (alteração de configurações), 100 = BEGIN_SYSTEM_CHANGE.
            codigo = Convert.ToUInt32(classe.InvokeMethod("CreateRestorePoint", new object[] { descricao, 12u, 100u }));
        }
        catch (Exception ex)
        {
            return new(false, MotivoPonto.ErroDoWindows, $"{Explicar(MotivoPonto.ErroDoWindows)} Detalhe: {ex.Message}", null);
        }
        if (codigo != 0)
            return new(false, MotivoPonto.ErroDoWindows, $"{Explicar(MotivoPonto.ErroDoWindows)} Código 0x{codigo:X8}.", null);

        var ponto = Confirmar(antes, Listar(), inicio);
        return ponto is null
            ? new(false, MotivoPonto.NaoConfirmado, Explicar(MotivoPonto.NaoConfirmado), null)
            : new(true, MotivoPonto.Nenhum, $"Ponto de restauração nº {ponto.Sequencia} criado em {ponto.CriadoUtc.ToLocalTime():dd/MM 'às' HH:mm} (\"{ponto.Descricao}\").", ponto);
    }
}
