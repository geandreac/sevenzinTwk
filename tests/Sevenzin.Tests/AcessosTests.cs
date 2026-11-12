using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class AcessosTests
{
    [Fact]
    public void Parse_le_uma_linha_tsv_valida()
    {
        var a = RegistroAcessos.Parse("2026-11-12T10:00:00.0000000Z\tmaria@x.com\tSucesso\t1");
        Assert.NotNull(a);
        Assert.Equal("maria@x.com", a!.Email);
        Assert.Equal("Sucesso", a.Resultado);
        Assert.True(a.Sucesso);
    }

    [Fact]
    public void Parse_marca_falha_quando_flag_zero()
    {
        var a = RegistroAcessos.Parse("2026-11-12T10:00:00.0000000Z\tx@x.com\tFalha de login\t0");
        Assert.NotNull(a);
        Assert.False(a!.Sucesso);
    }

    [Fact]
    public void Parse_null_para_linha_invalida()
    {
        Assert.Null(RegistroAcessos.Parse("linha sem tabs"));
        Assert.Null(RegistroAcessos.Parse(""));
    }
}
