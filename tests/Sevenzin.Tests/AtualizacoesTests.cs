using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class AtualizacoesTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("V1.4.0", 1, 4, 0)]
    [InlineData("1.2", 1, 2, 0)]        // parte ausente vira 0
    [InlineData("1.0.0-beta", 1, 0, 0)] // sufixo pré-release descartado
    [InlineData("2.5.1+build7", 2, 5, 1)]
    public void VersaoDaTag_parseia_formatos_comuns(string tag, int ma, int mi, int bu)
        => Assert.Equal(new Version(ma, mi, bu), VerificadorAtualizacoes.VersaoDaTag(tag));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("nightly")]
    [InlineData("v")]
    public void VersaoDaTag_retorna_null_para_tag_invalida(string tag)
        => Assert.Null(VerificadorAtualizacoes.VersaoDaTag(tag));

    [Fact]
    public void EhMaisNova_ignora_a_4a_componente_do_assembly()
    {
        // Assembly vem como 1.0.0.0; tag "1.0.0" não pode ser considerada mais nova (nem mais antiga) que ela.
        Assert.False(VerificadorAtualizacoes.EhMaisNova(new Version(1, 0, 0, 0), new Version(1, 0, 0)));
    }

    [Fact]
    public void EhMaisNova_detecta_versao_maior() =>
        Assert.True(VerificadorAtualizacoes.EhMaisNova(new Version(1, 0, 0, 0), new Version(1, 0, 1)));

    [Fact]
    public void EhMaisNova_rejeita_versao_menor() =>
        Assert.False(VerificadorAtualizacoes.EhMaisNova(new Version(1, 2, 0, 0), new Version(1, 1, 9)));
}
