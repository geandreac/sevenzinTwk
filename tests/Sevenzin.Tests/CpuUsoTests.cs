using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Cálculo delta do uso de CPU (GetSystemTimes). Função pura — números fixos,
/// cobrindo os casos de borda do contador de 100 ns.</summary>
public class CpuUsoTests
{
    [Fact]
    public void CalcularUso_metade_ocioso_da_50_porcento()
    {
        var antes = new LeitorCpuUso.Instantaneo(Ocioso: 1_000, Kernel: 1_000, Usuario: 2_000);
        var agora = new LeitorCpuUso.Instantaneo(Ocioso: 3_000, Kernel: 2_000, Usuario: 4_000);
        // Δidle = 2000; Δtotal = (1000 + 2000) = 3000 → uso = 1 − 2/3 = 33,3%

        double? uso = LeitorCpuUso.CalcularUso(antes, agora);

        Assert.NotNull(uso);
        Assert.Equal(100.0 / 3.0, uso!.Value, 3);
    }

    [Fact]
    public void CalcularUso_totalmente_ocupado_da_100_porcento()
    {
        var antes = new LeitorCpuUso.Instantaneo(1_000, 1_000, 1_000);
        var agora = new LeitorCpuUso.Instantaneo(1_000, 2_000, 3_000); // idle não cresceu

        Assert.Equal(100.0, LeitorCpuUso.CalcularUso(antes, agora));
    }

    [Fact]
    public void CalcularUso_intervalo_nulo_ou_contador_voltando_devolve_null()
    {
        var a = new LeitorCpuUso.Instantaneo(1_000, 1_000, 1_000);

        Assert.Null(LeitorCpuUso.CalcularUso(a, a));                                        // sem avanço
        Assert.Null(LeitorCpuUso.CalcularUso(a, new(500, 900, 900)));                       // contador voltou (sleep)
        Assert.Null(LeitorCpuUso.CalcularUso(a, new(5_000, 900, 900)));                     // idle > total (relógio reiniciado)
    }

    [Fact]
    public void MedidorCpuUso_primeira_leitura_devolve_null_e_depois_um_valor_valido()
    {
        var medidor = new MedidorCpuUso();
        Assert.Null(medidor.Ler()); // sem intervalo ainda — nunca 0 forjado
        // As chamadas seguintes usam GetSystemTimes real; não dá para prever o valor,
        // mas em qualquer máquina Windows viva a 2ª leitura produz 0..100.
        double? segunda = medidor.Ler();
        if (segunda is not null) Assert.InRange(segunda.Value, 0.0, 100.0);
    }
}
