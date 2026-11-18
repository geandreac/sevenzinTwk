using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Grupo de ajustes de registro de driver de GPU (NVIDIA ou AMD), 100% reversível via <see cref="MotorTweaks"/>.</summary>
public sealed record GrupoDriverGpu(
    string Id,
    string Nome,
    string Descricao,
    FabricanteGpu Fabricante,
    IReadOnlyList<Tweak> Tweaks);

/// <summary>Definição de um dos 7 perfis gráficos exclusivos da tela GPU.</summary>
public sealed record PerfilGraficoGpu(
    string Id,
    string Nome,
    string Subtitulo,
    bool RayTracingDisponivel,
    string ArquivoNip,
    IReadOnlyList<string> TweakIds)
{
    public string StatusRayTracing => RayTracingDisponivel
        ? "Ray Tracing disponível neste perfil."
        : "Ray Tracing desligado neste perfil.";

    public string TextoStatusAplicado =>
        $"Aplicado: {Nome} · {StatusRayTracing}";
}

/// <summary>Estado persistido do perfil gráfico ativo e da versão do driver em que foi aplicado.</summary>
public sealed record EstadoPerfilGpu(string? PerfilId, string? VersaoDriver, DateTime? AplicadoEmUtc);

/// <summary>
/// Ajustes de driver NVIDIA/AMD e Perfis Gráficos com paridade 1:1 aos valores literais identificados
/// na engenharia reversa estática, aplicados de forma auditada e reversível via <see cref="MotorTweaks"/>.
/// Nunca baixa binários de espelhos/Dropbox, não cria pastas ocultas e não encerra processos de terceiros.
/// </summary>
public static class GpuDriverTweaks
{
    public const string GuidClasseDisplay = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    public const string CaminhoClassePadrao = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";

    /// <summary>Normaliza o valor `Driver` de `HKLM\SYSTEM\CurrentControlSet\Enum\PCI\...` para um caminho HKLM válido.</summary>
    public static string? NormalizarCaminhoClasseDriver(string? valorDriver)
    {
        if (string.IsNullOrWhiteSpace(valorDriver)) return null;
        var v = valorDriver.Trim();
        const string prefixoCompleto = @"SYSTEM\CurrentControlSet\Control\Class\";
        if (v.StartsWith(prefixoCompleto, StringComparison.OrdinalIgnoreCase))
        {
            var resto = v[prefixoCompleto.Length..];
            return EhSubchaveDisplayValida(resto) ? prefixoCompleto + resto : null;
        }
        return EhSubchaveDisplayValida(v) ? prefixoCompleto + v : null;
    }

    static bool EhSubchaveDisplayValida(string sub)
    {
        const string p = GuidClasseDisplay + @"\";
        if (!sub.StartsWith(p, StringComparison.OrdinalIgnoreCase) || sub.Length != p.Length + 4)
            return false;
        for (int i = p.Length; i < sub.Length; i++)
            if (sub[i] < '0' || sub[i] > '9') return false;
        return true;
    }

    /// <summary>Resolve as chaves HKLM `Control\Class\{4d36e968-...}\XXXX` ativas para o fabricante informado.</summary>
    public static IReadOnlyList<string> ResolverChavesClasseDriver(FabricanteGpu fabricante)
    {
        var resultado = new List<string>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string ven = fabricante switch
        {
            FabricanteGpu.Nvidia => "VEN_10DE",
            FabricanteGpu.Amd => "VEN_1002",
            FabricanteGpu.Intel => "VEN_8086",
            _ => ""
        };
        if (ven.Length == 0) return resultado;

        try
        {
            foreach (var mo in Wmi.Query("SELECT PNPDeviceID FROM Win32_VideoController"))
            {
                var pnp = Wmi.Str(mo, "PNPDeviceID");
                if (string.IsNullOrWhiteSpace(pnp) || !pnp.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!pnp.Contains(ven, StringComparison.OrdinalIgnoreCase))
                    continue;

                using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + pnp);
                var norm = NormalizarCaminhoClasseDriver(enumKey?.GetValue("Driver") as string);
                if (norm is not null && vistos.Add(norm))
                    resultado.Add(norm);
            }
        }
        catch { }

        if (resultado.Count == 0)
        {
            try
            {
                const string baseClass = @"SYSTEM\CurrentControlSet\Control\Class\" + GuidClasseDisplay;
                using var raiz = Registry.LocalMachine.OpenSubKey(baseClass);
                if (raiz is not null)
                {
                    foreach (var sub in raiz.GetSubKeyNames())
                    {
                        if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;
                        using var sk = raiz.OpenSubKey(sub);
                        var prov = (sk?.GetValue("ProviderName") as string) ?? "";
                        bool bate = fabricante == FabricanteGpu.Nvidia
                            ? prov.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                            : fabricante == FabricanteGpu.Amd && (prov.Contains("AMD", StringComparison.OrdinalIgnoreCase) || prov.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase));
                        if (bate)
                        {
                            var full = baseClass + @"\" + sub;
                            if (vistos.Add(full)) resultado.Add(full);
                        }
                    }
                }
            }
            catch { }
        }

        return resultado;
    }

    /// <summary>Lê a versão atual do driver NVIDIA diretamente da chave de classe do adaptador, quando presente.</summary>
    public static string? LerVersaoDriverClasse(FabricanteGpu fabricante)
    {
        try
        {
            foreach (var caminho in ResolverChavesClasseDriver(fabricante))
            {
                using var k = Registry.LocalMachine.OpenSubKey(caminho);
                if (k?.GetValue("DriverVersion") is string v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────── Grupos NVIDIA (3 toggles) ───────────────────────────

    static readonly (string NomeValor, int Valor)[] ValoresNvidiaEconomiaEnergia =
    {
        ("RMPcieLinkSpeed", 4),
        ("RMDOPSB", 10914),
        ("RmRcWatchdog", 0),
        ("RmOverrideSupportChipsetAspm", 3),
        ("RML1ExitLatencyOverride", 1),
        ("RMEnableASPMPublicBits", 0),
        ("RMDisableGpuASPMFlags", 3),
        ("RMEnableASPMAtLoad", 0),
        ("RMEnableASPMDT", 0),
        ("RMPcieLtrL12ThresholdOverride", 0),
        ("RmClkSlowdown", 71303168),
        ("RmClkPowerOffDramPllWhenUnused", 0),
        ("RmDisableACPI", 1023),
        ("RmMIONoPowerOff", 1),
        ("DisableDynamicPstate", 1),
    };

    static readonly (string NomeValor, int Valor)[] ValoresNvidiaPowerGating =
    {
        ("RMElcg", 1431655765),
        ("RMBlcg", 286331153),
        ("RMSlcg", 262131),
        ("RMElpg", 4095),
        ("RMFspg", 15),
        ("RMOPSB", 10914),
        ("RMPriorityThrottleDelay", 0),
        ("RmL2CleanFbPull", 0),
        ("RMGC6Feature", 699050),
    };

    static readonly (string NomeValor, int Valor)[] ValoresNvidiaDriverPerf =
    {
        ("RmPowerFeaturesMask", -1),
    };

    /// <summary>Cria os 3 grupos de ajustes de driver NVIDIA apontando para a(s) chave(s) de classe resolvida(s).</summary>
    public static IReadOnlyList<GrupoDriverGpu> CriarGruposNvidia(IReadOnlyList<string>? caminhosClasse = null)
    {
        var alvos = (caminhosClasse is { Count: > 0 } ? caminhosClasse : new[] { CaminhoClassePadrao });
        return new[]
        {
            MontarGrupoDword("nv-economia-energia", "Economia de energia",
                "Menos estados ociosos no driver (ASPM PCIe, Dynamic P-State e slowdown de clock — 15 DWORDs).",
                FabricanteGpu.Nvidia, alvos, ValoresNvidiaEconomiaEnergia),
            MontarGrupoDword("nv-power-gating", "Power Gating",
                "Mantém blocos da GPU acordados (ELCG, BLCG, SLCG, ELPG, FSPG e GC6 — 9 DWORDs).",
                FabricanteGpu.Nvidia, alvos, ValoresNvidiaPowerGating),
            MontarGrupoDword("nv-driver-perf", "Driver Perf",
                "Máscara de desempenho no driver (RmPowerFeaturesMask = 0xFFFFFFFF).",
                FabricanteGpu.Nvidia, alvos, ValoresNvidiaDriverPerf),
        };
    }

    // ─────────────────────────── Grupos AMD (5 controles) ───────────────────────────

    static readonly (string NomeValor, int Valor)[] ValoresAmdBasicos =
    {
        ("AsicOnLowPower", 0),
        ("BGM_EnableLTR", 0),
        ("ChillEnabled", 0),
        ("DalDisableCursorCache", 1),
        ("DalDisableCursorCaching", 1),
        ("DisableBandwidthMonitoring", 1),
        ("DisableDMACopy", 1),
        ("DisableFBCForFullScreenApp", 0),
        ("DisableSAMUPowerGating", 1),
        ("EnableAspmL0s", 0),
        ("EnableAspmL1", 0),
        ("EnableUlps", 0),
        ("EnableUlps_NA", 0),
        ("FRXMaxLat0", 0),
        ("FRXMaxLat1", 0),
        ("KMD_CopyToGSMemEnabled", 0),
        ("KMD_DisableStutterMode", 0),
        ("KMD_EnableComputePreemption", 0),
        ("KMD_FRTEnabled", 0),
        ("LTRConfig", 0),
        ("MLSetLowPriGfx_DEF", 0),
        ("PerfLevelSrc", 13107),
        ("PowerScheme_DEF", 0),
        ("PP_ActivityTarget", 50),
        ("PP_FuzzyFanControl_EnableDPMBasedFuzzyFan_DEF", 0),
        ("PP_ThermalAutoThrottlingEnable", 0),
        ("PP_SclkDeepSleepDisable", 1),
        ("SQSEnabled", 1),
        ("StutterMode", 0),
    };

    static readonly (string NomeValor, int Valor)[] ValoresAmdPowerGating =
    {
        ("PP_DisableGfxASPML0s", 1),
        ("PP_DisableGfxASPML1", 1),
        ("PP_DisablePowerContainment", 1),
        ("DisableDrmdmaPowerGating", 1),
        ("DisableGMMPowerGating", 1),
        ("DisableVCNPowerGating", 1),
        ("PP_MCLKStutterModeThreshold", 0),
        ("PP_GPUPowerDownEnabled", 0),
    };

    static readonly (string NomeValor, string ValorTexto)[] ValoresAmdUmd =
    {
        ("AAF_NA", "0"),
        ("AntiAlias_NA", "0"),
        ("AreaAniso_NA", "0"),
        ("ASTT_DEF", "0"),
        ("ATMS_NA", "0"),
        ("CFM_DEF", "1"),
        ("EQAA_DEF", "0"),
        ("GI_DEF", "1"),
        ("Main3D_DEF", "0"),
        ("TextureLod_DEF", "3"),
        ("TextureLod_NA", "3"),
    };

    /// <summary>Cria os 5 grupos de ajustes de driver AMD apontando para a(s) chave(s) de classe resolvida(s).</summary>
    public static IReadOnlyList<GrupoDriverGpu> CriarGruposAmd(IReadOnlyList<string>? caminhosClasse = null)
    {
        var alvos = (caminhosClasse is { Count: > 0 } ? caminhosClasse : new[] { CaminhoClassePadrao });
        var umdTweaks = new List<Tweak>();
        for (int idx = 0; idx < alvos.Count; idx++)
        {
            string sufixo = idx == 0 ? "" : $"-{idx}";
            string subUmd = alvos[idx] + @"\UMD";
            foreach (var (nome, txt) in ValoresAmdUmd)
            {
                umdTweaks.Add(new Tweak(
                    $"amd-umd-{nome.ToLowerInvariant()}{sufixo}",
                    $"AMD UMD: {nome}",
                    $"Define {nome}={txt} na subchave UMD do driver AMD.",
                    "GPU",
                    Colmeia.MaquinaLocal,
                    subUmd,
                    nome,
                    0,
                    "Médio — requer reinício; reversível",
                    ExigeAdmin: true,
                    Tipo: TipoValor.String,
                    ValorTexto: txt));
            }
        }

        var dvrTweaks = new List<Tweak>
        {
            new("amd-dvr-perfmon", "AMD DVR: Opacidade monitor", "PerformanceMonitorOpacityWA=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "PerformanceMonitorOpacityWA", 0, "Baixo", false),
            new("amd-dvr-chill", "AMD DVR: Radeon Chill off", "ChillEnabled=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "ChillEnabled", 0, "Baixo", false),
            new("amd-dvr-instantreplay", "AMD DVR: Instant Replay off", "PrevInstantReplayEnable=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "PrevInstantReplayEnable", 0, "Baixo", false),
            new("amd-dvr-ingamereplay", "AMD DVR: In-Game Replay off", "PrevInGameReplayEnabled=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "PrevInGameReplayEnabled", 0, "Baixo", false),
            new("amd-dvr-gif", "AMD DVR: Instant GIF off", "PrevInstantGifEnabled=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "PrevInstantGifEnabled", 0, "Baixo", false),
            new("amd-dvr-remote", "AMD DVR: Servidor remoto off", "RemoteServerStatus=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "RemoteServerStatus", 0, "Baixo", false),
            new("amd-dvr-scene", "AMD DVR: ActiveSceneId=0", "ActiveSceneId=0", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "ActiveSceneId", 0, "Baixo", false, Tipo: TipoValor.String, ValorTexto: "0"),
            new("amd-dvr-overlay", "AMD DVR: Overlay off", "ShowRSOverlay=false", "GPU", Colmeia.UsuarioAtual, @"Software\AMD\DVR", "ShowRSOverlay", 0, "Baixo", false, Tipo: TipoValor.String, ValorTexto: "false"),
        };

        var dispTweaks = new List<Tweak>
        {
            new("amd-kmd-pxvideo", "AMD KMD: Vídeo em GPU dedicada", "KMD_PXForceVideoPlaybackToIntegrated=0", "GPU", Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\services\amdkmdap", "KMD_PXForceVideoPlaybackToIntegrated", 0, "Médio — requer reinício", true),
            new("amd-kmd-miniseg-opt", "AMD KMD: Opções de segmento compartilhado", "KMD_APlusISharedMiniSegmentOptions=7", "GPU", Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\services\amdkmdap", "KMD_APlusISharedMiniSegmentOptions", 7, "Médio — requer reinício", true),
            new("amd-kmd-miniseg-size", "AMD KMD: Tamanho de segmento compartilhado", "KMD_APlusISharedMiniSegmentSize=67108864 (64 MB)", "GPU", Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\services\amdkmdap", "KMD_APlusISharedMiniSegmentSize", 67108864, "Médio — requer reinício", true),
        };
        for (int idx = 0; idx < alvos.Count; idx++)
        {
            string sufixo = idx == 0 ? "" : $"-{idx}";
            dispTweaks.Add(new Tweak($"amd-kmd-crossgpu{sufixo}", "AMD KMD: CrossGpuDisplaySupport", "KMD_EnableCrossGpuDisplaySupport=1", "GPU", Colmeia.MaquinaLocal, alvos[idx], "KMD_EnableCrossGpuDisplaySupport", 1, "Médio — requer reinício", true));
        }

        return new[]
        {
            MontarGrupoDword("amd-ajustes-basicos", "Ajustes básicos",
                "Latência, ULPS, SCLK Deep Sleep, ASPM e StutterMode no driver AMD (29 DWORDs).",
                FabricanteGpu.Amd, alvos, ValoresAmdBasicos),
            MontarGrupoDword("amd-power-gating", "Power Gating",
                "Desativa ASPM L0s/L1, Power Containment, DRMDMA/GMM/VCN Power Gating e GPU PowerDown (8 DWORDs).",
                FabricanteGpu.Amd, alvos, ValoresAmdPowerGating),
            new GrupoDriverGpu("amd-umd", "UMD Settings",
                "Ajustes de Texture LOD, Anti-Alias e defaults 3D na subchave UMD do driver AMD.",
                FabricanteGpu.Amd, umdTweaks),
            new GrupoDriverGpu("amd-dvr", "DVR Settings",
                "Desativa Instant Replay, GIF, servidor remoto, Chill e overlay Radeon (HKCU).",
                FabricanteGpu.Amd, dvrTweaks),
            new GrupoDriverGpu("amd-dispositivos", "Dispositivos",
                "Ajusta serviço amdkmdap (segmento compartilhado) e CrossGpuDisplaySupport.",
                FabricanteGpu.Amd, dispTweaks),
        };
    }

    static GrupoDriverGpu MontarGrupoDword(
        string idGrupo,
        string nome,
        string descricao,
        FabricanteGpu fab,
        IReadOnlyList<string> caminhos,
        IReadOnlyList<(string NomeValor, int Valor)> pares)
    {
        var lista = new List<Tweak>(caminhos.Count * pares.Count);
        for (int idx = 0; idx < caminhos.Count; idx++)
        {
            string sufixo = idx == 0 ? "" : $"-{idx}";
            string caminho = caminhos[idx];
            foreach (var (nomeVal, val) in pares)
            {
                lista.Add(new Tweak(
                    $"{idGrupo}-{nomeVal.ToLowerInvariant()}{sufixo}",
                    $"{nome}: {nomeVal}",
                    $"{nomeVal}={val} em {caminho}",
                    "GPU",
                    Colmeia.MaquinaLocal,
                    caminho,
                    nomeVal,
                    val,
                    "Médio — pode aumentar consumo/temperatura em repouso; reversível; requer reinício",
                    ExigeAdmin: true));
            }
        }
        return new GrupoDriverGpu(idGrupo, nome, descricao, fab, lista);
    }

    public static bool GrupoAplicado(MotorTweaks motor, GrupoDriverGpu grupo) =>
        grupo.Tweaks.Count > 0 && grupo.Tweaks.All(motor.Aplicado);

    public static int AplicarGrupo(MotorTweaks motor, GrupoDriverGpu grupo)
    {
        int n = 0;
        foreach (var t in grupo.Tweaks) { motor.Aplicar(t); n++; }
        return n;
    }

    public static int DesfazerGrupo(MotorTweaks motor, GrupoDriverGpu grupo)
    {
        int n = 0;
        foreach (var t in grupo.Tweaks)
            if (motor.Desfazer(t)) n++;
        return n;
    }

    public static int DesativarGrupo(MotorTweaks motor, GrupoDriverGpu grupo)
    {
        int n = 0;
        foreach (var t in grupo.Tweaks)
            if (motor.Desativar(t)) n++;
        return n;
    }

    // ─────────────────────────── 7 Perfis Gráficos Exclusivos ───────────────────────────

    public static readonly IReadOnlyList<PerfilGraficoGpu> PerfisGraficos = new[]
    {
        new PerfilGraficoGpu("basic", "Básico", "Visual e desempenho", true, "honest2.nip",
            new[] { "gamedvr-off" }),
        new PerfilGraficoGpu("casual", "Casual", "Jogos do dia a dia", true, "Casual.nip",
            new[] { "gamedvr-off", "gamedvr-captura-off", "transparencia-off" }),
        new PerfilGraficoGpu("competitive", "Competitivo", "Foco competitivo", true, "Competitive.nip",
            new[] { "hags", "gamedvr-off", "gamedvr-captura-off", "mmcss-responsiveness" }),
        new PerfilGraficoGpu("fps", "FPS", "Perfil de desempenho", false, "FPS.nip",
            new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off" }),
        new PerfilGraficoGpu("fps2", "FPS 2.0", "Perfil melhorado", false, "honest.nip",
            new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off", "mmcss-responsiveness", "mmcss-games-gpu", "mmcss-games-prio" }),
        new PerfilGraficoGpu("latency", "Latência", "Resposta de input", false, "Latency.nip",
            new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off", "mouse-aceleracao-off", "mouse-limiar1-off", "mouse-limiar2-off" }),
        new PerfilGraficoGpu("advanced", "Avançado", "Ajustes agressivos", false, "honest1.nip",
            new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off", "mmcss-responsiveness", "mouse-aceleracao-off", "mouse-limiar1-off", "mouse-limiar2-off", "transparencia-off", "aero-peek-off" }),
    };

    public static PerfilGraficoGpu? ObterPerfil(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : PerfisGraficos.FirstOrDefault(p => p.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Gera um manifesto `.nip` (XML compatível com NVIDIA Profile Inspector) sincronizado 1:1 com os parâmetros DRS reais do perfil.</summary>
    public static XDocument GerarNipXml(PerfilGraficoGpu perfil)
    {
        var drsParams = NvidiaDrs.ObterParametrosPerfil(perfil.Id);
        int dxr = perfil.RayTracingDisponivel ? 1 : 0;

        XElement Setting(string name, uint id, uint val) => new("ProfileSetting",
            new XElement("SettingNameInfo", name),
            new XElement("SettingID", id),
            new XElement("SettingValue", val),
            new XElement("ValueType", "Dword"));

        var settingsElem = new XElement("Settings");
        foreach (var p in drsParams)
            settingsElem.Add(Setting(p.Nome, p.SettingId, p.Valor));
        settingsElem.Add(Setting("Ray Tracing (DXR) Support", 544428297u, unchecked((uint)dxr)));

        return new XDocument(
            new XDeclaration("1.0", "utf-16", null),
            new XElement("ArrayOfProfile",
                new XElement("Profile",
                    new XElement("ProfileName", "Base Profile"),
                    new XElement("Executeables"),
                    settingsElem)));
    }

    public static string ExportarNipLocal(PerfilGraficoGpu perfil, string? pastaBase = null)
    {
        var raiz = pastaBase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "PerfisGpu");
        Directory.CreateDirectory(raiz);
        var caminho = Path.Combine(raiz, perfil.ArquivoNip);
        var doc = GerarNipXml(perfil);
        using var sw = new StreamWriter(caminho, false, Encoding.Unicode);
        doc.Save(sw);
        return caminho;
    }

    static string CaminhoEstadoPerfil(string? pastaEstado) =>
        Path.Combine(pastaEstado ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin"), "perfil-gpu.json");

    public static EstadoPerfilGpu LerEstadoPerfil(string? pastaEstado = null)
    {
        try
        {
            var arq = CaminhoEstadoPerfil(pastaEstado);
            if (!File.Exists(arq)) return new(null, null, null);
            return JsonSerializer.Deserialize<EstadoPerfilGpu>(File.ReadAllText(arq)) ?? new(null, null, null);
        }
        catch { return new(null, null, null); }
    }

    public static void SalvarEstadoPerfil(EstadoPerfilGpu estado, string? pastaEstado = null)
    {
        var arq = CaminhoEstadoPerfil(pastaEstado);
        Directory.CreateDirectory(Path.GetDirectoryName(arq)!);
        File.WriteAllText(arq, JsonSerializer.Serialize(estado));
    }

    /// <summary>Se a versão do driver mudou desde a aplicação do perfil, limpa o estado antigo (paridade com TweakDriver).</summary>
    public static EstadoPerfilGpu SincronizarComVersaoDriver(string? versaoDriverAtual, string? pastaEstado = null)
    {
        var atual = LerEstadoPerfil(pastaEstado);
        if (atual.PerfilId is not null
            && !string.IsNullOrWhiteSpace(atual.VersaoDriver)
            && !string.IsNullOrWhiteSpace(versaoDriverAtual)
            && !string.Equals(atual.VersaoDriver.Trim(), versaoDriverAtual.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var limpo = new EstadoPerfilGpu(null, versaoDriverAtual, null);
            SalvarEstadoPerfil(limpo, pastaEstado);
            return limpo;
        }
        return atual;
    }
}
