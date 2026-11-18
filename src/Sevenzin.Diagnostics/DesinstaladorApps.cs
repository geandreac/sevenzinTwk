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
    public static IReadOnlyList<string> ArgumentosEdge => new[] { "--uninstall", "--force-uninstall", "--system-level", "--verbose-logging" };
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

    /// <summary>Desinstala o Edge usando o setup oficial. Fora do Espaço Econômico Europeu a Microsoft recusa a remoção
    /// (o setup sai com código de erro); nesse caso libera a remoção TEMPORARIAMENTE e tenta de novo, desfazendo a
    /// liberação no fim, com sucesso ou não.</summary>
    public static async Task<ResultadoDesinstalacao> DesinstalarEdgeAsync(CancellationToken ct = default)
    {
        string? setup = EncontrarEdgeSetup(@"C:\Program Files (x86)\Microsoft\Edge\Application")
            ?? EncontrarEdgeSetup(@"C:\Program Files\Microsoft\Edge\Application");
        if (setup is null) return new(false, "Setup do Edge não encontrado (Edge pode já estar desinstalado).");
        var r = await TentarComLiberacaoAsync(() => ExecutarAsync(setup, ArgumentosEdge, ct), LiberarRemocaoEdge);
        return r.Ok ? r : r with { Detalhe = r.Detalhe + " A Microsoft bloqueia a remoção do Edge em algumas regiões. Detalhes no log C:\\Windows\\Temp\\msedge_installer.log." };
    }

    /// <summary>Tenta; se falhar, aplica a liberação, tenta de novo e sempre desfaz a liberação. Testável.</summary>
    public static async Task<ResultadoDesinstalacao> TentarComLiberacaoAsync(Func<Task<ResultadoDesinstalacao>> tentar, Func<IDisposable> liberar)
    {
        var r = await tentar();
        if (r.Ok) return r;
        using (liberar()) return await tentar();
    }

    /// <summary>Os dois sinais que o setup do Edge consulta antes de aceitar a remoção: a chave EdgeUpdateDev\AllowUninstall
    /// e a presença do Edge legado em SystemApps. Cria só o que não existia e apaga isso mesmo no Dispose.</summary>
    static IDisposable LiberarRemocaoEdge()
    {
        const string chave = @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev";
        var legado = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SystemApps", "Microsoft.MicrosoftEdge_8wekyb3d8bbwe", "MicrosoftEdge.exe");
        bool criouValor = false, criouArquivo = false;
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(chave);
            if (k.GetValue("AllowUninstall") is null) { k.SetValue("AllowUninstall", 1, Microsoft.Win32.RegistryValueKind.DWord); criouValor = true; }
        }
        catch { }
        try
        {
            if (!File.Exists(legado)) { Directory.CreateDirectory(Path.GetDirectoryName(legado)!); File.WriteAllBytes(legado, Array.Empty<byte>()); criouArquivo = true; }
        }
        catch { }
        return new Desfazer(() =>
        {
            if (criouValor) try { using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(chave, writable: true); k?.DeleteValue("AllowUninstall", false); } catch { }
            if (criouArquivo) try { File.Delete(legado); } catch { }
        });
    }

    sealed class Desfazer(Action acao) : IDisposable { public void Dispose() => acao(); }

    /// <summary>Desinstala o OneDrive usando o setup oficial do Windows.</summary>
    public static async Task<ResultadoDesinstalacao> DesinstalarOneDriveAsync(CancellationToken ct = default)
    {
        string? setup = EncontrarOneDriveSetup(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (setup is null) return new(false, "OneDriveSetup.exe não encontrado (OneDrive pode já estar desinstalado).");
        return await ExecutarAsync(setup, ArgumentosOneDrive, ct);
    }
}
