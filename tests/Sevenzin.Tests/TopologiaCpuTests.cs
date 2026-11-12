using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Topologia real da CPU. Os valores variam por hardware, então os testes
/// checam consistência interna e a integração no coletor, não números fixos.</summary>
public class TopologiaCpuTests
{
    [Fact]
    public void Coletor_cpu_emite_topologia_real_sem_placeholder()
    {
        var cpu = ColetorCpu.Coletar();
        var pe = cpu.FirstOrDefault(i => i.Nome == "Núcleos P/E");

        // Em uma máquina Windows real a API responde: linha de confiança Fato, não a hipótese antiga.
        Assert.NotNull(pe);
        Assert.Equal(Confianca.Fato, pe!.Tipo);
        Assert.False(string.IsNullOrEmpty(pe.Valor));
        Assert.DoesNotContain(cpu, i => i.Nome == "Núcleos P/E e CCD"); // placeholder não deve mais aparecer
    }

    [Fact]
    public void Topologia_e_internamente_consistente()
    {
        var topo = TopologiaCpu.Ler();
        Assert.NotNull(topo);
        var t = topo!.Value;

        Assert.True(t.NucleosFisicos > 0);
        Assert.True(t.DominiosL3 >= 0);
        if (t.Hibrido)
        {
            Assert.True(t.PCores > 0 && t.ECores > 0);          // híbrido exige as duas classes
            Assert.Equal(t.NucleosFisicos, t.PCores + t.ECores); // P+E soma o total físico
        }
        else
            Assert.True(t.PCores == 0 && t.ECores == 0);        // homogêneo não separa P/E
    }

    [Fact]
    public void Mascaras_de_afinidade_p_e_sao_consistentes()
    {
        var topo = TopologiaCpu.Ler();
        Assert.NotNull(topo);
        var t = topo!.Value;
        long todos = AplicadorPerfil.MascaraTodosNucleos;

        Assert.Equal(0L, t.MascaraPCores & ~todos);   // P ⊆ todos os núcleos
        Assert.Equal(0L, t.MascaraECores & ~todos);   // E ⊆ todos os núcleos
        Assert.NotEqual(0L, t.MascaraPCores);         // sempre há P-cores
        if (!t.Hibrido)
        {
            Assert.Equal(0L, t.MascaraECores);        // homogêneo: sem E
            Assert.Equal(todos, t.MascaraPCores);     // homogêneo: P = todos
        }
        else
            Assert.Equal(0L, t.MascaraPCores & t.MascaraECores); // híbrido: P e E disjuntos
    }
}
