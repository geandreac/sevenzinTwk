using System.Net;
using System.Net.Http;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Envio explícito de captura (F2.3): validação pura de URL, persistência da configuração
/// e POST com handler fake (nunca rede real nos testes).</summary>
public class EnvioCapturaTests : IDisposable
{
    readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"sevenzin-cfg-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_arquivo); File.Delete(_arquivo + ".tmp"); } catch { }
    }

    [Fact]
    public void UrlWebhookValida_aceita_apenas_https_absoluta()
    {
        Assert.True(EnvioCaptura.UrlWebhookValida("https://hooks.example.com/abc"));
        Assert.False(EnvioCaptura.UrlWebhookValida(null));
        Assert.False(EnvioCaptura.UrlWebhookValida(""));
        Assert.False(EnvioCaptura.UrlWebhookValida("http://hooks.example.com/abc")); // sem TLS
        Assert.False(EnvioCaptura.UrlWebhookValida("ftp://x/abc"));
        Assert.False(EnvioCaptura.UrlWebhookValida("não é url"));
    }

    [Fact]
    public void Config_persiste_recarrega_e_tolera_arquivo_corrompido()
    {
        var store = new ConfigCapturasStore(_arquivo);
        Assert.False(store.Carregar().EnvioConfigurado); // padrão: sem webhook

        store.Salvar(new ConfigCaptura("https://hooks.example.com/abc"));
        var recarregado = new ConfigCapturasStore(_arquivo).Carregar();
        Assert.Equal("https://hooks.example.com/abc", recarregado.WebhookUrl);
        Assert.True(recarregado.EnvioConfigurado);

        File.WriteAllText(_arquivo, "{ inválido");
        Assert.False(new ConfigCapturasStore(_arquivo).Carregar().EnvioConfigurado); // corrompido → seguro (desligado)
    }

    sealed class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.NoContent;
        public string? UltimaUrl;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }

    [Fact]
    public async Task Enviar_faz_post_na_url_e_aceita_2xx()
    {
        var handler = new FakeHandler { Status = HttpStatusCode.NoContent };
        var ok = await EnvioCaptura.EnviarAsync(new byte[] { 1, 2, 3 }, "x.png", "https://hooks.example.com/abc", handler);
        Assert.True(ok);
        Assert.Equal("https://hooks.example.com/abc", handler.UltimaUrl);
    }

    [Fact]
    public async Task Enviar_devolve_false_em_erro_do_servidor_ou_rede()
    {
        var servidor500 = new FakeHandler { Status = HttpStatusCode.InternalServerError };
        Assert.False(await EnvioCaptura.EnviarAsync(new byte[] { 1 }, "x.png", "https://hooks.example.com/abc", servidor500));

        // URL inválida: falha sem exceção (a validação da UI usa UrlWebhookValida antes de chamar).
        Assert.False(await EnvioCaptura.EnviarAsync(new byte[] { 1 }, "x.png", "não é url"));
    }
}
