using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class RepositorioMongoTests
{
    [Fact]
    public void Mapeamento_documento_roundtrip_preserva_campos()
    {
        var u = new Usuario("Maria@X.com", "Maria", PapelUsuario.Administrador, "$2a$12$hash",
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "SEGREDO2FA", true);
        var v = RepositorioUsuariosMongo.DeDoc(RepositorioUsuariosMongo.ParaDoc(u));
        Assert.Equal(u.Email, v.Email);
        Assert.Equal(u.Nome, v.Nome);
        Assert.Equal(u.Papel, v.Papel);
        Assert.Equal(u.HashSenha, v.HashSenha);
        Assert.Equal(u.CriadoUtc, v.CriadoUtc);
        Assert.Equal(u.TotpSegredo, v.TotpSegredo);
        Assert.Equal(u.TotpAtivo, v.TotpAtivo);
    }

    [Fact]
    public void Mapeamento_sem_2fa_vira_null_e_false()
    {
        var u = new Usuario("a@b.com", "A", PapelUsuario.Usuario, "hash", DateTime.UtcNow);
        var v = RepositorioUsuariosMongo.DeDoc(RepositorioUsuariosMongo.ParaDoc(u));
        Assert.Null(v.TotpSegredo);
        Assert.False(v.TotpAtivo);
        Assert.Equal(PapelUsuario.Usuario, v.Papel);
    }
}
