using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Resumo de frametime de um benchmark (§21.2/21.3). FPS sempre acompanhado de consistência:
/// percentis de frametime, 1% low e contagem de stutter — nunca um FPS médio sozinho.</summary>
public sealed record ResumoFrametime(
    int Quadros, double FpsMedio, double FrametimeMedioMs, double P95Ms, double P99Ms,
    double Fps1PorCentoLow, double DesvioMs, int Stutters, double StutterLimiarMs);

/// <summary>Benchmark de frametime interno e reproduzível: mede o tempo de cada quadro de um
/// trabalho fixo fornecido por quem chama. Mede a renderização do próprio app — não a de um jogo
/// externo (isso exigiria ETW/PresentMon com privilégio de administrador).</summary>
public static class BenchmarkFrametime
{
    /// <summary>Executa <paramref name="quadros"/> iterações de <paramref name="umQuadro"/>, cronometrando cada uma.</summary>
    public static ResumoFrametime Rodar(int quadros, Action umQuadro, CancellationToken ct = default)
    {
        quadros = Math.Clamp(quadros, 1, 200_000);
        var ms = new List<double>(quadros);
        var sw = Stopwatch.StartNew();
        try { umQuadro(); } catch { } // aquecimento: 1 quadro fora da medição (JIT/cache)
        for (int i = 0; i < quadros && !ct.IsCancellationRequested; i++)
        {
            long t0 = sw.ElapsedTicks;
            umQuadro();
            ms.Add((sw.ElapsedTicks - t0) * 1000.0 / Stopwatch.Frequency);
        }
        return Resumir(ms);
    }

    /// <summary>Resume uma lista de frametimes (ms). Puro e determinístico.</summary>
    public static ResumoFrametime Resumir(IReadOnlyList<double> frametimesMs)
    {
        if (frametimesMs.Count == 0) return new(0, 0, 0, 0, 0, 0, 0, 0, 0);
        var v = frametimesMs.ToArray(); Array.Sort(v);
        double media = v.Average();
        double mediana = Estatisticas.Percentil(v, 50);
        double p95 = Estatisticas.Percentil(v, 95);
        double p99 = Estatisticas.Percentil(v, 99);
        double desvio = Math.Sqrt(v.Select(x => (x - media) * (x - media)).Average());
        double limiar = 2 * mediana;                                  // stutter: quadro acima de 2× a mediana
        int stutters = frametimesMs.Count(x => x > limiar);
        return new(v.Length,
            media > 0 ? 1000.0 / media : 0,                           // FPS médio = 1000 / frametime médio
            media, p95, p99,
            p99 > 0 ? 1000.0 / p99 : 0,                               // 1% low ≈ FPS no p99 de frametime
            desvio, stutters, limiar);
    }
}
