using System.Security.Cryptography;
using System.Text.Json;
using Sevenzin.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sevenzin.Tests;

public class ReiniciarParaAtualizarTests(ITestOutputHelper saida)
{
    [Fact]
    public void Escolhe_so_o_setup_e_le_o_sha256()
    {
        var hash = new string('a', 64);
        var json = JsonDocument.Parse($$"""
        {"assets":[
          {"name":"X-Admin-setup.exe","browser_download_url":"u0","digest":"sha256:00"},
          {"name":"X-portable.zip","browser_download_url":"u1"},
          {"name":"X-setup.exe","browser_download_url":"u2","digest":"sha256:{{hash}}"}]}
        """).RootElement;
        Assert.Equal(("u2", "X-setup.exe", hash), VerificadorAtualizacoes.SelecionarInstaladorRelease(json));
        var semDigest = JsonDocument.Parse("""{"assets":[{"name":"X-setup.exe","browser_download_url":"u"}]}""").RootElement;
        Assert.Null(VerificadorAtualizacoes.SelecionarInstaladorRelease(semDigest).Sha256);
    }

    [Theory]
    [InlineData("https://github.com/geandreac/sevenzinTwk/releases/download/v2.0.6/a-setup.exe", true)]
    [InlineData("http://github.com/geandreac/sevenzinTwk/releases/download/v2.0.6/a.exe", false)]
    [InlineData("https://github.com/outro/repo/releases/download/v1/a.exe", false)]
    [InlineData("https://github.com/geandreac/sevenzinTwk/releases/download/../../../../outro/a.exe", false)]
    [InlineData("https://github.com.evil.com/geandreac/sevenzinTwk/releases/download/a.exe", false)]
    [InlineData(null, false)]
    public void So_aceita_releases_deste_repositorio(string? url, bool ok) => Assert.Equal(ok, AtualizadorApp.UrlPermitida(url));

    [Fact]
    public void Sha256_e_script_conferem_o_arquivo_antes_de_instalar()
    {
        var f = Path.GetTempFileName();
        try
        {
            File.WriteAllText(f, "setup");
            var h = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
            Assert.True(AtualizadorApp.Sha256Confere(f, h.ToLowerInvariant()));
            Assert.False(AtualizadorApp.Sha256Confere(f, new string('0', 64)));
            Assert.False(AtualizadorApp.Sha256Confere(f, null));
            Assert.False(AtualizadorApp.IniciarAtualizacaoEReiniciar(f, new string('0', 64)));
            var s = AtualizadorApp.MontarScriptAtualizacaoPowerShell(@"C:\d'Ana\s.exe", h, null, 42);
            Assert.Contains(@"C:\d''Ana\s.exe", s);
            Assert.Contains(h, s);
            Assert.True(s.IndexOf("Get-FileHash", StringComparison.Ordinal) < s.IndexOf("/SILENT", StringComparison.Ordinal));
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Pos_reinicio_so_e_consumido_depois_do_boot_e_uma_vez()
    {
        var f = Path.Combine(Path.GetTempPath(), "sevenzin_pd_" + Guid.NewGuid().ToString("N") + ".json");
        var s = new PosDebloatNvidiaStore(f);
        var agora = DateTime.UtcNow;
        s.SalvarPendente(new("fps", "RTX", agora));
        Assert.Null(s.ConsumirPendente(agora.AddHours(-1))); // ainda não reiniciou
        Assert.True(s.TemPendente());
        Assert.Equal("fps", s.ConsumirPendente(agora.AddMinutes(1))!.PerfilId);
        Assert.Null(s.ConsumirPendente(agora.AddMinutes(1)));
        s.SalvarPendente(new("fps", "RTX", agora.AddHours(-30)));
        Assert.Null(s.ConsumirPendente(agora)); // velha demais
        Assert.False(s.TemPendente());
    }

    [Fact]
    public void Perfil_casual_respeita_notebook()
    {
        static uint Energia(string gpu) => NvidiaDrs.ObterParametrosPerfil("casual",
            new InfoGpu(gpu, null, null, null, null, FabricanteGpu.Nvidia, null, false)).First(p => p.SettingId == NvidiaDrs.IdPowerManagementMode).Valor;
        Assert.Equal(1u, Energia("NVIDIA GeForce RTX 4060 Ti"));
        Assert.Equal(0u, Energia("NVIDIA GeForce RTX 4060 Laptop GPU"));
    }

    // Só leitura no driver real; em máquina sem NVIDIA não verifica nada.
    [Fact]
    public void Leitura_do_base_profile_real_nao_falha()
    {
        var id = GpuDriverTweaks.LerEstadoPerfil().PerfilId ?? "fps2";
        var v = NvidiaDrs.VerificarBaseProfile(id);
        if (!v.Disponivel) return;
        Assert.True(v.TotalObrigatorios > 0, v.Resumo);
        saida.WriteLine($"{id}: {v.Resumo} | {NvidiaDrs.ResumoEstadoRealBaseProfile()}");
        foreach (var p in NvidiaDrs.ObterParametrosPerfil(id))
            saida.WriteLine($"{p.Nome}: esperado {p.Valor:X} real {NvidiaDrs.LerValorBaseProfile(p.SettingId):X} opcional={p.OpcionalNoDriver}");
    }
}
