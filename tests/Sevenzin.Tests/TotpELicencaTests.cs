using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F5: TOTP (RFC 6238), licença local e fluxo de autenticação com 2FA.</summary>
public class TotpELicencaTests
{
    const string SegredoVetor = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"; // base32 de "12345678901234567890"
    static DateTime Era = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Totp_reproduz_os_vetores_oficiais_rfc_6238_em_6_digitos()
    {
        Assert.Equal("287082", Totp.Calcular(SegredoVetor, Era.AddSeconds(59)));
        Assert.Equal("050471", Totp.Calcular(SegredoVetor, Era.AddSeconds(1111111111)));
        Assert.Equal("005924", Totp.Calcular(SegredoVetor, Era.AddSeconds(1234567890)));
        Assert.Equal("279037", Totp.Calcular(SegredoVetor, Era.AddSeconds(2000000000)));
    }

    [Fact]
    public void Totp_confere_o_codigo_atual_e_rejeita_errado()
    {
        var agora = DateTime.UtcNow;
        var codigo = Totp.Calcular(SegredoVetor, agora);
        Assert.True(Totp.Confere(SegredoVetor, codigo, agora));
        Assert.False(Totp.Confere(SegredoVetor, "000000", agora));
        Assert.False(Totp.Confere(SegredoVetor, null, agora));
    }

    [Fact]
    public void ValidadorLicenca_normaliza_e_valida_o_formato()
    {
        Assert.Equal("A1B2-C3D4-E5F6-G7H8", ValidadorLicenca.Normalizar("a1b2 c3d4 e5f6 g7h8"));
        Assert.True(ValidadorLicenca.ChaveValida("A1B2-C3D4-E5F6-G7H8"));
        Assert.False(ValidadorLicenca.ChaveValida("curta"));
        Assert.False(ValidadorLicenca.ChaveValida("A1B2-C3D4-E5F6-G7H!"));
        Assert.False(ValidadorLicenca.ChaveValida(null));
    }

    [Fact]
    public void LicencaStore_persiste_e_tolera_corrompido()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"sevenzin-lic-{Guid.NewGuid():N}.json");
        try
        {
            var store = new LicencaStore(caminho);
            Assert.False(store.Carregar().Ativa);

            store.Salvar(new Licenca("A1B2-C3D4-E5F6-G7H8", DateTime.UtcNow));
            var lida = new LicencaStore(caminho).Carregar();
            Assert.True(lida.Ativa);
            Assert.Equal("A1B2-C3D4-E5F6-G7H8", lida.Chave);

            File.WriteAllText(caminho, "{x");
            Assert.False(new LicencaStore(caminho).Carregar().Ativa);
        }
        finally { try { File.Delete(caminho); File.Delete(caminho + ".tmp"); } catch { } }
    }

    [Fact]
    public void Autenticacao_exige_2fa_quando_ativo_e_aceita_o_codigo_correto()
    {
        var repo = new RepositorioUsuariosMemoria();
        var contas = new ServicoContas(repo);
        var auth = new ServicoAutenticacao(repo);

        contas.Criar("eu@exemplo.com", "SenhaForte#123", PapelUsuario.Administrador);
        var segredo = contas.GerarSegredo2FA("eu@exemplo.com");
        Assert.NotNull(segredo);
        Assert.True(contas.Confirmar2FA("eu@exemplo.com", Totp.Calcular(segredo!, DateTime.UtcNow)));

        var semCodigo = auth.Autenticar("eu@exemplo.com", "SenhaForte#123");
        Assert.Equal(StatusAuth.Exige2FA, semCodigo.Status); // senha ok, mas 2FA ativo

        var codigoErrado = auth.Autenticar("eu@exemplo.com", "SenhaForte#123", "000000");
        Assert.Equal(StatusAuth.CredenciaisInvalidas, codigoErrado.Status);

        var codigoCerto = auth.Autenticar("eu@exemplo.com", "SenhaForte#123", Totp.Calcular(segredo!, DateTime.UtcNow));
        Assert.Equal(StatusAuth.Sucesso, codigoCerto.Status);
    }

    [Fact]
    public void Confirmar2fa_com_codigo_errado_nao_ativa_o_fator()
    {
        var repo = new RepositorioUsuariosMemoria();
        var contas = new ServicoContas(repo);
        contas.Criar("eu@exemplo.com", "SenhaForte#123", PapelUsuario.Usuario);
        contas.GerarSegredo2FA("eu@exemplo.com");

        Assert.False(contas.Confirmar2FA("eu@exemplo.com", "000000"));
        var auth = new ServicoAutenticacao(repo);
        Assert.Equal(StatusAuth.Sucesso, auth.Autenticar("eu@exemplo.com", "SenhaForte#123").Status); // 2FA NÃO ativo
    }
}
