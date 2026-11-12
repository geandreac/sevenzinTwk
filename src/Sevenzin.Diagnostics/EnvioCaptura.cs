using System.Net.Http;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Configuração de envio de capturas. Por padrão NÃO há webhook: o envio fica desligado
/// até o usuário digitar uma URL e confirmar cada envio — nunca automático, nunca em segundo plano.</summary>
public sealed record ConfigCaptura(string? WebhookUrl)
{
    public bool EnvioConfigurado => EnvioCaptura.UrlWebhookValida(WebhookUrl);
}

/// <summary>Persistência local da configuração de capturas (%LOCALAPPDATA%\Sevenzin\capturas-config.json).</summary>
public sealed class ConfigCapturasStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    readonly string _arquivo;

    public ConfigCapturasStore(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "capturas-config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
    }

    public ConfigCaptura Carregar()
    {
        try
        {
            return File.Exists(_arquivo) ? JsonSerializer.Deserialize<ConfigCaptura>(File.ReadAllText(_arquivo), Opts) ?? new ConfigCaptura(null) : new ConfigCaptura(null);
        }
        catch { return new ConfigCaptura(null); }
    }

    public void Salvar(ConfigCaptura c)
    {
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}

/// <summary>Envio EXPLÍCITO de captura para um webhook configurado pelo usuário. Só é chamado pela tela
/// quando o usuário clica "Enviar" e confirma — o padrão continua sendo salvar local.</summary>
public static class EnvioCaptura
{
    /// <summary>URL de webhook aceitável: https absoluta e não vazia. Função pura (testável).</summary>
    public static bool UrlWebhookValida(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(u.Host);

    /// <summary>POST multipart com o PNG. Devolve true em 2xx; false em falha de rede/servidor (mensagem
    /// no <paramref name="erro"/>). Nunca lança para fora.</summary>
    public static async Task<bool> EnviarAsync(byte[] png, string nomeArquivo, string url, HttpMessageHandler? handler = null, CancellationToken ct = default)
    {
        try
        {
            using var http = handler is null ? new HttpClient() : new HttpClient(handler);
            using var conteudo = new MultipartFormDataContent();
            using var bytes = new ByteArrayContent(png);
            bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            conteudo.Add(bytes, "captura", nomeArquivo);
            using var resp = await http.PostAsync(url, conteudo, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
