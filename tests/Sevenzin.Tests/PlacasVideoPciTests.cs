using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class PlacasVideoPciTests
{
    [Fact]
    public void Le_fabricante_e_dispositivo_do_id_de_instancia()
    {
        Assert.Equal((FabricanteGpu.Nvidia, "2414"), PlacasVideoPci.LerIds(@"PCI\VEN_10DE&DEV_2414&SUBSYS_241410DE&REV_A1\4&2283F625&0&0019"));
        Assert.Equal((FabricanteGpu.Amd, "73BF"), PlacasVideoPci.LerIds(@"PCI\VEN_1002&DEV_73bf&SUBSYS_0E3A1002"));
        Assert.Null(PlacasVideoPci.LerIds(@"USB\VID_046D&PID_C52B"));
    }

    [Fact]
    public void Classe_pci_03_e_controlador_de_video_mesmo_sem_driver()
    {
        Assert.True(PlacasVideoPci.EhControladorDeVideo(new[] { @"PCI\VEN_10DE&DEV_2414&REV_A1", @"PCI\CC_030000", @"PCI\CC_0300" }));
        Assert.False(PlacasVideoPci.EhControladorDeVideo(new[] { @"PCI\CC_040300" })); // áudio HDMI da mesma placa
        Assert.False(PlacasVideoPci.EhControladorDeVideo(null));
    }

    [Fact]
    public void Estado_do_driver()
    {
        Assert.Equal(EstadoDriverPlaca.Ausente, PlacasVideoPci.Estado(null, null)); // caso do PC sem driver nenhum
        Assert.Equal(EstadoDriverPlaca.Basico, PlacasVideoPci.Estado("Display", "BasicDisplay"));
        Assert.Equal(EstadoDriverPlaca.Instalado, PlacasVideoPci.Estado("Display", "nvlddmkm"));
    }

    [Theory]
    [InlineData("GA103 [GeForce RTX 3060 Ti]", "NVIDIA GeForce RTX 3060 Ti")]
    [InlineData("GA104 [GeForce RTX 3060 Ti Lite Hash Rate]", "NVIDIA GeForce RTX 3060 Ti")]
    [InlineData("GA106M [GeForce RTX 3060 Mobile / Max-Q]", "NVIDIA GeForce RTX 3060 Mobile")]
    [InlineData("NV1 Series", "NVIDIA NV1 Series")]
    public void Nome_comercial_vem_dos_colchetes(string descricao, string esperado) =>
        Assert.Equal(esperado, PlacasVideoPci.NomeComercialNvidia(descricao));

    [Fact]
    public void Tabela_embutida_identifica_a_rtx_3060_ti_do_caso_real()
    {
        Assert.Equal("NVIDIA GeForce RTX 3060 Ti", PlacasVideoPci.NomeNvidia("2414"));
        Assert.Null(PlacasVideoPci.NomeNvidia("FFFF"));
        Assert.True(NvidiaDebloat.EhNotebookGpu(PlacasVideoPci.NomeComercialNvidia("GA106M [GeForce RTX 3060 Mobile / Max-Q]")));
    }

    [Fact]
    public void Catalogo_sem_o_modelo_usa_a_serie_geforce_mais_nova()
    {
        var series = new List<ItemLookupNvidia>
        {
            new("GeForce RTX 40 Series", 127, 1),
            new("GeForce RTX 50 Series", 131, 1),
            new("GeForce RTX 50 Series (Notebooks)", 133, 1),
            new("GeForce GTX 16 Series", 112, 1),
        };
        var modelos = new List<ItemLookupNvidia>
        {
            new("GeForce RTX 4090", 995, 127),
            new("GeForce RTX 5090", 1066, 131),
            new("GeForce RTX 5090 Laptop GPU", 1080, 133),
        };
        Assert.False(NvidiaDebloat.ResolverIdsCatalogo("NVIDIA GPU", false, series, modelos, out _, out _, out _));
        Assert.True(NvidiaDebloat.ResolverSerieMaisRecente(false, series, modelos, out var psid, out var pfid, out _));
        Assert.Equal((131, 1066), (psid, pfid));
        Assert.True(NvidiaDebloat.ResolverSerieMaisRecente(true, series, modelos, out psid, out pfid, out _));
        Assert.Equal((133, 1080), (psid, pfid));
    }
}
