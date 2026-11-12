using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Resultado de uma desinstalação assistida.</summary>
public sealed record ResultadoDesinstalacao(bool Ok, string Detalhe);

/// <summary>Desinstalação assistida do Edge e do OneDrive (paridade com o conjunto antes excluído —
/// EdgeBrowserUninstall.ps1 / OneDriveUninstall.ps1 do original, agora aprovado). Os instaladores
/// OFICIAIS da Microsoft são executados com os argumentos de desinstalação; a UI pede confirmação
/// explícita antes. Nada roda sem o usuário.</summary>
public static class DesinstaladorApps
{
    /// <summary>Localiza o setup.exe do Edge na pasta de instalação (pasta/<versão>/Installer/setup.exe).
    /// Função pura (testável).</summary>
    public static string? EncontrarEdgeSetup(string pastaEdge)
    {
        try
        {
            if (!Directory.Exists(pastaEdge)) return null;
            foreach (var versao in Directory.GetDirectories(pastaEdge))
            {
                var setup = Path.Combine(versao, "Installer", "setup.exe");
                if (File.Exists(setup)) return setup;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Localiza o OneDriveSetup.exe (SysWOW64 primeiro, depois System32). Função pura (testável).</summary>
    public static string? EncontrarOneDriveSetup(string pastaWindows)
    {
        var syswow = Path.Combine(pastaWindows, "SysWOW64", "OneDriveSetup.exe");
        if (File.Exists(syswow)) return syswow;
        var system = Path.Combine(pastaWindows, "System32", "OneDriveSetup.exe");
        return File.Exists(system) ? system : null;
    }

    /// <summary>Argumentos de desinstalação do Edge (silenciosa, nível de sistema) e do OneDrive.</summary>
    public static IReadOnlyList<string> ArgumentosEdge => new[] { "--uninstall", "--force-uninstall", "--system-level" };
    public static IReadOnlyList<string> ArgumentosOneDrive => new[] { "/uninstall" };

    /// <summary>Executa o desinstalador. NUNCA chame sem o usuário ter confirmado.</summary>
    public static async Task<ResultadoDesinstalacao> ExecutarAsync(string executavel, IReadOnlyList<string> argumentos, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executavel)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in argumentos) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return new(false, "Não foi possível iniciar o desinstalador.");
            await p.StandardOutput.ReadToEndAsync(ct);
            var erro = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0
                ? new(true, "Desinstalação concluída.")
                : new(false, $"O desinstalador terminou com código {p.ExitCode}." + (erro.Length > 0 ? $" {erro.Trim()}" : ""));
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }

    /// <summary>Desinstala o Edge usando o setup oficial. Devolve falha informativa quando o setup não é encontrado.</summary>
    public static async Task<ResultadoDesinstalacao> DesinstalarEdgeAsync(CancellationToken ct = default)
    {
        string? setup = EncontrarEdgeSetup(@"C:\Program Files (x86)\Microsoft\Edge\Application")
            ?? EncontrarEdgeSetup(@"C:\Program Files\Microsoft\Edge\Application");
        if (setup is null) return new(false, "Setup do Edge não encontrado (Edge pode já estar desinstalado).");
        return await ExecutarAsync(setup, ArgumentosEdge, ct);
    }

    /// <summary>Desinstala o OneDrive usando o setup oficial do Windows.</summary>
    public static async Task<ResultadoDesinstalacao> DesinstalarOneDriveAsync(CancellationToken ct = default)
    {
        string? setup = EncontrarOneDriveSetup(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (setup is null) return new(false, "OneDriveSetup.exe não encontrado (OneDrive pode já estar desinstalado).");
        return await ExecutarAsync(setup, ArgumentosOneDrive, ct);
    }
}
