using System.Diagnostics;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>CPU Sets (parser puro + filtro P/E + aplicar/reverter temporário) e rede por interface
/// (caminhos puros + tweaks com valores literais do original).</summary>
public class CpuSetsERedeTests
{
    static byte[] EntradaCpuSet(uint id, ushort grupo, byte indice, byte eficiencia, uint size = 40)
    {
        var b = new byte[size];
        BitConverter.GetBytes(size).CopyTo(b, 0);
        BitConverter.GetBytes(id).CopyTo(b, 8);
        BitConverter.GetBytes(grupo).CopyTo(b, 12);
        b[14] = indice;
        b[18] = eficiencia;
        return b;
    }

    [Fact]
    public void Analisar_parseia_os_conjuntos_com_offsets_do_winnt()
    {
        var buffer = new byte[80];
        EntradaCpuSet(0x0000, 0, 0, 0).CopyTo(buffer, 0);   // E-core (classe 0)
        EntradaCpuSet(0x0108, 0, 8, 1).CopyTo(buffer, 40);  // P-core (classe 1)

        var conjuntos = CpuSets.Analisar(buffer);

        Assert.Equal(2, conjuntos.Count);
        Assert.Equal(0u, conjuntos[0].Id);
        Assert.Equal(0, conjuntos[0].EfficiencyClass);
        Assert.Equal(0x0108u, conjuntos[1].Id);
        Assert.Equal((byte)8, conjuntos[1].IndiceLogico);
        Assert.Equal(1, conjuntos[1].EfficiencyClass);
    }

    [Fact]
    public void IdsDePerformance_filtra_a_maior_classe_e_devolve_null_em_cpu_homogenea()
    {
        var hibrido = new[]
        {
            new ConjuntoCpu(0x0000, 0, 0, 0), new ConjuntoCpu(0x0001, 0, 1, 0),
            new ConjuntoCpu(0x0108, 0, 8, 1), new ConjuntoCpu(0x0109, 0, 9, 1),
        };
        Assert.Equal(new uint[] { 0x0108, 0x0109 }, CpuSets.IdsDePerformance(hibrido));

        var homogeneo = new[] { new ConjuntoCpu(0x0000, 0, 0, 1), new ConjuntoCpu(0x0001, 0, 1, 1) };
        Assert.Null(CpuSets.IdsDePerformance(homogeneo)); // sem divisão P/E → sem restrição
        Assert.Null(CpuSets.IdsDePerformance(Array.Empty<ConjuntoCpu>()));
    }

    [Fact]
    public void CpuSets_aplica_e_reverte_no_proprio_processo()
    {
        var p = Process.GetCurrentProcess();
        var ids = CpuSets.IdsDePerformance(CpuSets.Listar());
        if (ids is null) return; // CPU homogênea: nada a restringir

        var estado = AplicadorPerfil.Aplicar(p, 0, PrioridadeJogo.Normal, cpuSets: ids);
        try { Assert.NotNull(estado.CpuSetsAnteriores); }
        finally { AplicadorPerfil.Reverter(p, estado); } // restaura exatamente
    }

    [Fact]
    public void Caminhos_das_chaves_por_interface_sao_os_do_windows()
    {
        const string guid = "{8A2F-C1D3}";
        Assert.Equal(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + guid, RedePorInterface.CaminhoTcpip(guid));
        Assert.Equal(@"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_" + guid, RedePorInterface.CaminhoNetbt(guid));
        Assert.Equal(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\InterfaceSpecificParameters\" + guid, RedePorInterface.CaminhoDnscache(guid));
    }

    [Fact]
    public void Tweaks_do_adaptador_reproduzem_os_valores_literais_do_original()
    {
        var tweaks = RedePorInterface.TweaksPara("{1111-2222-3333-4444-5555}");

        Assert.Equal(5, tweaks.Count);
        Assert.All(tweaks, t => Assert.Equal(Colmeia.MaquinaLocal, t.Colmeia));
        Assert.All(tweaks, t => Assert.True(t.ExigeAdmin));
        Assert.All(tweaks, t => Assert.Equal("Rede", t.Categoria));

        var tcpack = tweaks.First(t => t.NomeValor == "TcpAckFrequency");
        Assert.Equal(1, tcpack.ValorOtimizado);
        Assert.EndsWith(RedePorInterface.Sanitizar("{1111-2222-3333-4444-5555}"), tcpack.Id);

        Assert.Equal(1, tweaks.First(t => t.NomeValor == "TCPNoDelay").ValorOtimizado);
        Assert.Equal(0, tweaks.First(t => t.NomeValor == "TcpDelAckTicks").ValorOtimizado);
        Assert.Equal(2, tweaks.First(t => t.NomeValor == "NetbiosOptions").ValorOtimizado);
        Assert.Equal(0, tweaks.First(t => t.NomeValor == "EnableMulticast").ValorOtimizado);
    }

    [Fact]
    public void Listar_interfaces_nao_lanca_e_entradas_tem_guid_e_descricao()
    {
        var interfaces = RedePorInterface.Listar(); // máquina real; pode ser vazia sem rede
        Assert.All(interfaces, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Guid));
            Assert.False(string.IsNullOrWhiteSpace(i.Descricao));
        });
    }
}
