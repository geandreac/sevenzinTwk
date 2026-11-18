using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Um parâmetro 3D individual do banco DRS da NVIDIA (compatível com NVIDIA Profile Inspector).</summary>
public sealed record ParametroDrsNvidia(
    uint SettingId,
    string Nome,
    uint Valor,
    string DescricaoValor,
    bool OpcionalNoDriver = false);

/// <summary>Resultado da aplicação ou restauração de perfil direto no banco DRS do driver NVIDIA (`nvapi64.dll`).</summary>
public sealed record ResultadoDrsNvidia(
    bool Disponivel,
    bool Sucesso,
    int Aplicados,
    int Ignorados,
    string Mensagem);

/// <summary>
/// Integração direta e segura com a API nativa NVIDIA Driver Settings (`NvAPI_DRS` em `System32\nvapi64.dll`).
/// Altera de verdade o *Base Profile* (Global Profile) lido pelo Painel da NVIDIA e pelo NVIDIA Profile Inspector,
/// sem baixar nenhum binário externo e permitindo restaurar os padrões de fábrica com 1 clique.
/// </summary>
public static class NvidiaDrs
{
    // IDs oficiais NvApiDriverSettings.h + CustomSettingNames.xml (NVIDIA Profile Inspector)
    public const uint IdPowerManagementMode = 0x1057EB71;         // PREFERRED_PSTATE_ID (0 = Normal/Adaptive, 1 = Prefer Max Performance)
    public const uint IdTextureFilteringQuality = 0x00CE2691;     // QUALITY_ENHANCEMENTS_ID (0 = Quality, 0xA = Performance, 0x14 = High Performance)
    public const uint IdAnisoSampleOptimization = 0x00E73211;     // PS_TEXFILTER_ANISO_OPTS2_ID (0 = Off, 1 = On)
    public const uint IdTrilinearOptimization = 0x002ECAF2;       // PS_TEXFILTER_DISABLE_TRILIN_SLOPE_ID (0 = On, 1 = Off)
    public const uint IdNegativeLodBias = 0x0019BB68;             // PS_TEXFILTER_NO_NEG_LODBIAS_ID (0 = Allow, 1 = Clamp)
    public const uint IdMaxPrerenderedFrames = 0x007BA09E;        // PRERENDERLIMIT_ID (0 = App Controlled, 1 = 1, 2 = 2)
    public const uint IdLowLatencyCpl = 0x0005F543;               // Low Latency Mode CPL (0 = Off, 1 = On, 2 = Ultra)
    public const uint IdUltraLowLatencyEnabled = 0x10835000;      // Ultra-Low Latency Enabled (0 = Off, 1 = On)
    public const uint IdVerticalSync = 0x00A879CF;                // VSYNCMODE_ID (0x60925292 = 3D App, 0x08416747 = Force Off)
    public const uint IdThreadedOptimization = 0x20C1221E;        // OGL_THREAD_CONTROL_ID (0 = Auto, 1 = On, 2 = Off)
    public const uint IdShaderDiskCache = 0x00198FFF;             // PS_SHADERDISKCACHE_ID (0 = Off, 1 = On)

    public const uint VsyncPadraoAplicativo = 0x60925292;
    public const uint VsyncForcarDesligado = 0x08416747;

    /// <summary>Todos os SettingIDs gerenciados pelo Sevenzin para restauração limpa ao remover o perfil.</summary>
    public static readonly IReadOnlyList<uint> TodosSettingIdsGerenciados = new uint[]
    {
        IdPowerManagementMode,
        IdTextureFilteringQuality,
        IdAnisoSampleOptimization,
        IdTrilinearOptimization,
        IdNegativeLodBias,
        IdMaxPrerenderedFrames,
        IdLowLatencyCpl,
        IdUltraLowLatencyEnabled,
        IdVerticalSync,
        IdThreadedOptimization,
        IdShaderDiskCache,
    };

    /// <summary>
    /// Retorna a tabela exata de parâmetros DRS para cada um dos 7 perfis gráficos exclusivos.
    /// Cada perfil possui combinação própria de P-State, filtragem de textura, fila de quadros, Low Latency, VSync e Threading.
    /// </summary>
    public static IReadOnlyList<ParametroDrsNvidia> ObterParametrosPerfil(string perfilId)
    {
        var id = (perfilId ?? "").Trim().ToLowerInvariant();
        return id switch
        {
            "basic" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 0u, "Normal / Adaptive"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000000u, "Quality"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 0u, "Off"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 1u, "Clamp"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 0u, "Use the 3D application setting"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 0u, "Off", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 0u, "Off", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncPadraoAplicativo, "Use the 3D application setting"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 0u, "Auto"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            "casual" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000000u, "Quality"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 0u, "Off"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 1u, "Clamp"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 2u, "2 frames"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 1u, "On", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 0u, "Off", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncPadraoAplicativo, "Use the 3D application setting"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            "competitive" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x0000000Au, "Performance"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 1u, "On"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 1u, "Clamp"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 1u, "1 frame"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 1u, "On", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 0u, "Off", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncForcarDesligado, "Force off"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            "fps" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000014u, "High performance"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 1u, "On"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 0u, "Allow"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 2u, "2 frames"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 1u, "On", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 0u, "Off", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncForcarDesligado, "Force off"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            "fps2" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000014u, "High performance"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 1u, "On"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 0u, "Allow"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 1u, "1 frame"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 1u, "On", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 1u, "On", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncForcarDesligado, "Force off"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            "latency" => new[]
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000014u, "High performance"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 1u, "On"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 1u, "Clamp"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 1u, "1 frame"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 2u, "Ultra", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 1u, "Ultra", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncForcarDesligado, "Force off"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
            _ => new[] // "advanced"
            {
                new ParametroDrsNvidia(IdPowerManagementMode, "Power management mode", 1u, "Prefer maximum performance"),
                new ParametroDrsNvidia(IdTextureFilteringQuality, "Texture filtering - Quality", 0x00000014u, "High performance"),
                new ParametroDrsNvidia(IdAnisoSampleOptimization, "Texture filtering - Anisotropic sample optimization", 1u, "On"),
                new ParametroDrsNvidia(IdTrilinearOptimization, "Texture filtering - Trilinear optimization", 0u, "On"),
                new ParametroDrsNvidia(IdNegativeLodBias, "Texture filtering - Negative LOD bias", 0u, "Allow"),
                new ParametroDrsNvidia(IdMaxPrerenderedFrames, "Maximum pre-rendered frames", 1u, "1 frame"),
                new ParametroDrsNvidia(IdLowLatencyCpl, "Low Latency Mode", 2u, "Ultra", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdUltraLowLatencyEnabled, "Ultra-Low Latency", 1u, "Ultra", OpcionalNoDriver: true),
                new ParametroDrsNvidia(IdVerticalSync, "Vertical Sync", VsyncForcarDesligado, "Force off"),
                new ParametroDrsNvidia(IdThreadedOptimization, "Threaded optimization", 1u, "On"),
                new ParametroDrsNvidia(IdShaderDiskCache, "Shader Cache", 1u, "On", OpcionalNoDriver: true),
            },
        };
    }

    // ─────────────────────────── Interop NvAPI_DRS (`nvapi64.dll`) ───────────────────────────

    const uint HashInitialize = 0x0150E828;
    const uint HashDrsCreateSession = 0x0694D52E;
    const uint HashDrsDestroySession = 0xDAD9CFF8;
    const uint HashDrsLoadSettings = 0x375DBD6B;
    const uint HashDrsSaveSettings = 0xFCBC7E14;
    const uint HashDrsGetBaseProfile = 0xDA8466A0;
    const uint HashDrsSetSetting = 0x577DD202;
    const uint HashDrsGetSetting = 0x73BF8338;
    const uint HashDrsRestoreProfileDefaultSetting = 0x53F0381E;

    // Layout exato de NVDRS_SETTING_V1 (12320 bytes = 0x3020, MAKE_NVAPI_VERSION = 0x00013020)
    public const int TamanhoNvdrsSettingV1 = 12320;
    public const uint VersaoNvdrsSettingV1 = 0x00013020;
    const int OffsetSettingId = 4100;
    const int OffsetSettingType = 4104;
    const int OffsetSettingLocation = 4108;
    const int OffsetIsCurrentPredefined = 4112;
    const int OffsetIsPredefinedValid = 4116;
    const int OffsetU32CurrentValue = 8220;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate IntPtr QueryInterfaceDelegate(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int NvApiInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsCreateSessionDelegate(out IntPtr hSession);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsDestroySessionDelegate(IntPtr hSession);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsLoadSettingsDelegate(IntPtr hSession);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsSaveSettingsDelegate(IntPtr hSession);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsGetBaseProfileDelegate(IntPtr hSession, out IntPtr hProfile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsSetSettingDelegate(IntPtr hSession, IntPtr hProfile, IntPtr pSetting);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsGetSettingDelegate(IntPtr hSession, IntPtr hProfile, uint settingId, IntPtr pSetting);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int DrsRestoreDefaultSettingDelegate(IntPtr hSession, IntPtr hProfile, uint settingId);

    sealed record ApiCarregada(
        DrsCreateSessionDelegate CreateSession,
        DrsDestroySessionDelegate DestroySession,
        DrsLoadSettingsDelegate LoadSettings,
        DrsSaveSettingsDelegate SaveSettings,
        DrsGetBaseProfileDelegate GetBaseProfile,
        DrsSetSettingDelegate SetSetting,
        DrsGetSettingDelegate GetSetting,
        DrsRestoreDefaultSettingDelegate RestoreDefaultSetting);

    static ApiCarregada? TentarCarregarApi()
    {
        try
        {
            var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var caminhoDll = Path.Combine(sys32, "nvapi64.dll");
            if (!File.Exists(caminhoDll)) return null;
            if (!NativeLibrary.TryLoad(caminhoDll, out var hMod)) return null;
            if (!NativeLibrary.TryGetExport(hMod, "nvapi_QueryInterface", out var pQuery)) return null;

            var query = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(pQuery);
            T? Obter<T>(uint hash) where T : Delegate
            {
                var ptr = query(hash);
                return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
            }

            var init = Obter<NvApiInitializeDelegate>(HashInitialize);
            if (init is null || init() != 0) return null;

            var create = Obter<DrsCreateSessionDelegate>(HashDrsCreateSession);
            var destroy = Obter<DrsDestroySessionDelegate>(HashDrsDestroySession);
            var load = Obter<DrsLoadSettingsDelegate>(HashDrsLoadSettings);
            var save = Obter<DrsSaveSettingsDelegate>(HashDrsSaveSettings);
            var baseProf = Obter<DrsGetBaseProfileDelegate>(HashDrsGetBaseProfile);
            var set = Obter<DrsSetSettingDelegate>(HashDrsSetSetting);
            var get = Obter<DrsGetSettingDelegate>(HashDrsGetSetting);
            var restore = Obter<DrsRestoreDefaultSettingDelegate>(HashDrsRestoreProfileDefaultSetting);

            if (create is null || destroy is null || load is null || save is null || baseProf is null || set is null || get is null || restore is null)
                return null;

            return new ApiCarregada(create, destroy, load, save, baseProf, set, get, restore);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Verifica de forma somente-leitura se a biblioteca `nvapi64.dll` está ativa nesta máquina.</summary>
    public static bool EstaDisponivel() => TentarCarregarApi() is not null;

    /// <summary>Lê de forma somente-leitura o valor DWORD atual de um SettingID no Base Profile da NVIDIA (ou null se no padrão/ausente).</summary>
    public static uint? LerValorBaseProfile(uint settingId)
    {
        var api = TentarCarregarApi();
        if (api is null) return null;

        IntPtr hSession = IntPtr.Zero;
        IntPtr buf = Marshal.AllocHGlobal(TamanhoNvdrsSettingV1);
        try
        {
            LimparMemoria(buf, TamanhoNvdrsSettingV1);
            Marshal.WriteInt32(buf, 0, unchecked((int)VersaoNvdrsSettingV1));

            if (api.CreateSession(out hSession) != 0 || hSession == IntPtr.Zero) return null;
            if (api.LoadSettings(hSession) != 0) return null;
            if (api.GetBaseProfile(hSession, out var hProfile) != 0 || hProfile == IntPtr.Zero) return null;
            if (api.GetSetting(hSession, hProfile, settingId, buf) != 0) return null;

            return unchecked((uint)Marshal.ReadInt32(buf, OffsetU32CurrentValue));
        }
        catch { return null; }
        finally
        {
            Marshal.FreeHGlobal(buf);
            if (hSession != IntPtr.Zero) { try { api.DestroySession(hSession); } catch { } }
        }
    }

    /// <summary>
    /// Grava os parâmetros do perfil escolhido diretamente no *Base Profile* do driver NVIDIA via `NvAPI_DRS_SetSetting` + `SaveSettings`.
    /// As mudanças aparecem imediatamente no NVIDIA Profile Inspector e no Painel de Controle da NVIDIA.
    /// </summary>
    public static ResultadoDrsNvidia AplicarNoBaseProfile(string perfilId)
    {
        var api = TentarCarregarApi();
        if (api is null)
            return new(false, false, 0, 0, "Driver NVIDIA (nvapi64.dll) não encontrado nesta máquina — apenas os ajustes de sistema e o arquivo .nip foram gerados.");

        var parametros = ObterParametrosPerfil(perfilId);
        IntPtr hSession = IntPtr.Zero;
        IntPtr buf = Marshal.AllocHGlobal(TamanhoNvdrsSettingV1);
        try
        {
            int rcCreate = api.CreateSession(out hSession);
            if (rcCreate != 0 || hSession == IntPtr.Zero)
                return new(true, false, 0, 0, $"Não foi possível abrir sessão DRS da NVIDIA (código {rcCreate}).");

            int rcLoad = api.LoadSettings(hSession);
            if (rcLoad != 0)
                return new(true, false, 0, 0, $"Não foi possível carregar o banco DRS da NVIDIA (código {rcLoad}).");

            int rcBase = api.GetBaseProfile(hSession, out var hProfile);
            if (rcBase != 0 || hProfile == IntPtr.Zero)
                return new(true, false, 0, 0, $"Não foi possível localizar o Base Profile da NVIDIA (código {rcBase}).");

            // Primeiro limpa overrides anteriores dos IDs gerenciados para garantir transição limpa entre perfis
            foreach (var idGerenciado in TodosSettingIdsGerenciados)
                api.RestoreDefaultSetting(hSession, hProfile, idGerenciado);

            int aplicados = 0, ignorados = 0;
            foreach (var p in parametros)
            {
                PreencherStructDword(buf, p.SettingId, p.Valor);
                int rcSet = api.SetSetting(hSession, hProfile, buf);
                if (rcSet == 0) aplicados++;
                else ignorados++;
            }

            int rcSave = api.SaveSettings(hSession);
            if (rcSave != 0)
                return new(true, false, aplicados, ignorados, $"Falha ao salvar o perfil no banco DRS da NVIDIA (código {rcSave}). Verifique permissões de Administrador.");

            return new(true, aplicados > 0, aplicados, ignorados,
                $"Base Profile da NVIDIA atualizado via NvAPI ({aplicados} parâmetro(s) gravados no driver — visível no NVIDIA Profile Inspector).");
        }
        catch (Exception ex)
        {
            return new(true, false, 0, 0, "Falha ao comunicar com nvapi64.dll: " + ex.Message);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
            if (hSession != IntPtr.Zero) { try { api.DestroySession(hSession); } catch { } }
        }
    }

    /// <summary>Restaura todos os SettingIDs gerenciados pelo Sevenzin no *Base Profile* para o padrão de fábrica da NVIDIA.</summary>
    public static ResultadoDrsNvidia RestaurarPadroesBaseProfile()
    {
        var api = TentarCarregarApi();
        if (api is null)
            return new(false, false, 0, 0, "nvapi64.dll indisponível.");

        IntPtr hSession = IntPtr.Zero;
        try
        {
            if (api.CreateSession(out hSession) != 0 || hSession == IntPtr.Zero)
                return new(true, false, 0, 0, "Falha ao abrir sessão DRS.");
            if (api.LoadSettings(hSession) != 0)
                return new(true, false, 0, 0, "Falha ao carregar banco DRS.");
            if (api.GetBaseProfile(hSession, out var hProfile) != 0 || hProfile == IntPtr.Zero)
                return new(true, false, 0, 0, "Falha ao obter Base Profile.");

            int restaurados = 0;
            foreach (var id in TodosSettingIdsGerenciados)
            {
                if (api.RestoreDefaultSetting(hSession, hProfile, id) == 0)
                    restaurados++;
            }

            int rcSave = api.SaveSettings(hSession);
            return rcSave == 0
                ? new(true, true, restaurados, 0, $"Base Profile da NVIDIA restaurado ao padrão de fábrica ({restaurados} parâmetro(s) limpos).")
                : new(true, false, restaurados, 0, $"Não foi possível salvar a restauração DRS (código {rcSave}).");
        }
        catch (Exception ex)
        {
            return new(true, false, 0, 0, ex.Message);
        }
        finally
        {
            if (hSession != IntPtr.Zero) { try { api.DestroySession(hSession); } catch { } }
        }
    }

    public static void PreencherStructDword(IntPtr buf, uint settingId, uint valor)
    {
        LimparMemoria(buf, TamanhoNvdrsSettingV1);
        Marshal.WriteInt32(buf, 0, unchecked((int)VersaoNvdrsSettingV1));
        Marshal.WriteInt32(buf, OffsetSettingId, unchecked((int)settingId));
        Marshal.WriteInt32(buf, OffsetSettingType, 0);          // NVDRS_DWORD_TYPE = 0
        Marshal.WriteInt32(buf, OffsetSettingLocation, 0);      // NVDRS_CURRENT_PROFILE_LOCATION = 0
        Marshal.WriteInt32(buf, OffsetIsCurrentPredefined, 0);  // valor definido pelo usuário
        Marshal.WriteInt32(buf, OffsetIsPredefinedValid, 0);
        Marshal.WriteInt32(buf, OffsetU32CurrentValue, unchecked((int)valor));
    }

    static readonly byte[] BufferZeros = new byte[TamanhoNvdrsSettingV1];

    static void LimparMemoria(IntPtr ptr, int tamanho) =>
        Marshal.Copy(BufferZeros, 0, ptr, tamanho);
}
