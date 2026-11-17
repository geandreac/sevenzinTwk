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

    [Fact]
    public void Indices_de_energia_do_original_estao_completos_e_com_valores_literais()
    {
        Assert.Equal(134, IndicesEnergiaOriginal.Todos.Count); // todos os setacvalueindex do original
        Assert.Contains(("0012ee47-9041-4b5d-9b77-535fba8b1442", "51dea550-bb38-4bc4-991b-eacf37be5ec8", "100"), IndicesEnergiaOriginal.Todos);
        Assert.Contains(("0012ee47-9041-4b5d-9b77-535fba8b1442", "6738e2c4-e8a5-4a42-b16a-e040e769756e", "0"), IndicesEnergiaOriginal.Todos);
        Assert.All(IndicesEnergiaOriginal.Todos, t =>
        {
            Assert.Matches(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", t.Subgrupo);
            Assert.Matches(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", t.Configuracao);
            Assert.False(string.IsNullOrWhiteSpace(t.Valor));
        });
    }
}
