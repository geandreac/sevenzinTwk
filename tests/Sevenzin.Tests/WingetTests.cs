using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F3: allowlist winget (literal do original) e catálogo ampliado com os nomes confirmados
/// na engenharia reversa — fontes oficiais, sem inventar URLs/hashes que são lacuna no original.</summary>
public class WingetTests
{
    [Fact]
    public void Allowlist_e_exatamente_a_confirmada_no_original()
    {
        Assert.Equal(5, WingetAllowlist.Itens.Count);
        Assert.Equal("Valve.Steam", WingetAllowlist.Itens["steam"]);
        Assert.Equal("Discord.Discord", WingetAllowlist.Itens["discord"]);
        Assert.Equal("7zip.7zip", WingetAllowlist.Itens["7zip"]);
        Assert.Equal("Google.Chrome", WingetAllowlist.Itens["chrome"]);
        Assert.Equal("VideoLAN.VLC", WingetAllowlist.Itens["vlc"]);
    }

    [Fact]
    public void ArgumentosPara_reproduz_o_comando_literal_do_original()
    {
        Assert.Equal(
            "install --id Valve.Steam --exact --source winget --silent --accept-source-agreements --accept-package-agreements --disable-interactivity --no-upgrade",
            WingetRunner.ArgumentosPara("Valve.Steam"));
    }

    [Fact]
    public async Task Instalar_rejeita_id_fora_da_allowlist_sem_iniciar_processo()
    {
        var r = await WingetRunner.InstalarAsync("Hacker.Tools");
        Assert.False(r.Ok);
        Assert.False(r.JaInstalado);
        Assert.Contains("allowlist", r.Detalhe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalogo_cobre_os_nomes_confirmados_da_re()
    {
        var nomes = CatalogoUtilitarios.Todos.Select(u => u.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var esperado in new[]
        {
            "7-Zip", "Brave Browser", "Google Chrome", "Microsoft Edge", "DirectX End-User Runtime",
            "Epic Games Launcher", "Rockstar Games Launcher", "Ubisoft Connect", "VALORANT", "FACEIT",
            "Gamers Club", "TeamSpeak", "LatencyMon", "MSI Afterburner", "Logitech G HUB", "NVCleanstall",
            "OCAT", "Open-Shell", "StartAllBack", "Visual Studio Code", "Visual Studio", "DDU (Display Driver Uninstaller)",
            "Radeon Software Slimmer", "ZenTimings", "Prime95", "Geekbench", "OCCT", "DriverStore Explorer",
            "CPU-Z", "HWiNFO", "GPU-Z", "Process Explorer (Sysinternals)", "Autoruns (Sysinternals)",
        })
            Assert.Contains(esperado, nomes);
    }

    [Fact]
    public void Kms_activator_fica_excluido_por_decisao_registrada()
    {
        Assert.Contains("KMS Activator", CatalogoUtilitarios.Excluidos);
        Assert.DoesNotContain(CatalogoUtilitarios.Todos, u => u.Nome.Contains("KMS", StringComparison.OrdinalIgnoreCase));
    }
}
