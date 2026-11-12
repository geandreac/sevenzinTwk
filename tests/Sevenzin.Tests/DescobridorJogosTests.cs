using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Detecção de jogos instalados (Steam/Epic): parsers puros dos formatos reais e integração
/// com estruturas sintéticas em pasta temporária — launcher ausente = lista vazia, sem exceção.</summary>
public class DescobridorJogosTests : IDisposable
{
    readonly string _raiz = Path.Combine(Path.GetTempPath(), "sevenzin-steam-" + Guid.NewGuid().ToString("N"));

    public DescobridorJogosTests() => Directory.CreateDirectory(_raiz);

    public void Dispose() { try { Directory.Delete(_raiz, recursive: true); } catch { } }

    [Fact]
    public void LibraryFolders_parseia_os_caminhos_com_barras_escapadas()
    {
        const string vdf = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"E:\\SteamLibrary"
        		"label"		""
        	}
        	"1"
        	{
        		"path"		"D:\\Games\\Steam"
        	}
        }
        """;
        var pastas = DescobridorJogos.AnalisarLibraryFolders(vdf);
        Assert.Equal(new[] { @"E:\SteamLibrary", @"D:\Games\Steam" }, pastas);
    }

    [Fact]
    public void AppManifest_parseia_appid_nome_e_installdir()
    {
        const string acf = """
        "AppState"
        {
        	"appid"		"730"
        	"name"		"Counter-Strike 2"
        	"installdir"		"Counter-Strike Global Offensive"
        }
        """;
        var m = DescobridorJogos.AnalisarAppManifest(acf);
        Assert.NotNull(m);
        Assert.Equal("730", m!.Value.AppId);
        Assert.Equal("Counter-Strike 2", m.Value.Nome);
        Assert.Equal("Counter-Strike Global Offensive", m.Value.Installdir);

        Assert.Null(DescobridorJogos.AnalisarAppManifest("sem campos"));
    }

    [Fact]
    public void ManifestoEpic_parseia_nome_instalacao_e_executavel_e_recusa_json_invalido()
    {
        const string item = """
        {"FormatVersion":0,"AppName":"Fortnite","DisplayName":"Fortnite","InstallLocation":"C:\\Epic\\Fortnite","LaunchExecutable":"FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping.exe"}
        """;
        var m = DescobridorJogos.AnalisarManifestoEpic(item);
        Assert.NotNull(m);
        Assert.Equal("Fortnite", m!.Value.Nome);
        Assert.Equal(@"C:\Epic\Fortnite", m.Value.InstallLocation);
        Assert.Equal("FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping.exe", m.Value.Executavel);

        Assert.Null(DescobridorJogos.AnalisarManifestoEpic("{ não é json"));
        Assert.Null(DescobridorJogos.AnalisarManifestoEpic("""{"AppName":"x"}""")); // sem DisplayName
    }

    [Fact]
    public void ExecutavelPrincipal_prefere_nome_da_pasta_depois_o_maior_e_ignora_lixo()
    {
        var pasta = Path.Combine(_raiz, "MeuJogo");
        Directory.CreateDirectory(pasta);
        File.WriteAllBytes(Path.Combine(pasta, "unins000.exe"), new byte[1000]);   // desinstalador: ignorado
        File.WriteAllBytes(Path.Combine(pasta, "vcredist_x64.exe"), new byte[900]); // redist: ignorado
        File.WriteAllBytes(Path.Combine(pasta, "Launcher.exe"), new byte[500]);     // candidato menor

        // sem o .exe exato → maior candidato válido
        Assert.Equal(Path.Combine(pasta, "Launcher.exe"), DescobridorJogos.ExecutavelPrincipal(pasta, "MeuJogo"));

        // com o .exe exato → ele vence, mesmo menor
        File.WriteAllBytes(Path.Combine(pasta, "MeuJogo.exe"), new byte[10]);
        Assert.Equal(Path.Combine(pasta, "MeuJogo.exe"), DescobridorJogos.ExecutavelPrincipal(pasta, "MeuJogo"));

        // pasta vazia/sem candidato → null
        Assert.Null(DescobridorJogos.ExecutavelPrincipal(Path.Combine(_raiz, "NaoExiste"), "x"));
        var vazia = Path.Combine(_raiz, "Vazia");
        Directory.CreateDirectory(vazia);
        Assert.Null(DescobridorJogos.ExecutavelPrincipal(vazia, "Vazia"));
    }

    [Fact]
    public void DetectarSteam_com_estrutura_real_devolve_os_jogos_e_sem_launcher_lista_vazia()
    {
        // Estrutura sintética: biblioteca + manifestos + pasta common com exe
        var steam = Path.Combine(_raiz, "Steam");
        var steamapps = Path.Combine(steam, "steamapps");
        var common = Path.Combine(steamapps, "common");
        Directory.CreateDirectory(common);
        Directory.CreateDirectory(Path.Combine(common, "Meu Jogo"));

        File.WriteAllText(Path.Combine(steamapps, "libraryfolders.vdf"),
            $"\"libraryfolders\"{{\"0\"{{\"path\"\t\"{steam.Replace("\\", "\\\\")}\"}}}}");
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_100.acf"),
            "\"AppState\"{\"appid\"\t\"100\"\n\"name\"\t\"Meu Jogo\"\n\"installdir\"\t\"Meu Jogo\"}");
        File.WriteAllBytes(Path.Combine(common, "Meu Jogo", "Meu Jogo.exe"), new byte[64]);

        var jogos = DescobridorJogos.DetectarSteam(steam);
        Assert.Single(jogos);
        Assert.Equal("Steam", jogos[0].Plataforma);
        Assert.Equal("Meu Jogo", jogos[0].Nome);
        Assert.Equal("100", jogos[0].AppId);
        Assert.Equal(Path.Combine(common, "Meu Jogo", "Meu Jogo.exe"), jogos[0].Executavel);

        Assert.Empty(DescobridorJogos.DetectarSteam(Path.Combine(_raiz, "SemSteam"))); // launcher ausente
    }

    [Fact]
    public void DetectarEpic_com_manifests_devolve_os_jogos_e_sem_launcher_lista_vazia()
    {
        var manifests = Path.Combine(_raiz, "Manifests");
        Directory.CreateDirectory(manifests);
        var instalacao = Path.Combine(_raiz, "Fortnite");
        Directory.CreateDirectory(instalacao);
        File.WriteAllBytes(Path.Combine(instalacao, "FortniteClient.exe"), new byte[64]);
        File.WriteAllText(Path.Combine(manifests, "x.item"),
            $"{{\"AppName\":\"Fortnite\",\"DisplayName\":\"Fortnite\",\"InstallLocation\":\"{instalacao.Replace("\\", "\\\\")}\",\"LaunchExecutable\":\"FortniteClient.exe\"}}");
        File.WriteAllText(Path.Combine(manifests, "y.item"), "{ inválido"); // ignorado

        var jogos = DescobridorJogos.DetectarEpic(manifests);
        Assert.Single(jogos);
        Assert.Equal("Epic", jogos[0].Plataforma);
        Assert.Equal("Fortnite", jogos[0].Nome);
        Assert.Equal(Path.Combine(instalacao, "FortniteClient.exe"), jogos[0].Executavel);

        Assert.Empty(DescobridorJogos.DetectarEpic(Path.Combine(_raiz, "SemEpic"))); // launcher ausente
    }
}
