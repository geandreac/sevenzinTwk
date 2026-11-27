using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sevenzin.DiscordBot;

// Bot SevenzinTwks: publica a mensagem oficial de cada canal (docs/discord/*.md) e anuncia releases do GitHub.
// Só usa a API REST do Discord: não fica conectado, roda e termina. O token vem da variável de ambiente
// SEVENZIN_DISCORD_TOKEN e nunca é gravado em arquivo nem impresso.
//
//   sevenzin-bot publicar [chave...]   posta ou EDITA a mensagem oficial (todos os canais ou só os informados)
//   sevenzin-bot release               anuncia em changelog a release mais recente, se ainda não anunciada
//   sevenzin-bot verificar             só valida os arquivos (não precisa de token nem de internet)

var pasta = Path.GetFullPath(Environment.GetEnvironmentVariable("SEVENZIN_DISCORD_DOCS")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "discord"));
var manifesto = JsonNode.Parse(File.ReadAllText(Path.Combine(pasta, "canais.json")))!;
var comando = args.FirstOrDefault() ?? "verificar";

if (comando == "verificar")
{
    foreach (var c in manifesto["canais"]!.AsArray())
    {
        var embeds = Mensagens.LerArquivo(File.ReadAllText(Path.Combine(pasta, c!["arquivo"]!.GetValue<string>())));
        Console.WriteLine($"ok  {c["chave"],-11} {embeds.Count} bloco(s), {embeds.Sum(e => e.Titulo.Length + e.Texto.Length)} caracteres");
    }
    return 0;
}

var token = Environment.GetEnvironmentVariable("SEVENZIN_DISCORD_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Defina o token do bot: setx SEVENZIN_DISCORD_TOKEN \"<token>\" e abra um terminal novo.");
    return 2;
}

using var http = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10/") };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token.Trim());
http.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot (https://github.com/geandreac/sevenzinTwk, 1.0)");

var botId = (await Api(HttpMethod.Get, "users/@me"))!["id"]!.GetValue<string>();
var servidor = manifesto["servidor"]!.GetValue<string>();

if (comando == "publicar")
{
    var filtro = args.Skip(1).ToHashSet();
    foreach (var c in manifesto["canais"]!.AsArray())
    {
        var chave = c!["chave"]!.GetValue<string>();
        if (filtro.Count > 0 && !filtro.Contains(chave)) continue;
        var canal = c["canal"]!.GetValue<string>();
        var embeds = Mensagens.LerArquivo(File.ReadAllText(Path.Combine(pasta, c["arquivo"]!.GetValue<string>())));
        var corpo = Mensagens.Corpo(embeds);

        if (c["forum"]?.GetValue<string>() is { } tituloPost)
        {
            // Fórum: a mensagem oficial é a primeira de um post do bot (id do post = id da mensagem inicial).
            var ativos = (await Api(HttpMethod.Get, $"guilds/{servidor}/threads/active"))!["threads"]!.AsArray();
            var post = ativos.FirstOrDefault(t => t?["parent_id"]?.GetValue<string>() == canal && t?["owner_id"]?.GetValue<string>() == botId);
            if (post is not null)
            {
                var id = post["id"]!.GetValue<string>();
                await Api(HttpMethod.Patch, $"channels/{id}/messages/{id}", corpo);
                Console.WriteLine($"editado  {chave} (post do fórum)");
            }
            else
            {
                await Api(HttpMethod.Post, $"channels/{canal}/threads", new JsonObject { ["name"] = tituloPost, ["message"] = corpo });
                Console.WriteLine($"criado   {chave} (post do fórum)");
            }
            continue;
        }

        var recentes = (await Api(HttpMethod.Get, $"channels/{canal}/messages?limit=50"))!.AsArray();
        if (Mensagens.MensagemDoBot(recentes, botId) is { } existente)
        {
            await Api(HttpMethod.Patch, $"channels/{canal}/messages/{existente}", corpo);
            Console.WriteLine($"editado  {chave}");
        }
        else
        {
            var nova = (await Api(HttpMethod.Post, $"channels/{canal}/messages", corpo))!["id"]!.GetValue<string>();
            await Api(HttpMethod.Put, $"channels/{canal}/pins/{nova}");
            Console.WriteLine($"criado   {chave} (fixado)");
        }
        // Tira o aviso "SevenzinTwks fixou uma mensagem" (type 6) que o Discord deixa no canal a cada fixação.
        foreach (var m in (await Api(HttpMethod.Get, $"channels/{canal}/messages?limit=20"))!.AsArray())
            if (m?["type"]?.GetValue<int>() == 6 && m["author"]?["id"]?.GetValue<string>() == botId)
                await Api(HttpMethod.Delete, $"channels/{canal}/messages/{m["id"]!.GetValue<string>()}");
    }
    return 0;
}

if (comando == "release")
{
    var repo = manifesto["repositorio"]!.GetValue<string>();
    using var gh = new HttpClient();
    gh.DefaultRequestHeaders.UserAgent.ParseAdd("sevenzin-bot");
    var rel = JsonNode.Parse(await gh.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest"))!;
    var tag = rel["tag_name"]!.GetValue<string>();
    var nome = rel["name"]?.GetValue<string>();
    var canal = manifesto["changelog"]!.GetValue<string>();

    var recentes = (await Api(HttpMethod.Get, $"channels/{canal}/messages?limit=50"))!.AsArray();
    if (Mensagens.ReleaseJaPublicada(recentes, botId, tag, nome)) { Console.WriteLine($"{tag} já anunciada."); return 0; }

    var nomeCargo = manifesto["cargoAvisosVersao"]?.GetValue<string>();
    var cargo = (await Api(HttpMethod.Get, $"guilds/{servidor}/roles"))!.AsArray()
        .FirstOrDefault(r => r?["name"]?.GetValue<string>() == nomeCargo)?["id"]?.GetValue<string>();
    var embed = Mensagens.Release(tag, nome, rel["body"]?.GetValue<string>(), rel["html_url"]!.GetValue<string>());
    await Api(HttpMethod.Post, $"channels/{canal}/messages",
        Mensagens.Corpo(new[] { embed }, cargo is null ? null : $"<@&{cargo}>", cargo));
    Console.WriteLine($"{tag} anunciada em changelog.");
    return 0;
}

Console.Error.WriteLine("Comandos: publicar [chave...] | release | verificar");
return 1;

async Task<JsonNode?> Api(HttpMethod metodo, string caminho, JsonNode? corpo = null)
{
    for (var tentativa = 0; ; tentativa++)
    {
        using var req = new HttpRequestMessage(metodo, caminho);
        if (corpo is not null) req.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        if (resp.StatusCode == (HttpStatusCode)429 && tentativa < 5)
        {
            var espera = JsonNode.Parse(texto)?["retry_after"]?.GetValue<double>() ?? 1;
            await Task.Delay(TimeSpan.FromSeconds(espera + 0.2));
            continue;
        }
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"{metodo} {caminho.Split('?')[0]} → {(int)resp.StatusCode}: {texto}");
        return texto.Length == 0 ? null : JsonNode.Parse(texto);
    }
}
