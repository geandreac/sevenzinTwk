using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>
/// Garante que o SevenzinTwks rode sempre elevado como Administrador desde a partida
/// e auxilia no relançamento imediato em caso de recuperação automática.
/// </summary>
public static class Elevacao
{
    public static bool EhAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Se o processo foi aberto sem elevação (ex.: via host externo), relança imediatamente como Administrador
    /// na partida, evitando qualquer necessidade de fechar o aplicativo depois durante um ajuste avançado.
    /// </summary>
    public static bool GarantirAdminNaPartida(string[] args)
    {
        if (EhAdmin()) return false;
        try
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!)
            {
                Verb = "runas",
                UseShellExecute = true,
                Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
            };
            RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
            Process.Start(psi);
            Application.Current.Shutdown();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Reabre o Sevenzin como administrador caso ainda não esteja elevado.</summary>
    public static bool ReabrirComoAdmin()
    {
        if (EhAdmin()) return true;
        try
        {
            RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true });
            Application.Current.Shutdown();
            return true;
        }
        catch { return false; }
    }

    /// <summary>Relança o aplicativo automaticamente em modo rápido após uma falha inesperada.</summary>
    public static void RelancarParaRecuperacao()
    {
        // Uma tentativa só: se esta instância JÁ é a reabertura e caiu de novo, para aqui — senão a mesma falha
        // reabre o app sem fim (um processo por segundo, cada um deixando um ícone na bandeja).
        if (Environment.GetCommandLineArgs().Contains("--recuperado")) return;
        try
        {
            var caminho = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(caminho)) return;
            Process.Start(new ProcessStartInfo(caminho)
            {
                Arguments = "--recuperado",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
