using System.Net;
using System.Net.Http;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F6: assistente por endpoint configurado pelo usuário — validação de URL, persistência da
/// configuração, contexto do PC (sem valores inventados) e POST com handler fake.</summary>
public class AssistenteTests : IDisposable
{
    readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"sevenzin-asst-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_arquivo); File.Delete(_arquivo + ".tmp"); } catch { }
    }

    [Fact]
    public void UrlEndpointValida_aceita_apenas_https_absoluta()
    {
        Assert.True(Assistente.UrlEndpointValida("https://meu-worker.exemplo.com/chat"));
        Assert.False(Assistente.UrlEndpointValida(null));
        Assert.False(Assistente.UrlEndpointValida(""));
        Assert.False(Assistente.UrlEndpointValida("http://x.com"));   // sem TLS
        Assert.False(Assistente.UrlEndpointValida("ftp://x.com"));
    }

    [Fact]
    public void Config_assistente_persiste_e_padrao_e_desligado()
    {
        var store = new ConfigAssistenteStore(_arquivo);
        Assert.False(store.Carregar().EnvioConfigurado); // padrão: sem endpoint → envio desligado

        store.Salvar(new ConfigAssistente("https://meu-worker.exemplo.com/chat", true));
        var lida = new ConfigAssistenteStore(_arquivo).Carregar();
        Assert.True(lida.EnvioConfigurado);
        Assert.True(lida.IncluirContextoPadrao);

        File.WriteAllText(_arquivo, "{x");
        Assert.False(new ConfigAssistenteStore(_arquivo).Carregar().EnvioConfigurado); // corrompido → desligado
    }

    [Fact]
    public void ContextoPc_monta_do_inventario_sem_inventar_valores()
    {
        var itens = new List<ItemInventario>
        {
            new() { Nome = "Windows", Valor = "Windows 11 Pro" },
            new() { Nome = "Modelo", Valor = "Ryzen 7 7800X3D" },
            new() { Nome = "Adaptador", Valor = "RTX 4070" },
            new() { Nome = "Capacidade total", Valor = "32 GB" },
        };

        var ctx = Assistente.ContextoPc(itens);

        Assert.Contains("Windows 11 Pro", ctx);
        Assert.Contains("Ryzen 7 7800X3D", ctx);
        Assert.Contains("RTX 4070", ctx);
        Assert.Contains("32 GB", ctx);
        Assert.Contains("—", ctx); // campo ausente (Modo atual) vira "—", nunca um valor forjado
    }

    sealed class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Corpo = "resposta do servidor";
        public string? UltimoJson;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimoJson = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status) { Content = new StringContent(Corpo) };
        }
    }

    [Fact]
    public async Task EnviarMensagem_manda_json_com_mensagem_e_contexto_e_devolve_o_corpo()
    {
        var handler = new FakeHandler();
        var resposta = await Assistente.EnviarMensagemAsync("olá", "SO: Windows 11", "https://x.com/chat", handler);

        Assert.Equal("resposta do servidor", resposta);
        Assert.NotNull(handler.UltimoJson);
        Assert.Contains("ol", handler.UltimoJson); // "olá" aparece escapado (\u00E1) no JSON
        Assert.Contains("SO: Windows 11", handler.UltimoJson);
    }

    [Fact]
    public async Task EnviarMensagem_devolve_null_em_falha_ou_resposta_vazia()
    {
        Assert.Null(await Assistente.EnviarMensagemAsync("x", null, "https://x.com/chat", new FakeHandler { Status = HttpStatusCode.InternalServerError }));
        Assert.Null(await Assistente.EnviarMensagemAsync("x", null, "https://x.com/chat", new FakeHandler { Corpo = "   " }));
        Assert.Null(await Assistente.EnviarMensagemAsync("x", null, "não é url")); // URL inválida falha sem exceção
    }
}
