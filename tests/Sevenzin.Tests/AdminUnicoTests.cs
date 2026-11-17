using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Administrador único (Geandre): a identidade provisionada é a ÚNICA capaz de ser administradora.
/// Gate no domínio (não é proteção de UI), convites de uso único com hash, e reconciliação que rebaixa
/// impostores. Nenhuma credencial em código — a identidade vem de SEVENZIN_ADMIN_EMAIL.</summary>
public class AdminUnicoTests : IDisposable
{
    readonly string _arqConvites = Path.Combine(Path.GetTempPath(), $"sevenzin-conv-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_arqConvites); File.Delete(_arqConvites + ".tmp"); } catch { }
    }

    const string Geandre = "geandre@dominio.com";

    static ServicoContas Contas(string identidade, string? arqConvites = null) =>
        new(new RepositorioUsuariosMemoria(), _ => identidade, arqConvites is null ? null : new RepositorioConvites(arqConvites));

    [Fact]
    public void Somente_a_identidade_provisionada_pode_ser_administradora()
    {
        var contas = Contas(Geandre);

        contas.Criar(Geandre, "SenhaForte#123", PapelUsuario.Administrador); // o único caso permitido
        Assert.Throws<ArgumentException>(() => contas.Criar("outro@dominio.com", "SenhaForte#123", PapelUsuario.Administrador));
        contas.Criar("outro@dominio.com", "SenhaForte#123", PapelUsuario.Usuario); // usuário comum, sem problema
    }

    [Fact]
    public void Sem_identidade_configurada_ninguem_e_administrador()
    {
        var contas = Contas(""); // env vazio → identidade nula
        Assert.Throws<ArgumentException>(() => contas.Criar(Geandre, "SenhaForte#123", PapelUsuario.Administrador));
    }

    [Fact]
    public void GarantirAdminUnico_rebaixa_impostores_e_preserva_o_geandre()
    {
        var repo = new RepositorioUsuariosMemoria();
        var contas = new ServicoContas(repo, _ => Geandre);
        contas.Criar(Geandre, "SenhaForte#123", PapelUsuario.Administrador);

        // injeta um admin "legado" diretamente no repositório (simula dados antigos)
        repo.Adicionar(new Usuario("legado@x.com", "Legado", PapelUsuario.Administrador, "hash", DateTime.UtcNow));

        Assert.Equal(1, contas.GarantirAdminUnico());
        Assert.Equal(PapelUsuario.Usuario, repo.PorEmail("legado@x.com")!.Papel);       // rebaixado
        Assert.Equal(PapelUsuario.Administrador, repo.PorEmail(Geandre)!.Papel);       // preservado
    }

    [Fact]
    public void Sessao_so_da_autorizacao_ao_admin_protegido()
    {
        var agora = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        var admin = new Sessao("t", new Usuario(Geandre, "Geandre", PapelUsuario.Administrador, "h", agora), agora.AddMinutes(30));
        Assert.True(Autorizacao.PodeAcessarAdmin(admin, agora, Geandre));

        var ss = new ServicoSessao(() => agora, lerEnv: _ => Geandre);
        ss.Abrir(new Usuario(Geandre, "Geandre", PapelUsuario.Administrador, "h", agora));
        Assert.True(ss.PodeAcessarAdmin);
        ss.Abrir(new Usuario("outro@x.com", "Outro", PapelUsuario.Administrador, "h", agora));
        Assert.False(ss.PodeAcessarAdmin); // papel certo, e-mail errado → negado
    }

    [Fact]
    public void Convite_e_de_uso_unico_e_persiste_apenas_o_hash()
    {
        var convites = new RepositorioConvites(_arqConvites);
        var codigo = convites.Gerar();

        Assert.True(IdentidadeAdmin.CodigoConviteValido(codigo));           // formato
        Assert.Equal(12, codigo.Length);
        Assert.True(convites.Consumir(codigo));                             // 1º uso: ok
        Assert.False(convites.Consumir(codigo));                            // 2º uso: recusado
        Assert.False(convites.Consumir("ABCDEFGHJKLM"));                    // nunca gerado
        Assert.False(convites.Consumir(null));

        var disco = File.ReadAllText(_arqConvites);
        Assert.DoesNotContain(codigo, disco);                               // código em claro NUNCA no disco
    }

    [Fact]
    public void Registro_publico_exige_convite_e_cria_sempre_usuario()
    {
        var convites = new RepositorioConvites(_arqConvites);
        var codigo = convites.Gerar();
        var contas = Contas(Geandre, _arqConvites);

        var usuario = contas.RegistrarComConvite("novo@dominio.com", "SenhaForte#123", codigo);
        Assert.Equal(PapelUsuario.Usuario, usuario.Papel);                  // nunca administrador
        Assert.Throws<ArgumentException>(() => contas.RegistrarComConvite("x@dominio.com", "SenhaForte#123", codigo)); // já usado
        Assert.Throws<ArgumentException>(() => contas.RegistrarComConvite("y@dominio.com", "SenhaForte#123", "INVALIDO123"));
    }

    [Fact]
    public void Codigo_convite_valida_formato_exato()
    {
        Assert.True(IdentidadeAdmin.CodigoConviteValido("ABCDEFGHJKLM"));
        Assert.True(IdentidadeAdmin.CodigoConviteValido(" abcdefghjklm ")); // case/trim tolerante
        Assert.False(IdentidadeAdmin.CodigoConviteValido("curto"));
        Assert.False(IdentidadeAdmin.CodigoConviteValido("ABCDEFGHJKLO"));  // O (zero) fora do alfabeto sem ambíguos
        Assert.False(IdentidadeAdmin.CodigoConviteValido("123456789012"));  // 1 fora do alfabeto
    }
}
