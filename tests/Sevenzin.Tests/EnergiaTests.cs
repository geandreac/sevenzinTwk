using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class EnergiaTests
{
    [Fact]
    public void ExtrairGuid_pega_o_guid_da_saida_do_getactivescheme()
    {
        var saida = "Esquema de Energia GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Equilibrado)";
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", GestorEnergia.ExtrairGuid(saida));
    }

    [Fact]
    public void ExtrairGuid_null_quando_nao_ha_guid()
        => Assert.Null(GestorEnergia.ExtrairGuid("sem guid aqui"));

    [Fact]
    public void ExtrairNome_pega_o_nome_entre_parenteses()
        => Assert.Equal("Sevenzin", GestorEnergia.ExtrairNome("GUID: 11111111-2222-3333-4444-555555555555  (Sevenzin)"));

    [Fact]
    public void GuidsPorNome_encontra_so_os_planos_com_o_nome()
    {
        var lista = string.Join('\n',
            "Esquema de Energia GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Equilibrado) *",
            "Esquema de Energia GUID: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee  (Sevenzin)",
            "Esquema de Energia GUID: 11111111-2222-3333-4444-555555555555  (Alto desempenho)");
        var achados = GestorEnergia.GuidsPorNome(lista, "Sevenzin");
        Assert.Single(achados);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", achados[0]);
    }
}
