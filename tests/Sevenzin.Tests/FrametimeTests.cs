using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class FrametimeTests
{
    [Fact]
    public void Resumir_calcula_fps_percentis_e_conta_stutter()
    {
        // 4 quadros bons de 10 ms + 1 pico de 100 ms → mediana 10, limiar 20, 1 stutter.
        var r = BenchmarkFrametime.Resumir(new double[] { 10, 10, 10, 10, 100 });
        Assert.Equal(5, r.Quadros);
        Assert.Equal(28.0, r.FrametimeMedioMs, 3);          // média (10+10+10+10+100)/5
        Assert.Equal(1000.0 / 28.0, r.FpsMedio, 2);
        Assert.Equal(20.0, r.StutterLimiarMs, 3);           // 2× mediana
        Assert.Equal(1, r.Stutters);                        // só o quadro de 100 ms
        Assert.True(r.P99Ms > r.P95Ms);                     // percentis crescentes
        Assert.True(r.Fps1PorCentoLow < r.FpsMedio);        // 1% low pior que a média
    }

    [Fact]
    public void Resumir_sem_quadros_nao_quebra()
    {
        var r = BenchmarkFrametime.Resumir(Array.Empty<double>());
        Assert.Equal(0, r.Quadros);
        Assert.Equal(0, r.FpsMedio);
    }

    [Fact]
    public void Rodar_mede_a_quantidade_de_quadros_pedida()
    {
        int chamadas = 0;
        var r = BenchmarkFrametime.Rodar(50, () => Interlocked.Increment(ref chamadas));
        Assert.Equal(50, r.Quadros);
        Assert.Equal(51, chamadas);          // 50 medidos + 1 de aquecimento
        Assert.True(r.FrametimeMedioMs >= 0);
    }

    [Fact]
    public void Rodar_respeita_cancelamento()
    {
        using var cts = new CancellationTokenSource();
        int chamadas = 0;
        var r = BenchmarkFrametime.Rodar(100000, () => { if (Interlocked.Increment(ref chamadas) >= 5) cts.Cancel(); }, cts.Token);
        Assert.True(r.Quadros < 100000);     // parou cedo
    }
}
