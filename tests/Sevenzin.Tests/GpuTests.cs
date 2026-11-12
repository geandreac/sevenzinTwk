using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class GpuTests
{
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Ti", FabricanteGpu.Nvidia)]
    [InlineData("NVIDIA GTX 1660 SUPER", FabricanteGpu.Nvidia)]
    [InlineData("AMD Radeon RX 6700 XT", FabricanteGpu.Amd)]
    [InlineData("Radeon(TM) Graphics", FabricanteGpu.Amd)]
    [InlineData("Intel(R) UHD Graphics 630", FabricanteGpu.Intel)]
    [InlineData("Intel Iris Xe Graphics", FabricanteGpu.Intel)]
    [InlineData("Microsoft Basic Display Adapter", FabricanteGpu.Desconhecido)]
    [InlineData(null, FabricanteGpu.Desconhecido)]
    public void Detectar_identifica_o_fabricante(string? nome, FabricanteGpu esperado)
        => Assert.Equal(esperado, DriverGpu.Detectar(nome));

    [Theory]
    [InlineData(FabricanteGpu.Nvidia, "https://www.nvidia.com/")]
    [InlineData(FabricanteGpu.Amd, "https://www.amd.com/")]
    [InlineData(FabricanteGpu.Intel, "https://www.intel.com/")]
    public void UrlOficial_aponta_para_o_dominio_do_fabricante(FabricanteGpu f, string prefixo)
    {
        var url = DriverGpu.UrlOficialDriver(f);
        Assert.NotNull(url);
        Assert.StartsWith(prefixo, url);
    }

    [Fact]
    public void UrlOficial_e_nula_para_fabricante_desconhecido()
        => Assert.Null(DriverGpu.UrlOficialDriver(FabricanteGpu.Desconhecido));

    [Fact]
    public void PareceAntigo_usa_o_limite_de_18_meses()
    {
        var agora = new DateTime(2026, 11, 11);
        Assert.True(DriverGpu.PareceAntigo(agora.AddDays(-600), agora));  // > 18 meses
        Assert.False(DriverGpu.PareceAntigo(agora.AddDays(-100), agora)); // recente
        Assert.False(DriverGpu.PareceAntigo(null, agora));                // sem data → não afirma
    }

    [Fact]
    public void Coletar_retorna_gpu_real_com_url_coerente()
    {
        var gpus = DiagnosticoGpu.Coletar();
        Assert.NotEmpty(gpus); // máquina real tem ao menos um adaptador
        Assert.All(gpus, g =>
        {
            Assert.False(string.IsNullOrEmpty(g.Adaptador));
            Assert.Equal(DriverGpu.UrlOficialDriver(g.Fabricante), g.UrlDriverOficial); // URL coerente com o fabricante
        });
    }
}
