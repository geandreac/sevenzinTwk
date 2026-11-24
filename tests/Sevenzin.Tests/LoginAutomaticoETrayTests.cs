using System.IO;
using System.Text;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public sealed class LoginAutomaticoETrayTests
{
    static string PastaTemp()
    {
        var p = Path.Combine(Path.GetTempPath(), "sevenzin-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public void PreferenciasUiStore_padroes_e_persistencia_de_inicio_minimizado_e_login_auto()
    {
        var pasta = PastaTemp();
        try
        {
            var arq = Path.Combine(pasta, "prefs.json");
            var store = new PreferenciasUiStore(arq);

            // Valores padrão: iniciar minimizado nos Tray Icons e login automático ativados por padrão
            var padrao = store.Carregar();
            Assert.True(padrao.IniciarMinimizadoNaBandeja);
            Assert.True(padrao.LoginAutomatico);

            // Modificação e persistência
            store.Salvar(new PreferenciasUi(
                MinimizarParaBandeja: true,
                FecharParaBandeja: true,
                IniciarMinimizadoNaBandeja: false,
                LoginAutomatico: false));

            var recarregado = new PreferenciasUiStore(arq).Carregar();
            Assert.True(recarregado.MinimizarParaBandeja);
            Assert.True(recarregado.FecharParaBandeja);
            Assert.False(recarregado.IniciarMinimizadoNaBandeja);
            Assert.False(recarregado.LoginAutomatico);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void CredenciaisLembradasStore_salva_cifrado_por_dpapi_e_recupera_com_seguranca()
    {
        var pasta = PastaTemp();
        try
        {
            var arqEmail = Path.Combine(pasta, "ultimo_email.txt");
            var arqSenha = Path.Combine(pasta, "senha_lembrada.bin");
            var store = new CredenciaisLembradasStore(arqEmail, arqSenha);

            Assert.False(store.TemCredenciais());
            var (vazioEmail, vazioSenha) = store.Carregar();
            Assert.Null(vazioEmail);
            Assert.Null(vazioSenha);

            const string email = "usuario@sevenzin.dev";
            const string senha = "SuperSenhaForte#2026!";

            store.Salvar(email, senha);

            Assert.True(store.TemCredenciais());
            Assert.True(File.Exists(arqEmail));
            Assert.True(File.Exists(arqSenha));

            // Garante que a senha NUNCA está em texto puro no arquivo (deve estar cifrada via DPAPI)
            var bytesSenha = File.ReadAllBytes(arqSenha);
            var textoBrutoSenha = Encoding.UTF8.GetString(bytesSenha);
            Assert.DoesNotContain(senha, textoBrutoSenha);

            // Recupera e valida
            var (emailCarregado, senhaCarregada) = store.Carregar();
            Assert.Equal(email, emailCarregado);
            Assert.Equal(senha, senhaCarregada);

            // Limpeza
            store.Limpar();
            Assert.False(store.TemCredenciais());
            Assert.False(File.Exists(arqEmail));
            Assert.False(File.Exists(arqSenha));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Theory]
    [InlineData("--autostart", true)]
    [InlineData("--tray", true)]
    [InlineData("--minimized", true)]
    [InlineData("--AUTOSTART", true)]
    [InlineData("--normal", false)]
    [InlineData("", false)]
    public void StartupHelper_detecta_flags_de_inicializacao_na_bandeja(string argumento, bool esperado)
    {
        var args = string.IsNullOrEmpty(argumento) ? Array.Empty<string>() : new[] { argumento };
        bool resultado = StartupHelper.DeveIniciarMinimizado(args, prefIniciarMinimizado: false);
        Assert.Equal(esperado, resultado);
    }
}
