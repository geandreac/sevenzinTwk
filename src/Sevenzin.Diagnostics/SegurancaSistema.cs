using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Leituras só-leitura de estado de segurança da plataforma (Secure Boot, TPM, UAC, Defender,
/// SmartScreen, HVCI, mitigações, Windows Update). Nunca altera nada — quem altera é o MotorTweaks,
/// sempre com prévia/confirmação/desfazer.</summary>
public static class SegurancaSistema
{
    /// <summary>Interpreta um DWORD lido: null = chave ausente (indeterminado); senão, true quando
    /// o valor é igual ao "ativo". Função pura (testável).</summary>
    public static bool? Interpretar(int? dword, int ativoQuando) =>
        dword is null ? null : dword == ativoQuando;

    static int? LerDword(string caminho, string nome)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(caminho);
            return k?.GetValue(nome) is int v ? v : null;
        }
        catch { return null; }
    }

    /// <summary>true = Secure Boot ativo; false = desativado; null = indeterminado (BIOS legada / sem UEFI).</summary>
    public static bool? SecureBootAtivo()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            return k?.GetValue("UEFISecureBootEnabled") is int v ? v == 1 : (bool?)null;
        }
        catch { return null; }
    }

    /// <summary>true = TPM habilitado; false = presente mas desabilitado; null = ausente/indeterminado.</summary>
    public static bool? TpmHabilitado()
    {
        try
        {
            foreach (var mo in Wmi.Query("SELECT IsEnabled_InitialValue FROM Win32_Tpm", @"root\CIMV2\Security\MicrosoftTpm"))
                return mo["IsEnabled_InitialValue"] as bool?;
            return null;
        }
        catch { return null; }
    }

    /// <summary>true = UAC ativo (EnableLUA ≠ 0); null = chave ausente (UAC ativo por padrão — a UI trata como ativo).</summary>
    public static bool? UacHabilitado() => Interpretar(LerDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA"), 1);

    /// <summary>true = proteção em tempo real do Defender ativa (DisableRealtimeMonitoring ≠ 1); null = indeterminado.</summary>
    public static bool? DefenderTempoRealAtivo() => Interpretar(LerDword(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"), 0);

    /// <summary>true = SmartScreen ativo (política EnableSmartScreen ≠ 0); null = chave ausente (ativo por padrão).</summary>
    public static bool? SmartScreenAtivo() => Interpretar(LerDword(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen"), 1);

    /// <summary>true = Isolamento de Núcleo (HVCI) ativo (Enabled=1); null = indeterminado.</summary>
    public static bool? HvciAtivo() => Interpretar(LerDword(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"), 1);

    /// <summary>true = mitigações Spectre/Meltdown ativas (FeatureSettingsOverride=0); null = chave ausente (ativas por padrão).</summary>
    public static bool? MitigacoesAtivas() => Interpretar(LerDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride"), 0);

    /// <summary>true = atualizações automáticas ativas (NoAutoUpdate=0); null = chave ausente (ativas por padrão).</summary>
    public static bool? AtualizacoesAutomaticasAtivas() => Interpretar(LerDword(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate"), 0);
}
