using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>
/// Auxilia na detecção de inicialização automática com o Windows e controle de flags de linha de comando.
/// </summary>
public static class StartupHelper
{
    public const string ArgAutostart = "--autostart";
    public const string ArgTray = "--tray";
    public const string ArgMinimized = "--minimized";

    /// <summary>
    /// Retorna true se a execução atual foi disparada para iniciar minimizada na bandeja do sistema
    /// (via flag de linha de comando ou detecção de logon recente com preferência ativada).
    /// </summary>
    public static bool DeveIniciarMinimizado(string[]? args, bool prefIniciarMinimizado = true)
    {
        if (args is not null)
        {
            foreach (var a in args)
            {
                if (a.Equals(ArgAutostart, StringComparison.OrdinalIgnoreCase) ||
                    a.Equals(ArgTray, StringComparison.OrdinalIgnoreCase) ||
                    a.Equals(ArgMinimized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Se a preferência de iniciar minimizado estiver ativa e o sistema acabou de ligar (< 3 minutos),
        // trata como inicialização com o Windows mesmo se agendador antigo rodou sem a flag.
        if (prefIniciarMinimizado && Environment.TickCount64 < 180_000)
        {
            return true;
        }

        return false;
    }
}
