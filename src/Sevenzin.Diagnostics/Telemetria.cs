using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Uma amostra instantânea de telemetria (§21.2). Só métricas de sistema já acessíveis
/// ao usuário — sem HWID, sem screenshot, sem ler memória de outros processos.</summary>
public sealed record AmostraTelemetria(DateTime Utc, double CpuPct, double CpuClockPct, double RamCommittedPct, double RamDisponivelMB);

/// <summary>Resumo estatístico de uma métrica ao longo da sessão. FPS/consistência nunca por um número só.</summary>
public sealed record ResumoMetrica(string Nome, string Unidade, double Media, double Desvio, double P95, double P99, double Min, double Max, int N);

/// <summary>Resultado de uma sessão de diagnóstico: janela, nº de amostras, overhead medido da própria coleta e as métricas resumidas.</summary>
public sealed record ResumoTelemetria(DateTime Inicio, DateTime Fim, int Amostras, double OverheadPct, IReadOnlyList<ResumoMetrica> Metricas)
{
    public static ResumoTelemetria De(IReadOnlyList<AmostraTelemetria> a, DateTime inicio, DateTime fim, double overheadPct) => new(
        inicio, fim, a.Count, overheadPct, a.Count == 0 ? Array.Empty<ResumoMetrica>() : new[]
        {
            Estatisticas.Resumir("Uso de CPU", "%", a.Select(x => x.CpuPct)),
            Estatisticas.Resumir("Clock efetivo da CPU", "% do base", a.Select(x => x.CpuClockPct)),
            Estatisticas.Resumir("Memória comprometida", "%", a.Select(x => x.RamCommittedPct)),
            Estatisticas.Resumir("Memória disponível", "MB", a.Select(x => x.RamDisponivelMB)),
        });
}

internal static class Estatisticas
{
    public static ResumoMetrica Resumir(string nome, string unidade, IEnumerable<double> valores)
    {
        var v = valores.ToArray(); Array.Sort(v);
        double media = v.Average();
        double desvio = Math.Sqrt(v.Select(x => (x - media) * (x - media)).Average());
        return new(nome, unidade, media, desvio, Percentil(v, 95), Percentil(v, 99), v[0], v[^1], v.Length);
    }

    // Percentil por interpolação linear sobre o vetor já ordenado.
    public static double Percentil(double[] ordenado, double p)
    {
        if (ordenado.Length == 1) return ordenado[0];
        double rank = p / 100.0 * (ordenado.Length - 1);
        int lo = (int)Math.Floor(rank); double frac = rank - lo;
        return lo + 1 < ordenado.Length ? ordenado[lo] + frac * (ordenado[lo + 1] - ordenado[lo]) : ordenado[lo];
    }
}

/// <summary>Sessão de diagnóstico iniciada e encerrada pelo usuário (§21.2): amostra em segundo plano
/// num intervalo fixo e mede o próprio overhead. Não grava tela nem processos não relacionados.</summary>
public sealed class SessaoTelemetria
{
    readonly int _intervaloMs;
    readonly Func<AmostraTelemetria?> _amostrar;
    readonly List<AmostraTelemetria> _amostras = new();
    readonly Stopwatch _relogio = new();
    long _ticksAmostragem;
    DateTime _inicio;
    CancellationTokenSource? _cts;
    Task? _loop;

    /// <param name="amostrar">fonte das amostras; por padrão lê o WMI. Injetável para teste.</param>
    public SessaoTelemetria(int intervaloMs = 500, Func<AmostraTelemetria?>? amostrar = null)
    {
        _intervaloMs = Math.Max(50, intervaloMs); // evita polling agressivo (§21.2)
        _amostrar = amostrar ?? LeituraWmi.Amostrar;
    }

    public int Amostras { get { lock (_amostras) return _amostras.Count; } }
    public bool Ativa => _loop is { IsCompleted: false };

    public void Iniciar()
    {
        if (Ativa) return;
        lock (_amostras) _amostras.Clear();
        try { _amostrar(); } catch { } // aquece o provedor WMI de perf (1ª query é lenta); fora da janela e do overhead
        _ticksAmostragem = 0; _inicio = DateTime.UtcNow; _relogio.Restart();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                long t0 = _relogio.ElapsedTicks;
                AmostraTelemetria? a = null;
                try { a = _amostrar(); } catch { /* sensor falho não derruba a sessão */ }
                Interlocked.Add(ref _ticksAmostragem, _relogio.ElapsedTicks - t0);
                if (a is not null) lock (_amostras) _amostras.Add(a);
                try { await Task.Delay(_intervaloMs, ct); } catch (TaskCanceledException) { break; }
            }
        });
    }

    public ResumoTelemetria Parar()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _relogio.Stop();
        var fim = DateTime.UtcNow;
        double overhead = _relogio.ElapsedTicks > 0 ? 100.0 * _ticksAmostragem / _relogio.ElapsedTicks : 0;
        List<AmostraTelemetria> copia; lock (_amostras) copia = _amostras.ToList();
        return ResumoTelemetria.De(copia, _inicio, fim, overhead);
    }
}

public enum VereditoAB { Inconclusivo, DiferencaDetectada }

/// <summary>Uma métrica comparada entre linha de base e nova execução. Reporta o delta e se ele
/// está dentro do ruído medido — sem declarar "melhor/pior", que exige saber a direção desejada.</summary>
public sealed record ResultadoAB(string Metrica, string Unidade, double Base, double Novo, double Delta, double Ruido, VereditoAB Veredito);

/// <summary>Comparação A/B (§21.3): marca inconclusivo quando |Δ médias| ≤ ruído (dispersão média
/// das duas execuções). Só uma diferença acima do ruído conta como diferença observada.</summary>
public static class ComparacaoAB
{
    public static List<ResultadoAB> Comparar(ResumoTelemetria baseR, ResumoTelemetria novo)
    {
        var porNome = novo.Metricas.ToDictionary(m => m.Nome);
        var r = new List<ResultadoAB>();
        foreach (var b in baseR.Metricas)
        {
            if (!porNome.TryGetValue(b.Nome, out var n)) continue;
            double delta = n.Media - b.Media;
            double ruido = (b.Desvio + n.Desvio) / 2.0;
            var veredito = Math.Abs(delta) > ruido ? VereditoAB.DiferencaDetectada : VereditoAB.Inconclusivo;
            r.Add(new(b.Nome, b.Unidade, b.Media, n.Media, delta, ruido, veredito));
        }
        return r;
    }
}

/// <summary>Fonte de amostras via contadores de desempenho do Windows (WMI Win32_PerfFormattedData_*),
/// reusando o acesso só-leitura já existente. Valores cozidos; sem delta manual.</summary>
/// <summary>Leitura instantânea para painéis (ex.: dashboard Início). A 1ª chamada é lenta (warm-up do WMI).</summary>
public static class LeituraAtual
{
    public static AmostraTelemetria? Agora() => LeituraWmi.Amostrar();
}

internal static class LeituraWmi
{
    public static AmostraTelemetria? Amostrar()
    {
        double cpu = 0, clock = 0, commit = 0, avail = 0;
        try { foreach (var mo in Wmi.Query("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'")) cpu = D(mo, "PercentProcessorTime"); } catch { }
        try { foreach (var mo in Wmi.Query("SELECT PercentProcessorPerformance FROM Win32_PerfFormattedData_Counters_ProcessorInformation WHERE Name='_Total'")) clock = D(mo, "PercentProcessorPerformance"); } catch { }
        try { foreach (var mo in Wmi.Query("SELECT AvailableMBytes,PercentCommittedBytesInUse FROM Win32_PerfFormattedData_PerfOS_Memory")) { avail = D(mo, "AvailableMBytes"); commit = D(mo, "PercentCommittedBytesInUse"); } } catch { }
        return new AmostraTelemetria(DateTime.UtcNow, cpu, clock, commit, avail);
    }

    static double D(System.Management.ManagementObject mo, string p) { try { return mo[p] is null ? 0 : Convert.ToDouble(mo[p]); } catch { return 0; } }
}
