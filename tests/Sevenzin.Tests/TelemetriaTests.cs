using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class TelemetriaTests
{
    static ResumoTelemetria ResumoDe(params double[] cpu)
    {
        var a = cpu.Select((v, i) => new AmostraTelemetria(DateTime.UnixEpoch.AddSeconds(i), v, 100, 50, 8000)).ToList();
        return ResumoTelemetria.De(a, DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(a.Count), 1.0);
    }

    [Fact]
    public void Resumo_calcula_media_percentis_e_faixa()
    {
        var r = ResumoDe(0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100);
        var cpu = r.Metricas.Single(m => m.Nome == "Uso de CPU");
        Assert.Equal(11, cpu.N);
        Assert.Equal(50, cpu.Media, 3);
        Assert.Equal(0, cpu.Min, 3);
        Assert.Equal(100, cpu.Max, 3);
        Assert.Equal(99, cpu.P99, 0);        // interpolação linear: 0,99·(n−1)
        Assert.True(cpu.P95 is >= 90 and <= 100);
    }

    [Fact]
    public void AB_marca_inconclusivo_quando_delta_menor_que_ruido()
    {
        // base e novo quase iguais, mas com dispersão alta: a diferença some no ruído.
        var baseR = ResumoDe(40, 60, 40, 60);   // média 50, desvio 10
        var novo = ResumoDe(48, 52, 48, 52);    // média 50, desvio 2
        var cpu = ComparacaoAB.Comparar(baseR, novo).Single(x => x.Metrica == "Uso de CPU");
        Assert.Equal(VereditoAB.Inconclusivo, cpu.Veredito);
    }

    [Fact]
    public void AB_detecta_diferenca_quando_delta_maior_que_ruido()
    {
        var baseR = ResumoDe(49, 50, 51, 50);   // média 50, desvio ~0,7
        var novo = ResumoDe(69, 70, 71, 70);    // média 70, desvio ~0,7 → delta 20 >> ruído
        var cpu = ComparacaoAB.Comparar(baseR, novo).Single(x => x.Metrica == "Uso de CPU");
        Assert.Equal(VereditoAB.DiferencaDetectada, cpu.Veredito);
        Assert.Equal(20, cpu.Delta, 1);
    }

    [Fact]
    public void Sessao_coleta_serie_temporal_e_mede_overhead_com_sampler_injetado()
    {
        int n = 0;
        var sessao = new SessaoTelemetria(50, () => new AmostraTelemetria(DateTime.UtcNow, Interlocked.Increment(ref n), 100, 50, 8000));
        sessao.Iniciar();
        var limite = DateTime.UtcNow.AddSeconds(2);
        while (sessao.Amostras < 2 && DateTime.UtcNow < limite) Thread.Sleep(20);
        var r = sessao.Parar();

        Assert.True(r.Amostras >= 2, $"esperava >=2 amostras, obteve {r.Amostras}");
        Assert.True(r.OverheadPct is >= 0 and <= 100);
        Assert.False(sessao.Ativa);
        Assert.Contains(r.Metricas, m => m.Nome == "Uso de CPU");
    }
}
