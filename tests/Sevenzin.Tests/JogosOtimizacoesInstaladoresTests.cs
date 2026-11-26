using System.Diagnostics;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class JogosOtimizacoesInstaladoresTests
{
    sealed class RegistroFake : IRegistro
    {
        readonly Dictionary<(Colmeia, string, string), string> _str = new();
        readonly Dictionary<(Colmeia, string, string), int> _dw = new();
        public int? LerDword(Colmeia h, string c, string n) => _dw.TryGetValue((h, c, n), out var v) ? v : null;
        public void EscreverDword(Colmeia h, string c, string n, int v) => _dw[(h, c, n)] = v;
        public string? LerString(Colmeia h, string c, string n) => _str.TryGetValue((h, c, n), out var s) ? s : null;
        public void EscreverString(Colmeia h, string c, string n, string s) => _str[(h, c, n)] = s;
        public void ApagarValor(Colmeia h, string c, string n) { _str.Remove((h, c, n)); _dw.Remove((h, c, n)); }
    }

    [Fact]
    public void TopologiaCpu_calcula_mascaras_sem_core0_sem_smt_e_primeiro_ccd()
    {
        // Simula 4 núcleos físicos com SMT (2 threads por núcleo: 0b0011, 0b1100, 0b0011_0000, 0b1100_0000)
        var nucleos = new long[] { 0b0000_0011, 0b0000_1100, 0b0011_0000, 0b1100_0000 };
        const long todos = 0b1111_1111;

        Assert.Equal(0b1111_1100, TopologiaCpu.CalcularMascaraSemCore0(nucleos, todos));
        Assert.Equal(0b0101_0101, TopologiaCpu.CalcularMascaraSemSmt(nucleos, todos));

        // Simula 2 CCDs (domínios de cache L3: 0x0F e 0xF0)
        var ccds = new long[] { 0b0000_1111, 0b1111_0000 };
        Assert.Equal(0b0000_1111, TopologiaCpu.CalcularMascaraPrimeiroCcd(ccds, todos));

        // Composição combinada: 1º CCD (0b0000_1111) + Sem SMT (0b0101_0101) + Sem Core 0 (0b1111_1100) => 0b0000_0100
        var topo = new TopologiaCpu.Resultado(
            Hibrido: false, ECores: 0, PCores: 0, NucleosFisicos: 4, DominiosL3: 2,
            MascaraPCores: todos, MascaraECores: 0,
            MascaraSemCore0: 0b1111_1100, MascaraSemSmt: 0b0101_0101, MascaraPrimeiroCcd: 0b0000_1111);

        long combinada = TopologiaCpu.CombinarMascaraJogo(0, topo, excluirCore0: true, desativarSmt: true, apenasUmCcd: true, mascaraTodosOverride: todos);
        Assert.Equal(0b0000_0100, combinada);
    }

    [Fact]
    public void ControleProcessoWindows_aplica_e_restaura_modo_compatibilidade_fse_dpi()
    {
        var reg = new RegistroFake();
        var controle = new ControleProcessoWindows(registro: reg);
        var procAtual = Process.GetCurrentProcess().ProcessName + ".exe";

        // Caso 1: chave não existia antes -> deve remover ao reverter
        var jogo = new PerfilJogo("TesteCompat", procAtual, 0, PrioridadeJogo.Normal, ModoCompatibilidade: true);
        Assert.True(controle.Aplicar(jogo));
        Assert.Equal(AplicadorPerfil.ValorCompatFseDpi, reg.LerString(Colmeia.UsuarioAtual, AplicadorPerfil.ChaveCompatFlags, procAtual));
        Assert.True(controle.Reverter(procAtual));
        Assert.Null(reg.LerString(Colmeia.UsuarioAtual, AplicadorPerfil.ChaveCompatFlags, procAtual));

        // Caso 2: já havia valor anterior do usuário -> deve restaurar o valor exato ao reverter
        reg.EscreverString(Colmeia.UsuarioAtual, AplicadorPerfil.ChaveCompatFlags, procAtual, "~ RUNASADMIN");
        Assert.True(controle.Aplicar(jogo));
        Assert.Equal(AplicadorPerfil.ValorCompatFseDpi, reg.LerString(Colmeia.UsuarioAtual, AplicadorPerfil.ChaveCompatFlags, procAtual));
        Assert.True(controle.Reverter(procAtual));
        Assert.Equal("~ RUNASADMIN", reg.LerString(Colmeia.UsuarioAtual, AplicadorPerfil.ChaveCompatFlags, procAtual));
    }

    [Fact]
    public void IniciarExecutavelJogo_retorna_false_quando_arquivo_nao_existe()
    {
        bool ok = AplicadorPerfil.IniciarExecutavelJogo(@"Z:\caminho\inexistente\jogo_fantasma.exe", out var msg);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(msg));
    }

    [Fact]
    public void IndicesEnergiaOriginal_contem_134_ac_e_134_dc_literais()
    {
        Assert.Equal(134, IndicesEnergiaOriginal.Todos.Count);
        Assert.Equal(134, IndicesEnergiaOriginal.TodosDc.Count);
        Assert.Contains(("0012ee47-9041-4b5d-9b77-535fba8b1442", "6738e2c4-e8a5-4a42-b16a-e040e769756e", "1200"), IndicesEnergiaOriginal.TodosDc);
        Assert.All(IndicesEnergiaOriginal.TodosDc, t =>
        {
            Assert.Matches(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", t.Subgrupo);
            Assert.Matches(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", t.Configuracao);
            Assert.False(string.IsNullOrWhiteSpace(t.Valor));
        });
    }

    [Fact]
    public void Novos_tweaks_de_paridade_estao_presentes_com_valores_literais()
    {
        Tweak Obter(string id) => CatalogoTweaks.Todos.Single(t => t.Id == id);

        Assert.Equal(26, Obter("priority-separation-26").ValorOtimizado);
        Assert.Equal(2, Obter("gamedvr-fse-off").ValorOtimizado);
        Assert.Equal(0, Obter("gamedvr-policy-off").ValorOtimizado);
        Assert.Equal(1, Obter("onedrive-sync-off").ValorOtimizado);
        Assert.Equal(33554435, Obter("mitigacoes-mask").ValorOtimizado);
        Assert.True(Obter("mitigacoes-mask").ExigeConfirmacao);
        Assert.Equal(1, Obter("wu-drivers-off").ValorOtimizado);
        Assert.True(Obter("wu-drivers-off").ExigeConfirmacao);
    }

    [Fact]
    public void OtimizacoesSistema_analisa_mmagent_e_bloqueia_injecao_em_adaptador()
    {
        const string saida = """
            MemoryCompression    : False
            ApplicationPreLaunch : True
            """;
        var est = OtimizacoesSistema.AnalisarSaidaMmAgent(saida);
        Assert.Equal(false, est.CompressaoMemoria);
        Assert.Equal(true, est.PreLaunch);

        Assert.True(OtimizacoesSistema.NomeAdaptadorValido("Ethernet 2"));
        Assert.True(OtimizacoesSistema.NomeAdaptadorValido("Wi-Fi"));
        Assert.False(OtimizacoesSistema.NomeAdaptadorValido("Ethernet'; calc.exe; '"));
        Assert.False(OtimizacoesSistema.NomeAdaptadorValido("Wi-Fi | iex"));
        Assert.False(OtimizacoesSistema.NomeAdaptadorValido(""));
    }

    [Fact]
    public void CatalogoUtilitarios_atinge_69_itens_oficiais_https()
    {
        Assert.Equal(69, CatalogoUtilitarios.Todos.Count);
        var pastaIcones = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Sevenzin.Wpf", "Assets", "Apps"));

        Assert.All(CatalogoUtilitarios.Todos, u =>
        {
            Assert.False(string.IsNullOrWhiteSpace(u.UrlDownload));
            Assert.StartsWith("https://", u.UrlDownload!);
            Assert.False(string.IsNullOrWhiteSpace(u.Icone));
            Assert.True(File.Exists(Path.Combine(pastaIcones, u.Icone + ".png")), $"Ícone ausente: {u.Icone}.png para {u.Nome}");
        });

        var nomes = CatalogoUtilitarios.Todos.Select(u => u.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in new[]
        {
            "Steam", "Discord", "VLC Media Player", "Spotify", "Everything (voidtools)",
            "Revo Uninstaller", "Notepad++", "qBittorrent", "OBS Studio", "CapFrameX",
            "HWMonitor", "AMD Chipset Drivers", "Intel Wireless Bluetooth",
            "Microsoft Visual C++ Redistributable", "EA App", "Medal.tv",
            "Minecraft Launcher", "FiveM", "League of Legends",
        })
        {
            Assert.Contains(item, nomes);
        }
    }

    [Fact]
    public void ExtrairAssetGitHubRelease_prefere_instalador_exe_x64_e_ignora_assinaturas()
    {
        const string json = """
            {
              "assets": [
                { "name": "app-arm64.exe", "browser_download_url": "https://github.com/org/repo/releases/download/v1/app-arm64.exe" },
                { "name": "app-x64-setup.exe.sha256", "browser_download_url": "https://github.com/org/repo/releases/download/v1/app-x64-setup.exe.sha256" },
                { "name": "app-portable.zip", "browser_download_url": "https://github.com/org/repo/releases/download/v1/app-portable.zip" },
                { "name": "app-x64-setup.exe", "browser_download_url": "https://github.com/org/repo/releases/download/v1/app-x64-setup.exe" }
              ]
            }
            """;
        var r = DownloaderInstalador.ExtrairAssetGitHubRelease(json);
        Assert.NotNull(r);
        Assert.Equal("app-x64-setup.exe", r!.Value.NomeArquivo);
        Assert.Equal("https://github.com/org/repo/releases/download/v1/app-x64-setup.exe", r.Value.Url);
    }

    [Fact]
    public void ExtrairAssetGitHubRelease_nao_escolhe_ARM_quando_ha_x86_64()
    {
        // Formato real da release do RustDesk: aarch64 e x86_64 lado a lado.
        const string json = """
            { "assets": [
                { "name": "rustdesk-1.5.3-aarch64.exe", "browser_download_url": "https://github.com/rustdesk/rustdesk/releases/download/1.5.3/rustdesk-1.5.3-aarch64.exe" },
                { "name": "rustdesk-1.5.3-aarch64.msi", "browser_download_url": "https://github.com/rustdesk/rustdesk/releases/download/1.5.3/rustdesk-1.5.3-aarch64.msi" },
                { "name": "rustdesk-1.5.3-x86_64.exe", "browser_download_url": "https://github.com/rustdesk/rustdesk/releases/download/1.5.3/rustdesk-1.5.3-x86_64.exe" },
                { "name": "rustdesk-1.5.3-x86_64.msi", "browser_download_url": "https://github.com/rustdesk/rustdesk/releases/download/1.5.3/rustdesk-1.5.3-x86_64.msi" }
            ] }
            """;
        Assert.Equal("rustdesk-1.5.3-x86_64.exe", DownloaderInstalador.ExtrairAssetGitHubRelease(json)!.Value.NomeArquivo);
    }
}