using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Sevenzin.DiscordBot;
using Xunit;

namespace Sevenzin.Tests;

public class DiscordBotTests
{
    [Fact]
    public void Arquivo_com_blocos_vira_um_embed_por_bloco_com_titulo()
    {
        var e = Mensagens.LerArquivo("# Um\nlinha 1\nlinha 2\n===\n# Dois\ntexto\n");
        Assert.Equal(2, e.Count);
        Assert.Equal(new Embed("Um", "linha 1\nlinha 2"), e[0]);
        Assert.Equal(new Embed("Dois", "texto"), e[1]);
    }

    [Fact]
    public void Texto_acima_do_limite_do_discord_falha_em_vez_de_publicar_cortado() =>
        Assert.Throws<System.InvalidOperationException>(() => Mensagens.LerArquivo("# T\n" + new string('x', 4097)));

    [Fact]
    public void Mensagem_oficial_e_a_mais_antiga_do_bot_entre_as_recentes()
    {
        var lista = JsonNode.Parse("""
            [{"id":"3","author":{"id":"bot"}},{"id":"2","author":{"id":"pessoa"}},{"id":"1","author":{"id":"bot"}},{"id":"0","type":7,"author":{"id":"bot"}}]
            """)!.AsArray();
        Assert.Equal("1", Mensagens.MensagemDoBot(lista, "bot"));
        Assert.Null(Mensagens.MensagemDoBot(lista, "outro"));
    }

    [Fact]
    public void Corpo_nao_menciona_ninguem_sem_pedir()
    {
        var c = Mensagens.Corpo(new[] { new Embed("T", "x") });
        Assert.Empty(c["allowed_mentions"]!["parse"]!.AsArray());
        Assert.Empty(c["allowed_mentions"]!["roles"]!.AsArray());
        Assert.Equal("1", Mensagens.Corpo(new[] { new Embed("T", "x") }, "<@&1>", "1")["allowed_mentions"]!["roles"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Release_corta_notas_longas_e_nao_repete_anuncio()
    {
        var e = Mensagens.Release("v2.2.1", "SevenzinTwks 2.2.1", new string('a', 5000), "https://x");
        Assert.True(e.Texto.Length <= 4096);
        Assert.EndsWith("se atualiza sozinho.", e.Texto);
        var lista = JsonNode.Parse("""[{"author":{"id":"bot"},"embeds":[{"title":"🚀 SevenzinTwks 2.2.1"}]}]""")!.AsArray();
        Assert.True(Mensagens.ReleaseJaPublicada(lista, "bot", "v2.2.1", "SevenzinTwks 2.2.1"));
        Assert.False(Mensagens.ReleaseJaPublicada(lista, "bot", "v2.2.2", "SevenzinTwks 2.2.2"));
    }

    [Fact]
    public void Todos_os_textos_dos_canais_cabem_nos_limites_do_discord()
    {
        var pasta = Path.Combine(Raiz(), "docs", "discord");
        var canais = JsonNode.Parse(File.ReadAllText(Path.Combine(pasta, "canais.json")))!["canais"]!.AsArray();
        Assert.Equal(21, canais.Count);
        foreach (var c in canais)
            Mensagens.LerArquivo(File.ReadAllText(Path.Combine(pasta, c!["arquivo"]!.GetValue<string>())));
    }

    static string Raiz()
    {
        var d = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Sevenzin.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
