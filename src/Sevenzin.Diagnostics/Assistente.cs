using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Configuração do assistente. Sem endpoint configurado o envio fica DESLIGADO —
/// o padrão é não haver servidor nenhum (o endpoint do original NÃO é reaproveitado).</summary>
public sealed record ConfigAssistente(string? EndpointUrl, bool IncluirContextoPadrao = false)
{
    public bool EnvioConfigurado => Assistente.UrlEndpointValida(EndpointUrl);
}

/// <summary>Persistência local da configuração do assistente (%LOCALAPPDATA%\Sevenzin\assistente-config.json).</summary>
public sealed class ConfigAssistenteStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    readonly string _arquivo;

    public ConfigAssistenteStore(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "assistente-config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
    }

    public ConfigAssistente Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<ConfigAssistente>(File.ReadAllText(_arquivo), Opts) ?? new(null) : new(null); }
        catch { return new(null); }
    }

    public void Salvar(ConfigAssistente c)
    {
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}

/// <summary>Assistente por endpoint configurado pelo usuário (paridade com 04_funcionalidades §10 /
/// 06_fluxos F12 — ChatSend/ChatGet — sem reutilizar o servidor do original). Cada mensagem é enviada
/// SÓ quando o usuário aperta Enviar (ação explícita); o contexto do PC entra apenas com consentimento
/// por mensagem (IncludeInContext/ShareWithAi).</summary>
public static class Assistente
{
    /// <summary>Endpoint aceitável: HTTPS absoluto. Função pura (testável).</summary>
    public static bool UrlEndpointValida(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(u.Host);

    /// <summary>Contexto do PC para a mensagem (só dados do inventário local; nada de identificadores).
    /// Nunca inventa valores — "—" quando a leitura não existe.</summary>
    public static string ContextoPc(IReadOnlyList<ItemInventario> itens)
    {
        string Val(string nome) => itens.FirstOrDefault(i => i.Nome == nome)?.Valor ?? "—";
        return string.Join("\n", new[]
        {
            $"SO: {Val("Windows")}",
            $"CPU: {Val("Modelo")}",
            $"GPU: {Val("Adaptador")}",
            $"RAM: {Val("Capacidade total")}",
            $"Modo atual: {Val("Modo atual")}",
        });
    }

    /// <summary>Envia UMA mensagem (JSON {mensagem, contexto, utc}) e devolve o corpo da resposta como
    /// texto, ou null em falha de rede/servidor. Nunca lança para fora.</summary>
    public static async Task<string?> EnviarMensagemAsync(string mensagem, string? contexto, string url, HttpMessageHandler? handler = null, CancellationToken ct = default)
    {
        try
        {
            var corpo = JsonSerializer.Serialize(new { mensagem, contexto, utc = DateTime.UtcNow.ToString("o") });
            using var http = handler is null ? new HttpClient() : new HttpClient(handler);
            using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(url, conteudo, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var texto = (await resp.Content.ReadAsStringAsync(ct)).Trim();
            return texto.Length > 0 ? texto : null;
        }
        catch
        {
            return null;
        }
    }
}
