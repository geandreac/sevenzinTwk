using System.Xml.Linq;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class GpuModuloCompletoTests
{
    sealed class RegistroMemoria : IRegistro
    {
        readonly Dictionary<(Colmeia, string, string), object> _dados = new();
        public int? LerDword(Colmeia h, string c, string n) => _dados.TryGetValue((h, c, n), out var v) && v is int i ? i : null;
        public void EscreverDword(Colmeia h, string c, string n, int valor) => _dados[(h, c, n)] = valor;
        public string? LerString(Colmeia h, string c, string n) => _dados.TryGetValue((h, c, n), out var v) && v is string s ? s : null;
        public void EscreverString(Colmeia h, string c, string n, string valor) => _dados[(h, c, n)] = valor;
        public void ApagarValor(Colmeia h, string c, string n) => _dados.Remove((h, c, n));
    }

    [Fact]
    public void NormalizarCaminhoClasseDriver_aceita_formato_curto_e_completo_e_rejeita_invalido()
    {
        const string esperado = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";
        Assert.Equal(esperado, GpuDriverTweaks.NormalizarCaminhoClasseDriver(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0000"));
        Assert.Equal(esperado, GpuDriverTweaks.NormalizarCaminhoClasseDriver(esperado));
        Assert.Null(GpuDriverTweaks.NormalizarCaminhoClasseDriver(@"{4d36e968-e325-11ce-bfc1-08002be10318}\00AB"));
        Assert.Null(GpuDriverTweaks.NormalizarCaminhoClasseDriver(@"SOFTWARE\OutraChave"));
        Assert.Null(GpuDriverTweaks.NormalizarCaminhoClasseDriver(""));
    }

    [Fact]
    public void GruposNvidia_possuem_contagens_exatas_e_reversao_completa()
    {
        var pastaTemp = Path.Combine(Path.GetTempPath(), "SevenzinTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var reg = new RegistroMemoria();
            var motor = new MotorTweaks(reg, pastaTemp);
            var caminho = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001";
            var grupos = GpuDriverTweaks.CriarGruposNvidia(new[] { caminho });

            Assert.Equal(3, grupos.Count);
            Assert.Equal(15, grupos[0].Tweaks.Count); // Economia de energia
            Assert.Equal(9, grupos[1].Tweaks.Count);  // Power Gating
            Assert.Single(grupos[2].Tweaks);          // Driver Perf (RmPowerFeaturesMask = -1)
            Assert.Equal(-1, grupos[2].Tweaks[0].ValorOtimizado);

            foreach (var g in grupos)
            {
                Assert.False(GpuDriverTweaks.GrupoAplicado(motor, g));
                GpuDriverTweaks.AplicarGrupo(motor, g);
                Assert.True(GpuDriverTweaks.GrupoAplicado(motor, g));
                GpuDriverTweaks.DesfazerGrupo(motor, g);
                Assert.False(GpuDriverTweaks.GrupoAplicado(motor, g));
            }
        }
        finally
        {
            if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
        }
    }

    [Fact]
    public void GruposAmd_possuem_cinco_controles_com_29_basicos_e_8_power_gating()
    {
        var grupos = GpuDriverTweaks.CriarGruposAmd();
        Assert.Equal(5, grupos.Count);
        Assert.Equal(29, grupos[0].Tweaks.Count);
        Assert.Equal(8, grupos[1].Tweaks.Count);
    }

    [Fact]
    public void SetePerfisGraficos_batem_ray_tracing_e_geram_nip_valido()
    {
        var perfis = GpuDriverTweaks.PerfisGraficos;
        Assert.Equal(7, perfis.Count);

        // Básico, Casual e Competitivo mantêm RT disponível; FPS, FPS 2.0, Latência e Avançado desligam RT
        Assert.True(GpuDriverTweaks.ObterPerfil("basic")!.RayTracingDisponivel);
        Assert.True(GpuDriverTweaks.ObterPerfil("casual")!.RayTracingDisponivel);
        Assert.True(GpuDriverTweaks.ObterPerfil("competitive")!.RayTracingDisponivel);
        Assert.False(GpuDriverTweaks.ObterPerfil("fps")!.RayTracingDisponivel);
        Assert.False(GpuDriverTweaks.ObterPerfil("fps2")!.RayTracingDisponivel);
        Assert.False(GpuDriverTweaks.ObterPerfil("latency")!.RayTracingDisponivel);
        Assert.False(GpuDriverTweaks.ObterPerfil("advanced")!.RayTracingDisponivel);

        var fps2 = GpuDriverTweaks.ObterPerfil("fps2")!;
        Assert.Equal("Aplicado: FPS 2.0 · Ray Tracing desligado neste perfil.", fps2.TextoStatusAplicado);

        var doc = GpuDriverTweaks.GerarNipXml(fps2);
        Assert.NotNull(doc.Root);
        Assert.Equal("ArrayOfProfile", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void SincronizarComVersaoDriver_invalida_perfil_quando_driver_muda()
    {
        var pastaTemp = Path.Combine(Path.GetTempPath(), "SevenzinTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            GpuDriverTweaks.SalvarEstadoPerfil(new EstadoPerfilGpu("fps2", "32.0.15.6636", DateTime.UtcNow), pastaTemp);
            var mesmo = GpuDriverTweaks.SincronizarComVersaoDriver("32.0.15.6636", pastaTemp);
            Assert.Equal("fps2", mesmo.PerfilId);

            var mudou = GpuDriverTweaks.SincronizarComVersaoDriver("32.0.15.7216", pastaTemp);
            Assert.Null(mudou.PerfilId);
        }
        finally
        {
            if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
        }
    }

    [Fact]
    public void NvidiaDebloat_protege_componentes_vitais_e_monta_perfis_corretos()
    {
        Assert.True(NvidiaDebloat.EhComponenteProtegido("Display.Driver"));
        Assert.True(NvidiaDebloat.EhComponenteProtegido("NvContainer"));
        Assert.True(NvidiaDebloat.EhComponenteProtegido("NVI2"));
        Assert.True(NvidiaDebloat.EhComponenteProtegido("NVDisplay.Container"));
        Assert.False(NvidiaDebloat.EhComponenteProtegido("GFExperience"));
        Assert.False(NvidiaDebloat.EhComponenteProtegido("NvTelemetry"));

        var seguro = NvidiaDebloat.MontarListaComponentesRemover(new OpcoesNvidiaDebloat(Perfil: PerfilDebloatNvidia.Seguro));
        Assert.Contains("GFExperience", seguro);
        Assert.DoesNotContain("ShadowPlay", seguro);
        Assert.DoesNotContain("NGX", seguro);

        var avancadoComExtras = NvidiaDebloat.MontarListaComponentesRemover(new OpcoesNvidiaDebloat(
            Perfil: PerfilDebloatNvidia.Avancado, RemoverDlssNgx: true, RemoverPhysX: true, RemoverHdAudio: true));
        Assert.Contains("ShadowPlay", avancadoComExtras);
        Assert.Contains("NvApp", avancadoComExtras);
        Assert.Contains("NGX", avancadoComExtras);
        Assert.Contains("PhysX", avancadoComExtras);
        Assert.Contains("HDAudio", avancadoComExtras);
        Assert.All(avancadoComExtras, c => Assert.False(NvidiaDebloat.EhComponenteProtegido(c)));
    }

    [Fact]
    public void NvidiaDebloat_edita_setup_cfg_preservando_driver_e_removendo_bloat_e_eula()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <setup>
              <install>
                <sub-package name="Display.Driver" />
                <sub-package name="NVI2" />
                <sub-package name="GFExperience" />
                <sub-package name="NvTelemetry" />
                <sub-package package="ShadowPlay" />
              </install>
              <files>
                <file name="${{EulaHtmlFile}}" />
                <file name="${{FunctionalConsentFile}}" />
                <file name="${{PrivacyPolicyFile}}" />
                <file name="setup.exe" />
              </files>
            </setup>
            """;
        var doc = XDocument.Parse(xml);
        int removidos = NvidiaDebloat.EditarManifestoSetupCfg(doc, new[] { "GFExperience", "NvTelemetry", "ShadowPlay", "Display.Driver" });
        Assert.Equal(6, removidos); // 3 sub-packages de bloat + 3 arquivos de consentimento; Display.Driver é protegido!
        var restante = doc.ToString();
        Assert.Contains("Display.Driver", restante);
        Assert.Contains("NVI2", restante);
        Assert.DoesNotContain("GFExperience", restante);
        Assert.DoesNotContain("EulaHtmlFile", restante);
    }

    [Fact]
    public void NvidiaDebloat_valida_chassi_urls_e_json_oficial()
    {
        Assert.True(NvidiaDebloat.EhNotebookGpu("NVIDIA GeForce RTX 4060 Laptop GPU"));
        Assert.True(NvidiaDebloat.EhNotebookGpu("GeForce GTX 1050M"));
        Assert.False(NvidiaDebloat.EhNotebookGpu("NVIDIA GeForce RTX 3060"));
        Assert.Equal("RTX 4060", NvidiaDebloat.NormalizarModeloGpu("NVIDIA GeForce RTX 4060 Laptop GPU"));

        Assert.True(NvidiaDebloat.UrlDownloadNvidiaValida("https://us.download.nvidia.com/Windows/566.36/566.36-desktop-win10-win11-64bit-international-dch-whql.exe"));
        Assert.False(NvidiaDebloat.UrlDownloadNvidiaValida("http://us.download.nvidia.com/Windows/566.36/566.36-desktop-win10-win11-64bit-international-dch-whql.exe"));
        Assert.False(NvidiaDebloat.UrlDownloadNvidiaValida("https://dropbox.com/566.36-desktop-win10-win11-64bit-international-dch-whql.exe"));

        Assert.True(NvidiaDebloat.NomeInstaladorOficialValido("566.36-desktop-win10-win11-64bit-international-dch-whql.exe", CanalDriverNvidia.GameReady));
        Assert.False(NvidiaDebloat.NomeInstaladorOficialValido("566.36-desktop-win10-win11-64bit-international-nsd-dch-whql.exe", CanalDriverNvidia.GameReady));
        Assert.True(NvidiaDebloat.NomeInstaladorOficialValido("566.36-desktop-win10-win11-64bit-international-nsd-dch-whql.exe", CanalDriverNvidia.Studio));
    }

    [Fact]
    public void Estado2_validadores_kms_powerlimit_e_msi_funcionam()
    {
        Assert.True(Licenciamento.HostKmsValido("kms.empresa.corp:1688"));
        Assert.False(Licenciamento.HostKmsValido("-ipk XXXXX"));
        Assert.False(Licenciamento.HostKmsValido("kms.local /ato"));

        var tele = GpuOverclock.AnalisarCsv("RTX 4060 Ti, 615, 3105, 405, 9001, 43.0, 28.0, 160.0, 180.0, 15, 1200, 16384, 160.0, 100.0")!;
        Assert.Null(GpuOverclock.ValidarPowerLimit(170, tele));
        Assert.NotNull(GpuOverclock.ValidarPowerLimit(90, tele));
        Assert.NotNull(GpuOverclock.ValidarPowerLimit(200, tele));

        Assert.Equal((true, "Alta"), MsiIrq.InterpretarMsi(1, 3));
        Assert.Equal((false, "Padrão"), MsiIrq.InterpretarMsi(0, null));
        Assert.Equal("NVIDIA GeForce RTX 3060", MsiIrq.LimparDescricaoInf("@oem42.inf,%nvidia_dev%;NVIDIA GeForce RTX 3060"));
    }
}
