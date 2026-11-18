using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Métrica de interrupções (DPC/ISR) como fração do tempo de CPU — leitura por contadores de
/// performance. Não é a latência em microssegundos (isso exigiria ETW/driver); é a CARGA de DPC/ISR,
/// que indica quando um driver está monopolizando o processador.</summary>
public sealed record DpcMetrica(double DpcPct, double InterruptPct)
{
    public double TotalPct => Math.Round(DpcPct + InterruptPct, 2);
}

/// <summary>Diagnóstico de DPC/ISR (§5.2): mede o tempo de DPC e de interrupções (fração de CPU) por
/// contador de performance. A latência DPC real (µs) exige kernel tracing (LatencyMon/xperf) e não é
/// reproduzida aqui — a limitação é registrada. Não grava <c>DpcWatchdogPeriod=0</c> (mascarar instabilidade).</summary>
public static class DpcDiagnostico
{
    /// <summary>Lê % DPC Time e % Interrupt Time do total de processadores (síncrono, com breve intervalo).
    /// null quando o contador não existe.</summary>
    public static DpcMetrica? Ler()
    {
        try
        {
            using var dpc = new PerformanceCounter("Processor Information", "% DPC Time", "_Total");
            using var irq = new PerformanceCounter("Processor Information", "% Interrupt Time", "_Total");
            dpc.NextValue(); irq.NextValue();
            Thread.Sleep(120); // intervalo mínimo para o delta do contador estabilizar
            return new DpcMetrica(Math.Round(dpc.NextValue(), 2), Math.Round(irq.NextValue(), 2));
        }
        catch { return null; }
    }

    /// <summary>Leitura assíncrona com janela configurável (não bloqueia a thread da UI).</summary>
    public static async Task<DpcMetrica?> LerAsync(int janelaMs = 250, CancellationToken ct = default)
    {
        try
        {
            using var dpc = new PerformanceCounter("Processor Information", "% DPC Time", "_Total");
            using var irq = new PerformanceCounter("Processor Information", "% Interrupt Time", "_Total");
            dpc.NextValue(); irq.NextValue();
            await Task.Delay(Math.Clamp(janelaMs, 50, 2000), ct);
            return new DpcMetrica(Math.Round(dpc.NextValue(), 2), Math.Round(irq.NextValue(), 2));
        }
        catch { return null; }
    }

    /// <summary>Interpretação para a UI: carga baixa/média/alta de DPC+ISR.</summary>
    public static string Classificar(DpcMetrica m) => m.TotalPct switch
    {
        < 5 => "baixa (normal)",
        < 15 => "moderada — observe drivers em uso intenso",
        _ => "alta — algum driver pode estar monopolizando o processador",
    };
}
