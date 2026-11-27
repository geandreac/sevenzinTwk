using System.Text.Json.Nodes;

namespace Sevenzin.DiscordBot;

/// <summary>Um embed do Discord: título e texto (markdown do Discord).</summary>
public sealed record Embed(string Titulo, string Texto);

/// <summary>Regras puras do bot (sem rede), testadas em tests/Sevenzin.Tests.</summary>
public static class Mensagens
{
    public const int CorMarca = 0x2F8CFF;
    const int MaxTitulo = 256, MaxTexto = 4096, MaxEmbedsTotal = 6000;

    /// <summary>Lê um arquivo de canal: blocos separados por uma linha "===", cada um começando com "# Título".
    /// Bloco sem título vira embed sem título. Erra se passar dos limites do Discord (melhor falhar aqui do que
    /// publicar cortado).</summary>
    public static List<Embed> LerArquivo(string conteudo)
    {
        var blocos = conteudo.Replace("\r\n", "\n").Split("\n===\n");
        var embeds = new List<Embed>();
        foreach (var bloco in blocos)
        {
            var b = bloco.Trim('\n', ' ');
            if (b.Length == 0) continue;
            string titulo = "", texto = b;
            if (b.StartsWith("# "))
            {
                var fim = b.IndexOf('\n');
                titulo = (fim < 0 ? b[2..] : b[2..fim]).Trim();
                texto = fim < 0 ? "" : b[(fim + 1)..].Trim('\n');
            }
            if (titulo.Length > MaxTitulo) throw new InvalidOperationException($"Título com {titulo.Length} caracteres (máx. {MaxTitulo}).");
            if (texto.Length > MaxTexto) throw new InvalidOperationException($"Bloco \"{titulo}\" com {texto.Length} caracteres (máx. {MaxTexto}).");
            embeds.Add(new Embed(titulo, texto));
        }
        if (embeds.Count is 0 or > 10) throw new InvalidOperationException($"{embeds.Count} blocos (precisa de 1 a 10).");
        var total = embeds.Sum(e => e.Titulo.Length + e.Texto.Length);
        if (total > MaxEmbedsTotal) throw new InvalidOperationException($"Mensagem com {total} caracteres (máx. {MaxEmbedsTotal} somando os blocos).");
        return embeds;
    }

    /// <summary>Corpo JSON da mensagem (sem menções automáticas: o bot só menciona o que pedirmos).</summary>
    public static JsonObject Corpo(IEnumerable<Embed> embeds, string? conteudo = null, string? cargoMencionado = null)
    {
        var lista = new JsonArray();
        foreach (var e in embeds)
        {
            var o = new JsonObject { ["description"] = e.Texto, ["color"] = CorMarca };
            if (e.Titulo.Length > 0) o["title"] = e.Titulo;
            lista.Add(o);
        }
        var corpo = new JsonObject
        {
            ["embeds"] = lista,
            ["allowed_mentions"] = new JsonObject
            {
                ["parse"] = new JsonArray(),
                ["roles"] = cargoMencionado is null ? new JsonArray() : new JsonArray(cargoMencionado),
            },
        };
        if (conteudo is not null) corpo["content"] = conteudo;
        return corpo;
    }

    /// <summary>A mensagem oficial do bot num canal: a mais antiga dele entre as recentes (a que foi fixada primeiro).
    /// Lista vem do Discord da mais nova para a mais antiga. Só conta mensagem comum (type 0): avisos de sistema como
    /// "o bot entrou no servidor" aparecem com o bot como autor, mas não podem ser editados.</summary>
    public static string? MensagemDoBot(JsonArray mensagens, string botId) =>
        mensagens.LastOrDefault(m => m?["author"]?["id"]?.GetValue<string>() == botId
            && (m["type"]?.GetValue<int>() ?? 0) == 0)?["id"]?.GetValue<string>();

    /// <summary>Embed de uma release do GitHub. Notas longas são cortadas com link para a página.</summary>
    public static Embed Release(string tag, string? nome, string? notas, string url)
    {
        var titulo = $"🚀 {(string.IsNullOrWhiteSpace(nome) ? tag : nome)}";
        var rodape = $"\n\n**Baixar:** {url}\nO app também avisa e se atualiza sozinho.";
        var corpo = (notas ?? "").Replace("\r\n", "\n").Trim();
        var limite = MaxTexto - rodape.Length;
        if (corpo.Length > limite) corpo = corpo[..(limite - 1)] + "…";
        return new Embed(titulo.Length > MaxTitulo ? titulo[..MaxTitulo] : titulo, corpo + rodape);
    }

    /// <summary>Esta versão já foi anunciada? Procura a tag nos títulos dos embeds das mensagens do bot.</summary>
    public static bool ReleaseJaPublicada(JsonArray mensagens, string botId, string tag, string? nome)
    {
        var alvo = $"🚀 {(string.IsNullOrWhiteSpace(nome) ? tag : nome)}";
        return mensagens.Any(m => m?["author"]?["id"]?.GetValue<string>() == botId
            && (m["embeds"] as JsonArray)?.Any(e => e?["title"]?.GetValue<string>() == alvo) == true);
    }
}
