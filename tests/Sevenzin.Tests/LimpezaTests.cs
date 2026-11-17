using System;
using System.IO;
using System.Linq;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class LimpezaTests
{
    [Fact]
    public void So_a_Lixeira_e_permanente()
    {
        Assert.Equal(3, Limpeza.Alvos.Count);
        Assert.All(Limpeza.Alvos, a => Assert.Equal(a.Id == CategoriaLimpeza.Lixeira, a.Permanente));
    }

    [Fact]
    public void TamanhoPasta_soma_recursivamente_pulando_nada_de_fixture()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "sevenzin_lt_" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(raiz, "sub");
        Directory.CreateDirectory(sub);
        try
        {
            File.WriteAllBytes(Path.Combine(raiz, "a.bin"), new byte[100]);
            File.WriteAllBytes(Path.Combine(sub, "b.bin"), new byte[23]);
            Assert.Equal(123, Limpeza.TamanhoPasta(raiz));
        }
        finally { Directory.Delete(raiz, recursive: true); }
    }

    [Fact]
    public void TamanhoPasta_inexistente_e_zero() =>
        Assert.Equal(0, Limpeza.TamanhoPasta(Path.Combine(Path.GetTempPath(), "nao_existe_" + Guid.NewGuid().ToString("N"))));

    [Fact]
    public void DentroDe_aceita_a_propria_raiz_e_subcaminhos()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "sevenzin_root");
        Assert.True(Limpeza.DentroDe(raiz, new[] { raiz }));
        Assert.True(Limpeza.DentroDe(Path.Combine(raiz, "sub", "x.tmp"), new[] { raiz }));
    }

    [Fact]
    public void DentroDe_recusa_fora_da_raiz_e_escapes()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "sevenzin_root");
        Assert.False(Limpeza.DentroDe(Path.Combine(Path.GetTempPath(), "outra", "x.tmp"), new[] { raiz }));
        Assert.False(Limpeza.DentroDe(Path.Combine(raiz, "..", "..", "Windows", "System32"), new[] { raiz })); // ".." colapsa p/ fora
        Assert.False(Limpeza.DentroDe(raiz, Array.Empty<string>()));
    }
}
