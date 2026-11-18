using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Nível de prioridade de um processo. <see cref="NivelPrioridade.TempoReal"/> é avançado e exige
/// confirmação explícita.</summary>
public enum NivelPrioridade { Ocioso, AbaixoNormal, Normal, AcimaNormal, Alto, TempoReal }

public sealed record ResultadoPrioridade(bool Ok, string Mensagem);

/// <summary>Gerenciamento de prioridade de processos (§5.4), incluindo Tempo Real com proteções.
/// A prioridade é TEMPORÁRIA (não persiste entre execuções do processo). Nunca aplica Tempo Real a processos
/// críticos do Windows. Cada alteração é registrada.</summary>
public static class PrioridadeProcesso
{
    /// <summary>Processos críticos que NUNCA podem receber Tempo Real (podem travar o sistema).</summary>
    static readonly string[] Criticos =
    {
        "csrss", "wininit", "winlogon", "lsass", "services", "smss", "svchost",
        "dwm", "audiodg", "fontdrvhost", "conhost", "spoolsv",
    };

    public static ProcessPriorityClass Mapear(NivelPrioridade p) => p switch
    {
        NivelPrioridade.Ocioso => ProcessPriorityClass.Idle,
        NivelPrioridade.AbaixoNormal => ProcessPriorityClass.BelowNormal,
        NivelPrioridade.Normal => ProcessPriorityClass.Normal,
        NivelPrioridade.AcimaNormal => ProcessPriorityClass.AboveNormal,
        NivelPrioridade.Alto => ProcessPriorityClass.High,
        NivelPrioridade.TempoReal => ProcessPriorityClass.RealTime,
        _ => ProcessPriorityClass.Normal,
    };

    public static NivelPrioridade? Demapear(ProcessPriorityClass p) => p switch
    {
        ProcessPriorityClass.Idle => NivelPrioridade.Ocioso,
        ProcessPriorityClass.BelowNormal => NivelPrioridade.AbaixoNormal,
        ProcessPriorityClass.Normal => NivelPrioridade.Normal,
        ProcessPriorityClass.AboveNormal => NivelPrioridade.AcimaNormal,
        ProcessPriorityClass.High => NivelPrioridade.Alto,
        ProcessPriorityClass.RealTime => NivelPrioridade.TempoReal,
        _ => null,
    };

    /// <summary>true se o nome do processo é crítico do sistema (protegido contra Tempo Real).</summary>
    public static bool EhCritico(string? nomeProcesso)
    {
        var n = (nomeProcesso ?? "").Trim().ToLowerInvariant();
        if (n.Length == 0) return true; // sem nome = conservador
        if (n is "system" or "idle") return true;
        // Remove sufixo ".exe" para comparar com o nome base (Process.ProcessName não traz .exe).
        var baseNome = n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
        return Criticos.Any(c => baseNome.Equals(c, StringComparison.OrdinalIgnoreCase) || baseNome.StartsWith(c, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Valida a mudança; retorna mensagem de erro ou null se permitida.
    /// Tempo Real em processo crítico é bloqueado.</summary>
    public static string? Validar(NivelPrioridade p, string? nomeProcesso)
    {
        if (p == NivelPrioridade.TempoReal && EhCritico(nomeProcesso))
            return "Tempo Real é bloqueado em processos críticos do Windows (pode travar o sistema).";
        return null;
    }

    /// <summary>Aplica a prioridade ao processo por PID (temporária).</summary>
    public static ResultadoPrioridade Aplicar(int pid, NivelPrioridade p)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            var erro = Validar(p, proc.ProcessName);
            if (erro is not null) return new(false, erro);
            proc.PriorityClass = Mapear(p);
            return new(true, $"Prioridade de '{proc.ProcessName}' ajustada para {p}.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Prioridade atual de um processo (leitura).</summary>
    public static NivelPrioridade? Ler(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return Demapear(proc.PriorityClass);
        }
        catch { return null; }
    }
}
