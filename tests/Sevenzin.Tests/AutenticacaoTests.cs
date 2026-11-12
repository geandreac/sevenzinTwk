using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class AutenticacaoTests
{
    static (ServicoContas contas, IRepositorioUsuarios repo) NovoRepo()
    {
        var repo = new RepositorioUsuariosMemoria();
        return (new ServicoContas(repo), repo);
    }

    [Fact]
    public void Autenticar_com_credenciais_corretas_retorna_sucesso()
    {
        var (contas, repo) = NovoRepo();
        contas.Criar("admin@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
        var auth = new ServicoAutenticacao(repo);

        var r = auth.Autenticar("admin@escola.edu", "Sevenzin#2026");

        Assert.True(r.Ok);
        Assert.Equal("admin@escola.edu", r.Usuario!.Email);
    }

    [Fact]
    public void Autenticar_e_mail_normalizado_ignora_caixa_e_espacos()
    {
        var (contas, repo) = NovoRepo();
        contas.Criar("admin@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
        var auth = new ServicoAutenticacao(repo);

        Assert.True(auth.Autenticar("  ADMIN@Escola.edu ", "Sevenzin#2026").Ok);
    }

    [Fact]
    public void Senha_errada_e_e_mail_inexistente_dao_o_mesmo_erro_generico()
    {
        var (contas, repo) = NovoRepo();
        contas.Criar("admin@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
        var auth = new ServicoAutenticacao(repo);

        var senhaErrada = auth.Autenticar("admin@escola.edu", "errada999");
        var inexistente = auth.Autenticar("ninguem@escola.edu", "errada999");

        Assert.Equal(StatusAuth.CredenciaisInvalidas, senhaErrada.Status);
        Assert.Equal(StatusAuth.CredenciaisInvalidas, inexistente.Status);
        Assert.Equal(senhaErrada.Mensagem, inexistente.Mensagem); // não revela se o e-mail existe
        Assert.DoesNotContain("existe", senhaErrada.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Campos_vazios_retornam_dados_invalidos()
    {
        var (_, repo) = NovoRepo();
        var auth = new ServicoAutenticacao(repo);
        Assert.Equal(StatusAuth.DadosInvalidos, auth.Autenticar("", "x").Status);
        Assert.Equal(StatusAuth.DadosInvalidos, auth.Autenticar("a@b.com", "").Status);
    }

    [Fact]
    public void Forca_bruta_bloqueia_apos_o_limite_de_tentativas()
    {
        var (contas, repo) = NovoRepo();
        contas.Criar("admin@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
        var pol = new PoliticaForcaBruta(MaxTentativas: 3, Janela: TimeSpan.FromMinutes(5), Bloqueio: TimeSpan.FromMinutes(5));
        var auth = new ServicoAutenticacao(repo, pol: pol);

        Assert.Equal(StatusAuth.CredenciaisInvalidas, auth.Autenticar("admin@escola.edu", "x1").Status);
        Assert.Equal(StatusAuth.CredenciaisInvalidas, auth.Autenticar("admin@escola.edu", "x2").Status);
        Assert.Equal(StatusAuth.Bloqueado, auth.Autenticar("admin@escola.edu", "x3").Status);
        // Mesmo com a senha correta, continua bloqueado durante a janela.
        Assert.Equal(StatusAuth.Bloqueado, auth.Autenticar("admin@escola.edu", "Sevenzin#2026").Status);
    }

    [Fact]
    public void Bloqueio_expira_apos_a_janela_e_permite_login()
    {
        var (contas, repo) = NovoRepo();
        contas.Criar("admin@escola.edu", "Sevenzin#2026", PapelUsuario.Administrador);
        var relogio = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var pol = new PoliticaForcaBruta(MaxTentativas: 2, Bloqueio: TimeSpan.FromMinutes(5));
        var auth = new ServicoAutenticacao(repo, () => relogio, pol);

        auth.Autenticar("admin@escola.edu", "x");
        Assert.Equal(StatusAuth.Bloqueado, auth.Autenticar("admin@escola.edu", "x").Status);

        relogio = relogio.AddMinutes(6); // passou o bloqueio
        Assert.True(auth.Autenticar("admin@escola.edu", "Sevenzin#2026").Ok);
    }

    [Fact]
    public void Sessao_expira_e_renovar_estende_a_validade()
    {
        var relogio = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var ss = new ServicoSessao(() => relogio, TimeSpan.FromMinutes(10));
        var u = new Usuario("admin@escola.edu", "Admin", PapelUsuario.Administrador, "hash", relogio);

        ss.Abrir(u);
        Assert.True(ss.Valida);

        relogio = relogio.AddMinutes(5);
        Assert.NotNull(ss.Renovar());          // renova antes de expirar

        relogio = relogio.AddMinutes(12);       // 12 min após a renovação → expirou
        Assert.False(ss.Valida);
        Assert.Null(ss.Renovar());
        Assert.Null(ss.Atual);
    }

    [Fact]
    public void Autorizacao_admin_so_para_sessao_de_administrador_e_valida()
    {
        var agora = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var admin = new Sessao("t", new Usuario("a@e.edu", "Admin", PapelUsuario.Administrador, "h", agora), agora.AddMinutes(30));
        var comum = new Sessao("t", new Usuario("u@e.edu", "Usuario", PapelUsuario.Usuario, "h", agora), agora.AddMinutes(30));
        var expirada = admin with { ExpiraUtc = agora.AddMinutes(-1) };

        Assert.True(Autorizacao.PodeAcessarAdmin(admin, agora));
        Assert.False(Autorizacao.PodeAcessarAdmin(comum, agora));   // sem permissão
        Assert.False(Autorizacao.PodeAcessarAdmin(expirada, agora)); // sessão expirada
        Assert.False(Autorizacao.PodeAcessarAdmin(null, agora));
    }

    [Fact]
    public void Hash_bcrypt_nao_guarda_texto_e_confere_corretamente()
    {
        var hash = SegurancaSenha.Gerar("Sevenzin#2026");
        Assert.NotEqual("Sevenzin#2026", hash);
        Assert.StartsWith("$2", hash);                       // prefixo bcrypt
        Assert.True(SegurancaSenha.Confere("Sevenzin#2026", hash));
        Assert.False(SegurancaSenha.Confere("senha124", hash));
    }

    [Fact]
    public void Seed_cria_admin_das_variaveis_de_ambiente_sem_duplicar()
    {
        var (contas, _) = NovoRepo();
        var env = new Dictionary<string, string?>
        {
            [SemeadorAdmin.VarEmail] = "chefe@escola.edu",
            [SemeadorAdmin.VarSenha] = "Sevenzin#2026",
        };
        string? Ler(string k) => env.TryGetValue(k, out var v) ? v : null;

        Assert.Equal("chefe@escola.edu", SemeadorAdmin.Semear(contas, Ler));
        Assert.True(contas.Existe("chefe@escola.edu"));
        Assert.Equal(PapelUsuario.Administrador, contas.Todos.Single().Papel);

        Assert.Null(SemeadorAdmin.Semear(contas, Ler));       // não duplica
        Assert.Single(contas.Todos);
    }

    [Fact]
    public void Seed_sem_variaveis_de_ambiente_nao_cria_nada()
    {
        var (contas, _) = NovoRepo();
        Assert.Null(SemeadorAdmin.Semear(contas, _ => null));
        Assert.Empty(contas.Todos);
    }
}
