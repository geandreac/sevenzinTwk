using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class RecuperacaoSenhaTests
{
    [Theory]
    [InlineData("123456", true)]
    [InlineData(" 123 456 ", true)]   // colado do e-mail com espaços
    [InlineData("123-456", true)]
    [InlineData("12345", false)]      // curto
    [InlineData("abcdef", false)]
    [InlineData(null, false)]
    public void CodigoValido(string? codigo, bool esperado) => Assert.Equal(esperado, RecuperacaoSenha.CodigoValido(codigo));

    [Fact]
    public void NormalizarCodigo_mantem_so_digitos() => Assert.Equal("123456", RecuperacaoSenha.NormalizarCodigo(" 12-34 56 "));

    [Fact]
    public void Validar_ok_com_codigo_e_senha_forte()
        => Assert.Empty(RecuperacaoSenha.Validar("fulano@exemplo.com", "123456", "Nova#Senha2026x", "Nova#Senha2026x"));

    [Fact]
    public void Validar_acusa_codigo_invalido_e_senhas_diferentes()
    {
        var erros = RecuperacaoSenha.Validar("fulano@exemplo.com", "12", "Nova#Senha2026x", "Outra#Senha2026");
        Assert.Contains(erros, e => e.Contains("código"));
        Assert.Contains("As senhas não conferem.", erros);
    }

    [Fact]
    public void Validar_aplica_politica_de_senha_forte() =>
        Assert.NotEmpty(RecuperacaoSenha.Validar("fulano@exemplo.com", "123456", "fraca", "fraca"));

    [Theory]
    [InlineData(429, null, "Muitos pedidos")]
    [InlineData(403, "Token has expired or is invalid", "inválido ou expirado")]
    [InlineData(422, "New password should be different from the old password.", "diferente da atual")]
    public void Mensagem_traduz_erros_do_supabase(int status, string? erro, string trecho)
        => Assert.Contains(trecho, RecuperacaoSenha.Mensagem(status, erro));
}
