using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class UsuariosTests
{
    static string ArqTemp() => Path.Combine(Path.GetTempPath(), "sevenzin-users-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Senha_confere_a_correta_e_rejeita_a_errada()
    {
        var hash = SegurancaSenha.Gerar("Sevenzin123");
        Assert.True(SegurancaSenha.Confere("Sevenzin123", hash));
        Assert.False(SegurancaSenha.Confere("errada999", hash));
    }

    [Fact]
    public void Hash_e_diferente_a_cada_geracao_mesmo_com_a_mesma_senha()
    {
        var a = SegurancaSenha.Gerar("mesmaSenha1");
        var b = SegurancaSenha.Gerar("mesmaSenha1");
        Assert.NotEqual(a, b); // bcrypt embute salt aleatório => hashes diferentes
    }

    [Theory]
    [InlineData("ab", false)]                 // sem @ nem domínio
    [InlineData("a@b", false)]                // sem ponto no domínio
    [InlineData("prof@escola.edu", true)]
    [InlineData("user_01@x.co", true)]
    public void Validacao_de_email(string email, bool valido)
        => Assert.Equal(valido, ValidacaoCredenciais.ValidarEmail(email).Count == 0);

    [Theory]
    [InlineData("Abc#1", false)]            // curta (< 12)
    [InlineData("abcdefghijk#1", false)]    // sem maiúscula
    [InlineData("Abcdefghijkl1", false)]    // sem símbolo
    [InlineData("Abcdefghijkl#", false)]    // sem número
    [InlineData("Sevenzin#2026", true)]
    public void Validacao_de_senha(string senha, bool valido)
        => Assert.Equal(valido, ValidacaoCredenciais.ValidarSenha(senha).Count == 0);

    [Fact]
    public void Senha_nao_pode_conter_o_nome_nem_o_e_mail()
    {
        Assert.False(ValidacaoCredenciais.ValidarSenha("Mariana#2026xx", email: "mariana@x.edu").Count == 0);
        Assert.True(ValidacaoCredenciais.ValidarSenha("Sevenzin#2026", email: "mariana@x.edu", nome: "Mariana Costa").Count == 0);
    }

    [Fact]
    public void Gerar_senha_forte_atende_todos_os_requisitos()
    {
        for (int i = 0; i < 20; i++)
        {
            var s = ValidacaoCredenciais.GerarSenhaForte();
            Assert.True(s.Length >= 12);
            Assert.Empty(ValidacaoCredenciais.ValidarSenha(s));
        }
    }

    [Fact]
    public void Criar_autentica_persiste_e_impede_duplicado()
    {
        var arq = ArqTemp();
        try
        {
            var contas = new ServicoContas(new RepositorioUsuariosArquivo(arq));
            contas.Criar("professor@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
            Assert.True(contas.Existe("PROFESSOR@Escola.edu"));                          // case-insensitive

            var auth = new ServicoAutenticacao(new RepositorioUsuariosArquivo(arq));
            Assert.True(auth.Autenticar("professor@escola.edu", "Sevenzin#2026").Ok);        // senha certa
            Assert.False(auth.Autenticar("professor@escola.edu", "outra1234").Ok);       // senha errada
            Assert.Throws<ArgumentException>(() => contas.Criar("professor@escola.edu", "Sevenzin#2026", PapelUsuario.Usuario)); // duplicado
            Assert.Throws<ArgumentException>(() => contas.Criar("x", "123", PapelUsuario.Usuario));                           // inválido

            var recarregado = new ServicoContas(new RepositorioUsuariosArquivo(arq));    // persistiu
            Assert.Single(recarregado.Todos);
            Assert.Equal(PapelUsuario.Administrador, recarregado.Todos[0].Papel);
            Assert.True(recarregado.Remover("professor@escola.edu"));
            Assert.Empty(new ServicoContas(new RepositorioUsuariosArquivo(arq)).Todos);
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Senha_nunca_e_guardada_em_texto()
    {
        var arq = ArqTemp();
        try
        {
            new ServicoContas(new RepositorioUsuariosArquivo(arq)).Criar("joao@escola.edu", "Sevenzin#2026", PapelUsuario.Usuario);
            var conteudo = File.ReadAllText(arq);
            Assert.DoesNotContain("Sevenzin#2026", conteudo); // o arquivo não contém a senha em claro
        }
        finally { File.Delete(arq); }
    }
}
