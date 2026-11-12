using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Leituras só-leitura de estado de segurança da plataforma (Secure Boot, TPM). Nunca altera nada.</summary>
public static class SegurancaSistema
{
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
}
