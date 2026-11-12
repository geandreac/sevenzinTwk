using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Sevenzin.Wpf;

/// <summary>Elevação sob demanda (§21.7): o app roda sem administrador; só pede UAC quando o usuário aplica
/// um ajuste de máquina (HKLM), reabrindo a si mesmo elevado.</summary>
public static class Elevacao
{
    public static bool EhAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Reabre o Sevenzin como administrador e fecha a instância atual. False se o usuário cancelar o UAC.</summary>
    public static bool ReabrirComoAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true });
            Application.Current.Shutdown();
            return true;
        }
        catch { return false; } // UAC cancelado
    }
}
