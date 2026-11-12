using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Um jogo detectado no disco (Steam/Epic). Só leitura: nenhum processo é iniciado.</summary>
public sealed record JogoDetectado(string Plataforma, string Nome, string AppId, string PastaInstalacao, string? Executavel);

/// <summary>Descoberta de jogos instalados — paridade com o original (06_fluxos F10: SteamAppId/KnownPaths,
/// EpicAppName; 04_funcionalidades §9). Lê APENAS arquivos e registro (libraryfolders.vdf, appmanifest_*.acf,
/// manifestos .item da Epic). Launcher ausente → lista vazia, nunca erro. Nada é executado.</summary>
public static class DescobridorJogos
{
    /// <summary>Caminho do Steam no registro (HKCU\Software\Valve\Steam\SteamPath), ou null se ausente.</summary>
    public static string? SteamPathDeRegistro()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return k?.GetValue("SteamPath") as string;
        }
        catch { return null; }
    }

    /// <summary>Parse do libraryfolders.vdf: devolve as pastas de biblioteca (valor "path"). Função pura.</summary>
    public static List<string> AnalisarLibraryFolders(string vdf)
    {
        var pastas = new List<string>();
        foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var caminho = m.Groups[1].Value.Replace(@"\\", @"\");
            if (caminho.Length > 2) pastas.Add(caminho);
        }
        return pastas.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Parse de um appmanifest_*.acf: (AppId, Nome, Installdir). Função pura.</summary>
    public static (string AppId, string Nome, string Installdir)? AnalisarAppManifest(string acf)
    {
        var nome = Regex.Match(acf, "\"name\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        var dir = Regex.Match(acf, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (!nome.Success || !dir.Success) return null;
        var appid = Regex.Match(acf, "\"appid\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return (appid.Success ? appid.Groups[1].Value : "", nome.Groups[1].Value, dir.Groups[1].Value);
    }

    /// <summary>Detecta jogos Steam instalados (manifestos .acf de todas as bibliotecas).
    /// <paramref name="steamPath"/> injetável para teste; null = lê do registro.</summary>
    public static List<JogoDetectado> DetectarSteam(string? steamPath = null)
    {
        var jogos = new List<JogoDetectado>();
        try
        {
            var raiz = steamPath ?? SteamPathDeRegistro();
            if (raiz is null) return jogos;
            var vdf = Path.Combine(raiz, "steamapps", "libraryfolders.vdf");
            var bibliotecas = File.Exists(vdf)
                ? AnalisarLibraryFolders(File.ReadAllText(vdf))
                : new List<string> { raiz }; // sem libraryfolders, a biblioteca padrão é a própria pasta

            foreach (var lib in bibliotecas)
            {
                var pastaManifestos = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(pastaManifestos)) continue;
                foreach (var acf in Directory.GetFiles(pastaManifestos, "appmanifest_*.acf"))
                {
                    var manifest = AnalisarAppManifest(File.ReadAllText(acf));
                    if (manifest is not { } m || m.Nome.Length == 0) continue;
                    var pastaJogo = Path.Combine(pastaManifestos, "common", m.Installdir);
                    jogos.Add(new JogoDetectado("Steam", m.Nome, m.AppId, pastaJogo, ExecutavelPrincipal(pastaJogo, m.Installdir)));
                }
            }
        }
        catch { /* Steam ausente/corrompido → lista vazia */ }
        return jogos.OrderBy(j => j.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Parse de um manifesto .item da Epic: (AppName, DisplayName, InstallLocation, LaunchExecutable).
    /// Função pura.</summary>
    public static (string AppName, string Nome, string InstallLocation, string Executavel)? AnalisarManifestoEpic(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            string Get(string n) => raiz.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var nome = Get("DisplayName");
            if (nome.Length == 0) return null;
            return (Get("AppName"), nome, Get("InstallLocation"), Get("LaunchExecutable"));
        }
        catch { return null; }
    }

    /// <summary>Detecta jogos Epic instalados via manifestos .item em %ProgramData%\Epic.
    /// <paramref name="pastaManifestos"/> injetável para teste; null = caminho padrão.</summary>
    public static List<JogoDetectado> DetectarEpic(string? pastaManifestos = null)
    {
        var jogos = new List<JogoDetectado>();
        try
        {
            var pasta = pastaManifestos ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(pasta)) return jogos;
            foreach (var item in Directory.GetFiles(pasta, "*.item"))
            {
                var m = AnalisarManifestoEpic(File.ReadAllText(item));
                if (m is not { } manifesto) continue;
                string? executavel = null;
                if (manifesto.InstallLocation.Length > 0 && manifesto.Executavel.Length > 0)
                {
                    var caminho = Path.Combine(manifesto.InstallLocation, manifesto.Executavel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(caminho)) executavel = caminho;
                }
                jogos.Add(new JogoDetectado("Epic", manifesto.Nome, manifesto.AppName, manifesto.InstallLocation, executavel));
            }
        }
        catch { /* Epic ausente → lista vazia */ }
        return jogos.OrderBy(j => j.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Detecta Steam + Epic e devolve tudo junto.</summary>
    public static List<JogoDetectado> DetectarTudo()
    {
        var todos = new List<JogoDetectado>();
        todos.AddRange(DetectarSteam());
        todos.AddRange(DetectarEpic());
        return todos;
    }

    /// <summary>Escolhe o executável principal de uma pasta de jogo. Função pura (testável):
    /// prioriza o .exe cujo nome casa com o nome da pasta; depois o maior arquivo; ignora
    /// desinstaladores, redistribuíveis e similares. null quando não há candidato.</summary>
    public static string? ExecutavelPrincipal(string pasta, string nomePasta)
    {
        if (!Directory.Exists(pasta)) return null;
        var ignorados = new[] { "unins", "uninstall", "redist", "setup", "crash", "dxsetup", "vcredist" };
        var candidatos = Directory.GetFiles(pasta, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(e => !ignorados.Any(i => Path.GetFileNameWithoutExtension(e).Contains(i, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (candidatos.Count == 0) return null;
        var exato = candidatos.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e).Equals(nomePasta, StringComparison.OrdinalIgnoreCase));
        return exato ?? candidatos.OrderByDescending(e => new FileInfo(e).Length).First();
    }
}
