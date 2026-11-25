using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Resultado da verificação de atualização. VersaoRemota/Url/UrlInstalador só fazem sentido quando a release existe.</summary>
public record ResultadoAtualizacao(
    bool HaNova,
    string VersaoAtual,
    string VersaoRemota,
    string Url,
    string? Notas,
    string? UrlInstalador = null,
    string? NomeInstalador = null,
    string? Sha256 = null);

/// <summary>Verifica a release mais recente do repositório no GitHub e compara com a versão do app.
/// Falha/offline/sem release = retorna null em silêncio, nunca trava o app.</summary>
public static class VerificadorAtualizacoes
{
    public const string UrlReleaseMaisRecente = "https://api.github.com/repos/geandreac/sevenzinTwk/releases/latest";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Extrai a versão de uma tag ("v1.2.3", "1.2.3", "1.2", "1.0.0-beta"). Null se não parsear.</summary>
    public static Version? VersaoDaTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.Trim().TrimStart('v', 'V');
        int corte = s.IndexOfAny(new[] { '-', '+' }); // descarta sufixo pré-release/build (ex.: "-beta")
        if (corte >= 0) s = s[..corte];
        return Version.TryParse(s, out var v) ? Normaliza(v) : null;
    }

    /// <summary>Compara normalizando a 3 componentes: assembly vem como 4 partes (1.0.0.0) e a tag como
    /// 3 (1.0.0); sem normalizar, Version trata a parte ausente como -1 e 1.0.0 ficaria "menor" que 1.0.0.0.</summary>
    public static bool EhMaisNova(Version atual, Version remota) => Normaliza(remota) > Normaliza(atual);

    static Version Normaliza(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    /// <summary>Seleciona o asset instalador (`*-setup.exe`, excluindo `Admin`) do JSON da release do GitHub.</summary>
    public static (string? Url, string? Nome, string? Sha256) SelecionarInstaladorRelease(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return (null, null, null);

        foreach (var asset in assets.EnumerateArray())
        {
            var nome = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(url)) continue;
            if (nome.Contains("admin", StringComparison.OrdinalIgnoreCase)) continue;

            if (nome.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
                return (url, nome, digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest[7..] : null);
        }
        return (null, null, null);
    }

    public static async Task<ResultadoAtualizacao?> VerificarAsync(Version atual, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, UrlReleaseMaisRecente);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("SevenzinTwks-Updater", "1.0")); // GitHub exige User-Agent
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null; // 404 = ainda sem releases; 403/offline = silencioso
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var remota = VersaoDaTag(tag);
            if (remota is null) return null;
            var url = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            var notas = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            var (urlInst, nomeInst, sha) = SelecionarInstaladorRelease(root);
            return new ResultadoAtualizacao(EhMaisNova(atual, remota), Normaliza(atual).ToString(3), remota.ToString(3), url ?? "", notas, urlInst, nomeInst, sha);
        }
        catch { return null; } // sem internet, timeout, JSON inesperado: não incomoda o usuário
    }
}
