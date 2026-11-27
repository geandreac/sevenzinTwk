using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Um utilitário do catálogo: origem OFICIAL, ícone local (`Assets/Apps/{Icone}.png`), URL HTTPS oficial
/// de download (ou API GitHub Releases oficial), ID opcional do `winget` oficial para fallback de mirror, e o
/// que validar antes de executar. Assinatura Authenticode é sempre exigida. <c>SoPagina</c> (com o motivo) = sem download
/// automático: o item só abre a página oficial (ex.: executável sem assinatura ou site que bloqueia downloads diretos).</summary>
public sealed record Utilitario(
    string Nome,
    string Descricao,
    string UrlOficial,
    string? UrlDownload,
    string? PublisherEsperado,
    string? Sha256Esperado,
    string Categoria = "Sistema",
    string Icone = "hdd",
    string? WingetId = null,
    string? SoPagina = null);

public enum ResultadoVerificacao { Ok, HashNaoConfere, SemAssinatura, PublisherDiferente }

/// <summary>Verificação de um instalador baixado (§21.5/21.7): hash fixado (se houver) e assinatura
/// Authenticode do publisher esperado. Sem isso, o instalador NÃO é oferecido para execução.</summary>
public static class VerificadorDownload
{
    public static string Sha256(string arquivo)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(arquivo);
        return Convert.ToHexString(sha.ComputeHash(fs)); // hex maiúsculo
    }

    /// <summary>Assunto do certificado que assinou o arquivo (Authenticode embutido), ou null se não assinado.</summary>
    public static string? AssinaturaPublisher(string arquivo)
    {
        try { return X509Certificate.CreateFromSignedFile(arquivo).Subject; }
        catch { return null; }
    }

    /// <param name="obterPublisher">injetável para teste; por padrão lê a assinatura real.</param>
    public static ResultadoVerificacao Verificar(string arquivo, Utilitario u, Func<string, string?>? obterPublisher = null)
    {
        obterPublisher ??= AssinaturaPublisher;
        if (u.Sha256Esperado is { } esperado && !Sha256(arquivo).Equals(esperado, StringComparison.OrdinalIgnoreCase))
            return ResultadoVerificacao.HashNaoConfere;
        var pub = obterPublisher(arquivo);
        if (string.IsNullOrEmpty(pub)) return ResultadoVerificacao.SemAssinatura;
        if (u.PublisherEsperado is { } pe && !pub.Contains(pe, StringComparison.OrdinalIgnoreCase))
            return ResultadoVerificacao.PublisherDiferente;
        return ResultadoVerificacao.Ok;
    }
}

/// <summary>Progresso detalhado de um download (bytes, total, velocidade e ETA), para a UI.</summary>
public sealed record ProgressoDownload(long BytesRecebidos, long TotalBytes, double BytesPorSegundo, TimeSpan? TempoRestante)
{
    public double? Percentual => TotalBytes > 0 ? (double)BytesRecebidos / TotalBytes : null;
}

/// <summary>Baixa um instalador da URL oficial para um arquivo local com STAGING: grava em
/// <c>destino.part</c> e move atomicamente só após o fim. Em falha, o arquivo de destino nunca é
/// corrompido (o .part fica descartável). Verifica espaço livre antes. Não executa nada.</summary>
public static class DownloaderInstalador
{
    /// <summary>Verifica se há espaço livre para o download. Função pura (testável).</summary>
    public static bool EspacoSuficiente(string pasta, long bytesNecessarios)
    {
        try
        {
            var raiz = Path.GetPathRoot(Path.GetFullPath(pasta));
            return string.IsNullOrEmpty(raiz) || new DriveInfo(raiz).AvailableFreeSpace >= bytesNecessarios;
        }
        catch { return true; }
    }

    /// <param name="handler">injetável para teste; por padrão usa HttpClient real.</param>
    public static async Task BaixarAsync(string url, string destino, IProgress<ProgressoDownload>? progresso = null,
        CancellationToken ct = default, HttpMessageHandler? handler = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
        var part = destino + ".part";
        try
        {
            using var http = handler is null ? CriarClienteHttp() : new HttpClient(handler);
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1;
            if (total > 0 && !EspacoSuficiente(Path.GetDirectoryName(Path.GetFullPath(destino))!, total))
                throw new IOException("Espaço em disco insuficiente para este download.");

            await using var origem = await resp.Content.ReadAsStreamAsync(ct);
            await using var arquivo = File.Create(part);
            var buffer = new byte[81920];
            long lido = 0; int n;
            var crono = Stopwatch.StartNew();
            while ((n = await origem.ReadAsync(buffer, ct)) > 0)
            {
                await arquivo.WriteAsync(buffer.AsMemory(0, n), ct);
                lido += n;
                if (progresso is not null)
                {
                    double v = crono.Elapsed.TotalSeconds > 0 ? lido / crono.Elapsed.TotalSeconds : 0;
                    TimeSpan? eta = v > 0 && total > 0 ? TimeSpan.FromSeconds((total - lido) / v) : null;
                    progresso.Report(new ProgressoDownload(lido, total, v, eta));
                }
            }
            await arquivo.FlushAsync(ct);
            arquivo.Dispose();
            File.Move(part, destino, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(part) && !File.Exists(destino)) File.Delete(part); } catch { }
            throw;
        }
    }

    static HttpClient CriarClienteHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SevenzinTwks/2.0");
        return c;
    }

    /// <summary>Resolve URL oficial (incluindo API GitHub Releases), faz download com fallback oficial via `winget download`
    /// quando o mirror exige sessão web e, se for pacote `.zip`, extrai para subpasta isolada devolvendo o `.exe`/`.msi` alvo.</summary>
    public static async Task<string> ObterInstaladorProntoAsync(
        Utilitario u,
        string pastaBase,
        IProgress<ProgressoDownload>? progresso = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(u.UrlDownload))
            throw new InvalidOperationException("URL de download não configurada para este item.");

        Directory.CreateDirectory(pastaBase);
        var slug = Regex.Replace(u.Nome.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

        string? caminhoBaixado = null;
        Exception? erroHttp = null;
        try
        {
            var (urlReal, nomeSugerido) = await ResolverUrlRealAsync(u.UrlDownload, slug, ct);
            var destino = Path.Combine(pastaBase, nomeSugerido);
            await BaixarAsync(urlReal, destino, progresso, ct);
            if (EhBinarioInstaladorOuZip(destino))
                caminhoBaixado = destino;
            else
            {
                try { File.Delete(destino); } catch { }
                erroHttp = new InvalidDataException("O servidor oficial retornou uma página HTML em vez do binário direto.");
            }
        }
        catch (Exception ex)
        {
            erroHttp = ex;
        }

        if (caminhoBaixado is null && !string.IsNullOrWhiteSpace(u.WingetId))
        {
            caminhoBaixado = await BaixarViaWingetOficialAsync(u.WingetId!, Path.Combine(pastaBase, slug + "-winget"), ct);
        }

        if (caminhoBaixado is null)
            throw erroHttp ?? new IOException($"Não foi possível obter o instalador direto de '{u.Nome}'.");

        if (EhArquivoZip(caminhoBaixado))
        {
            var pastaExtraida = Path.Combine(pastaBase, slug + "-pkg");
            if (Directory.Exists(pastaExtraida)) Directory.Delete(pastaExtraida, recursive: true);
            ZipFile.ExtractToDirectory(caminhoBaixado, pastaExtraida, overwriteFiles: true);
            var exe = Directory.GetFiles(pastaExtraida, "*.exe", SearchOption.AllDirectories)
                .OrderByDescending(f => f.Contains("64", StringComparison.OrdinalIgnoreCase) || f.Contains("setup", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault()
                ?? Directory.GetFiles(pastaExtraida, "*.msi", SearchOption.AllDirectories).FirstOrDefault();
            if (exe is null)
                throw new InvalidDataException("Nenhum executável (.exe/.msi) encontrado dentro do arquivo .zip baixado.");
            return exe;
        }

        return caminhoBaixado;
    }

    static async Task<(string Url, string NomeArquivo)> ResolverUrlRealAsync(string urlConfigurada, string slug, CancellationToken ct)
    {
        if (urlConfigurada.StartsWith("https://api.github.com/repos/", StringComparison.OrdinalIgnoreCase))
        {
            using var http = CriarClienteHttp();
            var json = await http.GetStringAsync(urlConfigurada, ct);
            var escolhida = ExtrairAssetGitHubRelease(json);
            if (escolhida is null)
                throw new InvalidOperationException("Nenhum instalador (.exe/.msi/.zip) encontrado na última release oficial do GitHub.");
            return escolhida.Value;
        }

        var uri = new Uri(urlConfigurada);
        var nomeLocal = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(nomeLocal) || !nomeLocal.Contains('.'))
            nomeLocal = slug + ".exe";
        return (urlConfigurada, nomeLocal);
    }

    /// <summary>Seleciona o melhor asset Windows x64 a partir do JSON oficial do GitHub Releases. Puro/testável.</summary>
    public static (string Url, string NomeArquivo)? ExtrairAssetGitHubRelease(string jsonRelease)
    {
        if (string.IsNullOrWhiteSpace(jsonRelease)) return null;
        try
        {
            using var doc = JsonDocument.Parse(jsonRelease);
            if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            var candidatos = new List<(string Nome, string Url, int Peso)>();
            foreach (var a in assets.EnumerateArray())
            {
                var nome = a.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "";
                var url = a.TryGetProperty("browser_download_url", out var uEl) ? (uEl.GetString() ?? "") : "";
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || nome.Length == 0) continue;
                var lower = nome.ToLowerInvariant();
                if (lower.Contains("arm64") || lower.Contains("aarch64") || lower.Contains("armv7") || lower.Contains("linux") || lower.Contains("macos") || lower.Contains("darwin") || lower.EndsWith(".sig") || lower.EndsWith(".sha256"))
                    continue;

                int peso = 0;
                if (lower.EndsWith(".exe")) peso += 30;
                else if (lower.EndsWith(".msi")) peso += 25;
                else if (lower.EndsWith(".zip")) peso += 15;
                else continue;

                if (lower.Contains("x64") || lower.Contains("x86_64") || lower.Contains("amd64") || lower.Contains("win64") || lower.Contains("64bit")) peso += 10;
                if (lower.Contains("installer") || lower.Contains("setup")) peso += 5;
                candidatos.Add((nome, url, peso));
            }

            var melhor = candidatos.OrderByDescending(c => c.Peso).FirstOrDefault();
            return melhor.Url is not null ? (melhor.Url, melhor.Nome) : null;
        }
        catch { return null; }
    }

    static bool EhBinarioInstaladorOuZip(string caminho)
    {
        try
        {
            var fi = new FileInfo(caminho);
            if (!fi.Exists || fi.Length < 4096) return false;
            Span<byte> cab = stackalloc byte[4];
            using var fs = File.OpenRead(caminho);
            if (fs.Read(cab) < 4) return false;
            // MZ (PE .exe), PK\x03\x04 (.zip), D0CF11E0 (.msi Compound File)
            if (cab[0] == (byte)'M' && cab[1] == (byte)'Z') return true;
            if (cab[0] == (byte)'P' && cab[1] == (byte)'K' && cab[2] == 3 && cab[3] == 4) return true;
            if (cab[0] == 0xD0 && cab[1] == 0xCF && cab[2] == 0x11 && cab[3] == 0xE0) return true;
            return false;
        }
        catch { return false; }
    }

    static bool EhArquivoZip(string caminho)
    {
        try
        {
            if (caminho.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return true;
            Span<byte> cab = stackalloc byte[4];
            using var fs = File.OpenRead(caminho);
            return fs.Read(cab) == 4 && cab[0] == (byte)'P' && cab[1] == (byte)'K' && cab[2] == 3 && cab[3] == 4;
        }
        catch { return false; }
    }

    static async Task<string?> BaixarViaWingetOficialAsync(string wingetId, string pastaStage, CancellationToken ct)
    {
        if (!Regex.IsMatch(wingetId, @"^[A-Za-z0-9._+-]+$")) return null;
        try
        {
            if (Directory.Exists(pastaStage)) Directory.Delete(pastaStage, recursive: true);
            Directory.CreateDirectory(pastaStage);

            var psi = new ProcessStartInfo("winget",
                $"download --id {wingetId} --exact --download-directory \"{pastaStage}\" --accept-source-agreements --accept-package-agreements --disable-interactivity")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            await proc.WaitForExitAsync(ct);

            return Directory.GetFiles(pastaStage, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
        }
        catch { return null; }
    }
}

/// <summary>Catálogo de utilitários benignos com origem HTTPS oficial, ícone individual e "Baixar e verificar",
/// organizado por tema (<see cref="OrdemCategorias"/>). Nada é baixado ou executado sem confirmação do usuário e
/// validação de assinatura. Itens com <see cref="Utilitario.SoPagina"/> só abrem a página oficial.</summary>
public static class CatalogoUtilitarios
{
    public static readonly IReadOnlyList<Utilitario> Todos = new[]
    {
        // ── Plataformas de jogos ──
        new Utilitario("Steam", "Plataforma e biblioteca de jogos para PC (Valve).",
            "https://store.steampowered.com/about/", "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe", "Valve", null, "Jogos e comunidade", "logo_steam", "Valve.Steam"),
        new Utilitario("Epic Games Launcher", "Loja e launcher de jogos da Epic.",
            "https://store.epicgames.com/", "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/installer/download/EpicGamesLauncherInstaller.msi", "Epic Games", null, "Jogos e comunidade", "logo_epicgames", "EpicGames.EpicGamesLauncher"),
        new Utilitario("EA App", "Plataforma oficial de jogos da Electronic Arts.",
            "https://www.ea.com/ea-app", "https://origin-a.akamaihd.net/EA-Desktop-Client-Download/installer-releases/EAappInstaller.exe", "Electronic Arts", null, "Jogos e comunidade", "logo_eaapp", "ElectronicArts.EADesktop"),
        new Utilitario("Ubisoft Connect", "Launcher da Ubisoft.",
            "https://ubisoftconnect.com/", "https://ubi.li/4vxt9", "Ubisoft", null, "Jogos e comunidade", "logo_ubisoftconnect", "Ubisoft.Connect"),
        new Utilitario("Rockstar Games Launcher", "Launcher da Rockstar Games.",
            "https://socialclub.rockstargames.com/rockstar-games-launcher", "https://gamedownloads.rockstargames.com/public/installer/Rockstar-Games-Launcher.exe", "Rockstar", null, "Jogos e comunidade", "logo_rockstar", "RockstarGames.Launcher"),

        // ── Competitivo (CS2 e afins) ──
        new Utilitario("FACEIT", "Plataforma competitiva de partidas e anti-cheat.",
            "https://www.faceit.com/", "https://cdn.faceit.com/faceit/anticheat/FaceitAC_1.0.17.36.exe", "FACEIT", null, "Jogos e comunidade", "logo_faceit", "FACEITLtd.FACEITClient"),
        new Utilitario("Gamers Club", "Plataforma brasileira de partidas competitivas.",
            "https://gamersclub.com.br/", "https://anti-cheat.gamersclub.com.br/Gamers-Club-Anti-Cheat.exe", "EMAC LAB", null, "Jogos e comunidade", "logo_gamersclub", null),
        new Utilitario("ExitLag", "Otimizador de rota de rede para reduzir ping e perda de pacotes em jogos online (pago).",
            "https://www.exitlag.com/download", "https://www.exitlag.com/download", "Skowsand", null, "Jogos e comunidade", "logo_exitlag", null,
            SoPagina: "O site da ExitLag bloqueia downloads automáticos; baixe pela página oficial (o instalador é assinado por Skowsand Tecnologia)."),
        new Utilitario("vibranceGUI", "Ajusta a vibração digital (saturação) da GPU automaticamente quando o jogo abre (open source).",
            "https://vibrancegui.com/", "https://vibrancegui.com/", null, null, "Jogos e comunidade", "logo_vibrancegui", null,
            SoPagina: "O executável não tem assinatura digital, então o SevenzinTwks não o oferece para execução; baixe pela página oficial."),
        new Utilitario("MeasureSleep", "Mede a precisão real do timer do Windows (quanto um Sleep(1) dura de verdade), para comparar antes e depois de ajustes de timer (open source).",
            "https://github.com/valleyofdoom/TimerResolution", "https://github.com/valleyofdoom/TimerResolution", null, null, "Jogos e comunidade", "autocpuset_icon", null,
            SoPagina: "O MeasureSleep não tem assinatura digital, então o SevenzinTwks não o oferece para execução; baixe pela página oficial no GitHub."),

        // ── Jogos ──
        new Utilitario("VALORANT", "Jogo de tiro tático da Riot (com anti-cheat Vanguard).",
            "https://playvalorant.com/", "https://valorant.secure.dyn.riotcdn.net/channels/public/x/installer/current/live.live.br.exe", "Riot Games", null, "Jogos e comunidade", "logo_valorant", "RiotGames.Valorant.BR"),
        new Utilitario("League of Legends", "MOBA competitivo da Riot Games.",
            "https://www.leagueoflegends.com/", "https://lol.secure.dyn.riotcdn.net/channels/public/x/installer/current/live.br.exe", "Riot Games", null, "Jogos e comunidade", "logo_leagueoflegends", "RiotGames.LeagueOfLegends.BR"),
        new Utilitario("FiveM", "Plataforma multiplayer dedicada de roleplay e servidores customizados (Cfx.re).",
            "https://fivem.net/", "https://runtime.fivem.net/client/FiveM.exe", "CitizenFX", null, "Jogos e comunidade", "logo_fivem", "Cfx.re.FiveM"),
        new Utilitario("Minecraft Launcher", "Launcher oficial de Minecraft Java & Bedrock (Mojang/Microsoft).",
            "https://www.minecraft.net/download", "https://launcher.mojang.com/download/MinecraftInstaller.exe", "Microsoft", null, "Jogos e comunidade", "logo_minecraft", "Mojang.MinecraftLauncher"),

        // ── Comunicação ──
        new Utilitario("Discord", "Comunicação por voz, vídeo e comunidades para jogadores.",
            "https://discord.com/download", "https://discord.com/api/download?platform=win", "Discord", null, "Jogos e comunidade", "logo_discord", "Discord.Discord"),
        new Utilitario("TeamSpeak", "Comunicação por voz com servidores próprios.",
            "https://teamspeak.com/en/downloads/", "https://files.teamspeak-services.com/releases/client/3.6.2/TeamSpeak3-Client-win64-3.6.2.exe", "TeamSpeak", null, "Jogos e comunidade", "logo_teamspeak", "TeamSpeakSystems.TeamSpeakClient"),

        // ── Navegadores ──
        new Utilitario("Brave Browser", "Navegador com bloqueio de anúncios e rastreadores embutido.",
            "https://brave.com/", "https://laptop-updates.brave.com/latest/winx64", "Brave", null, "Aplicativos essenciais", "logo_brave", "Brave.Brave"),
        new Utilitario("Google Chrome", "Navegador do Google.",
            "https://www.google.com/chrome/", "https://dl.google.com/chrome/install/latest/chrome_installer.exe", "Google", null, "Aplicativos essenciais", "logo_googlechrome", "Google.Chrome"),
        new Utilitario("Mozilla Firefox", "Navegador open source da Mozilla (instalador completo em português).",
            "https://www.mozilla.org/firefox/", "https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=pt-BR", "Mozilla", null, "Aplicativos essenciais", "logo_firefox", "Mozilla.Firefox.pt-BR"),
        new Utilitario("Microsoft Edge", "Navegador da Microsoft.",
            "https://www.microsoft.com/edge", "https://go.microsoft.com/fwlink/?linkid=2108834&Channel=Stable&language=pt-br", "Microsoft", null, "Aplicativos essenciais", "logo_microsoftedge", "Microsoft.Edge"),

        // ── Desempenho ──
        new Utilitario("MSI Afterburner", "Overclock e monitoramento de GPU (MSI).",
            "https://www.msi.com/Landing/afterburner/graphics-cards", "https://download.msi.com/uti_exe/vga/MSIAfterburnerSetup.zip", "Micro-Star", null, "Desempenho e diagnóstico", "logo_msiafterburner", "Guru3D.Afterburner"),
        new Utilitario("Process Lasso", "Ajusta prioridade e afinidade de processos automaticamente (ProBalance) para manter o PC responsivo (Bitsum).",
            "https://bitsum.com/", "https://dl.bitsum.com/files/processlassosetup64.exe", "Bitsum", null, "Desempenho e diagnóstico", "logo_processlasso", "BitSum.ProcessLasso"),
        new Utilitario("Autoruns (Sysinternals)", "Lista tudo que inicia com o Windows, para revisar e desligar itens de inicialização (Microsoft).",
            "https://learn.microsoft.com/sysinternals/downloads/autoruns", "https://live.sysinternals.com/Autoruns64.exe", "Microsoft", null, "Desempenho e diagnóstico", "logo_autorun", "Microsoft.Sysinternals.Autoruns"),
        new Utilitario("CRU (Custom Resolution Utility)", "Cria resoluções e taxas de atualização personalizadas para o monitor (ToastyX). Avançado: valores errados podem deixar a tela preta até restaurar.",
            "https://www.monitortests.com/forum/Thread-Custom-Resolution-Utility-CRU", "https://www.monitortests.com/download/cru/cru-1.5.2.zip", null, null, "Desempenho e diagnóstico", "logo_cru", null,
            SoPagina: "Os executáveis do CRU não têm assinatura digital, então o SevenzinTwks não os oferece para execução; baixe pela página oficial."),

        // ── Monitoramento ──
        new Utilitario("CPU-Z", "Informações detalhadas de CPU, placa-mãe e memória (CPUID).",
            "https://www.cpuid.com/softwares/cpu-z.html", "https://download.cpuid.com/cpu-z/cpu-z_2.17-en.exe", "CPUID", null, "Desempenho e diagnóstico", "logo_cpuz", "CPUID.CPU-Z"),
        new Utilitario("HWiNFO", "Monitoramento de sensores (temperatura, clock, uso) de CPU/GPU/placa (REALiX).",
            "https://www.hwinfo.com/download/", "https://www.hwinfo.com/download/", "REALiX", null, "Desempenho e diagnóstico", "logo_hwinfo", "REALiX.HWiNFO"),
        new Utilitario("GPU-Z", "Informações e sensores da placa de vídeo (TechPowerUp).",
            "https://www.techpowerup.com/gpuz/", "https://www.techpowerup.com/gpuz/", "TechPowerUp", null, "Desempenho e diagnóstico", "logo_gpuz", "TechPowerUp.GPU-Z"),
        new Utilitario("CrystalDiskInfo", "Saúde e S.M.A.R.T. dos discos (Crystal Dew World).",
            "https://crystalmark.info/en/software/crystaldiskinfo/", "https://api.github.com/repos/hiyohiyo/CrystalDiskInfo/releases/latest", null, null, "Desempenho e diagnóstico", "hdd", "CrystalDewWorld.CrystalDiskInfo"),
        new Utilitario("HWMonitor", "Leitura de tensões, temperaturas e ventoinhas (CPUID).",
            "https://www.cpuid.com/softwares/hwmonitor.html", "https://download.cpuid.com/hwmonitor/hwmonitor_1.55.exe", "CPUID", null, "Desempenho e diagnóstico", "logo_hwmonitor", "CPUID.HWMonitor"),
        new Utilitario("Coreinfo (Sysinternals)", "Mostra o mapeamento de núcleos lógicos/físicos, cache e topologia (Microsoft).",
            "https://learn.microsoft.com/sysinternals/downloads/coreinfo", "https://live.sysinternals.com/Coreinfo64.exe", "Microsoft", null, "Desempenho e diagnóstico", "autocpuset_icon", "Microsoft.Sysinternals.Coreinfo"),
        new Utilitario("ZenTimings", "Tempos de memória em placas AMD (open source).",
            "https://zentimings.protonrom.com/", "https://api.github.com/repos/irusanov/ZenTimings/releases/latest", null, null, "Desempenho e diagnóstico", "logo_zentimings", "ivanrusanov.ZenTimings"),
        new Utilitario("Process Explorer (Sysinternals)", "Gerenciador de processos avançado da Microsoft (handles, árvore, CPU por thread).",
            "https://learn.microsoft.com/sysinternals/downloads/process-explorer", "https://live.sysinternals.com/procexp64.exe", "Microsoft", null, "Desempenho e diagnóstico", "logo_processexplorer", "Microsoft.Sysinternals.ProcessExplorer"),
        new Utilitario("LatencyMon", "Mede a latência DPC/ISR do sistema (Resplendence).",
            "https://www.resplendence.com/latencymon", "https://www.resplendence.com/download/LatencyMon.exe", "Resplendence", null, "Desempenho e diagnóstico", "logo_latencymon", "Resplendence.LatencyMon"),

        // ── Benchmark e estabilidade ──
        new Utilitario("CapFrameX", "Captura e análise avançada de frametime baseada em PresentMon.",
            "https://www.capframex.com/download", "https://api.github.com/repos/CXWorld/CapFrameX/releases/latest", null, null, "Desempenho e diagnóstico", "logo_capframex", "CXWorld.CapFrameX"),
        new Utilitario("OCAT", "Captura de FPS por PresentMon (GPUOpen/AMD).",
            "https://gpuopen.com/ocat/", "https://api.github.com/repos/GPUOpen-Tools/OCAT/releases/latest", "Advanced Micro Devices", null, "Desempenho e diagnóstico", "logo_ocat", null),
        new Utilitario("Prime95", "Teste de estabilidade de CPU/memória (GIMPS).",
            "https://www.mersenne.org/download/", "https://www.mersenne.org/download/software/v30/30.19/p95v3019b20.win64.zip", null, null, "Desempenho e diagnóstico", "logo_prime95", null),
        new Utilitario("Geekbench", "Benchmark de CPU/GPU multiplataforma (Primate Labs).",
            "https://www.geekbench.com/download/", "https://cdn.geekbench.com/Geekbench-6.3.0-WindowsSetup.exe", "Primate Labs", null, "Desempenho e diagnóstico", "logo_geek", "PrimateLabs.Geekbench.6"),
        new Utilitario("OCCT", "Teste de estabilidade e sensores (Ocbase).",
            "https://www.ocbase.com/", "https://www.ocbase.com/download/edition:Personal/os:Windows", "OCBASE", null, "Desempenho e diagnóstico", "logo_occt", "OCBASE.OCCT.Personal"),

        // ── Drivers e periféricos ──
        new Utilitario("NVCleanstall", "Instalação limpa do driver NVIDIA (TechPowerUp).",
            "https://www.techpowerup.com/download/techpowerup-nvcleanstall/", "https://www.techpowerup.com/download/techpowerup-nvcleanstall/", "TechPowerUp", null, "Drivers e componentes", "logo_nvcleanstall", "TechPowerUp.NVCleanstall"),
        new Utilitario("NVIDIA Control Panel", "Painel de Controle NVIDIA oficial: configurações 3D, resolução, G-SYNC e cor da tela.",
            "https://apps.microsoft.com/detail/9NF8H0H7WMLT", "https://apps.microsoft.com/detail/9NF8H0H7WMLT", "NVIDIA", null, "Drivers e componentes", "logo_nvidiacontrolpanel", null,
            SoPagina: "O Painel de Controle NVIDIA só é distribuído pela Microsoft Store, sem instalador próprio; o botão abre a página oficial dele na Store para instalar. Com o driver NVIDIA atual ele costuma se instalar sozinho, mas some se a Store estiver desativada ou se o driver foi instalado sem ele."),
        new Utilitario("DDU (Display Driver Uninstaller)", "Remove drivers de vídeo de forma limpa (Wagnardsoft).",
            "https://www.wagnardsoft.com/forums/viewforum.php?f=5", "https://www.wagnardsoft.com/DDU/download/DDU%20v18.0.8.9.exe", "Wagnardsoft", null, "Drivers e componentes", "logo_ddu", "Wagnardsoft.DisplayDriverUninstaller"),
        new Utilitario("Radeon Software Slimmer", "Instalação enxuta do driver AMD (open source).",
            "https://github.com/GSDragoon/RadeonSoftwareSlimmer", "https://api.github.com/repos/GSDragoon/RadeonSoftwareSlimmer/releases/latest", null, null, "Drivers e componentes", "logo_radeonslimmer", "GSDragoon.RadeonSoftwareSlimmer"),
        new Utilitario("DriverStore Explorer", "Gerencia drivers instalados (open source, GitHub).",
            "https://github.com/lostindark/DriverStoreExplorer", "https://api.github.com/repos/lostindark/DriverStoreExplorer/releases/latest", null, null, "Drivers e componentes", "logo_driverstools", "lostindark.DriverStoreExplorer"),
        new Utilitario("AMD Chipset Drivers", "Instalador oficial de drivers de chipset e gráficos AMD.",
            "https://www.amd.com/en/support/downloads/drivers.html/chipsets.html", "https://drivers.amd.com/drivers/installer/amd_software_installer.exe", "Advanced Micro Devices", null, "Drivers e componentes", "logo_amdchipset", "AMD.RyzenMaster"),
        new Utilitario("Intel DSA", "Assistente oficial de drivers de rede e Wi-Fi (Intel DSA).",
            "https://www.intel.com/content/www/us/en/download-center/home.html", "https://dsadata.intel.com/installer", "Intel", null, "Drivers e componentes", "logo_internetdriver", "Intel.IntelDriverAndSupportAssistant"),
        new Utilitario("Intel Wireless Bluetooth", "Driver oficial Bluetooth para adaptadores Intel.",
            "https://www.intel.com/content/www/us/en/download/18649/intel-wireless-bluetooth-drivers-for-windows-10-and-windows-11.html", "https://dsadata.intel.com/installer", "Intel", null, "Drivers e componentes", "logo_bluetoothdriver", "Intel.IntelDriverAndSupportAssistant"),
        new Utilitario("Logitech G HUB", "Periféricos Logitech G (RGB, perfis).",
            "https://www.logitechg.com/en-us/innovation/g-hub.html", "https://download01.logi.com/web/ftp/pub/techsupport/gaming/lghub_installer.exe", "Logitech", null, "Drivers e componentes", "logo_logitechghub", "Logitech.GHUB"),

        // ── Runtimes ──
        new Utilitario("Microsoft Visual C++ Redistributable", "Pacote oficial de runtimes C++ x64 exigido pela maioria dos jogos (Microsoft).",
            "https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist", "https://aka.ms/vs/17/release/vc_redist.x64.exe", "Microsoft", null, "Drivers e componentes", "logo_vcredist", "Microsoft.VCRedist.2015+.x64"),
        new Utilitario("DirectX End-User Runtime", "Bibliotecas DirectX legadas para jogos antigos (Microsoft).",
            "https://www.microsoft.com/en-us/download/details.aspx?id=35", "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe", "Microsoft", null, "Drivers e componentes", "logo_directx", "Microsoft.DirectX"),

        // ── Utilitários ──
        new Utilitario("7-Zip", "Compactador de arquivos leve e aberto (Igor Pavlov).",
            "https://www.7-zip.org/", "https://api.github.com/repos/ip7z/7zip/releases/latest", "Igor Pavlov", null, "Aplicativos essenciais", "logo_7zip", "7zip.7zip"),
        new Utilitario("WinRAR", "Compactador de arquivos RAR e ZIP (versão em português; teste gratuito de 40 dias).",
            "https://www.win-rar.com/download.html", "https://www.rarlab.com/rar/winrar-x64-713br.exe", "win.rar", null, "Aplicativos essenciais", "logo_winrar", "RARLab.WinRAR"),
        new Utilitario("Everything (voidtools)", "Busca instantânea de arquivos por índice NTFS.",
            "https://www.voidtools.com/downloads/", "https://www.voidtools.com/Everything-1.4.1.1026.x64-Setup.exe", "voidtools", null, "Aplicativos essenciais", "logo_everything", "voidtools.Everything"),
        new Utilitario("Revo Uninstaller", "Desinstalador com varredura de sobras de registro e pastas.",
            "https://www.revouninstaller.com/products/revo-uninstaller-free/", "https://download.revouninstaller.com/download/revosetup.exe", "VS Revo Group", null, "Aplicativos essenciais", "logo_revouninstaller", "RevoUninstaller.RevoUninstaller"),
        new Utilitario("Geek Uninstaller", "Desinstalador portátil e leve que também remove sobras e força a remoção de programas teimosos.",
            "https://geekuninstaller.com/download", "https://geekuninstaller.com/geek.zip", "CrystalBit", null, "Aplicativos essenciais", "logo_geekuninstaller", "GeekUninstaller.GeekUninstaller"),
        new Utilitario("Temp Cleaner (Sordum)", "Limpa arquivos temporários do Windows e de navegadores com um clique (portátil, Sordum).",
            "https://www.sordum.org/", "https://www.sordum.org/files/downloads.php?st-temp-cleaner", "Sordum", null, "Aplicativos essenciais", "logo_tempcleaner", null),
        new Utilitario("Lightshot", "Captura de tela rápida de uma região com edição e envio (Skillbrains).",
            "https://app.prntscr.com/", "https://app.prntscr.com/build/setup-lightshot.exe", "Kilonova", null, "Aplicativos essenciais", "logo_lightshot", "Skillbrains.Lightshot"),
        new Utilitario("Notepad++", "Editor de texto e código rápido e leve.",
            "https://notepad-plus-plus.org/downloads/", "https://api.github.com/repos/notepad-plus-plus/notepad-plus-plus/releases/latest", "Notepad++", null, "Aplicativos essenciais", "logo_notepad", "Notepad++.Notepad++"),
        new Utilitario("qBittorrent", "Cliente BitTorrent open source sem anúncios.",
            "https://www.qbittorrent.org/download", "https://www.qbittorrent.org/download", null, null, "Aplicativos essenciais", "logo_qbittorrent", "qBittorrent.qBittorrent"),

        // ── Mídia e gravação ──
        new Utilitario("OBS Studio", "Gravação e transmissão ao vivo acelerada por GPU (open source).",
            "https://obsproject.com/download", "https://api.github.com/repos/obsproject/obs-studio/releases/latest", "OBS", null, "Sistema, criação e desenvolvimento", "logo_obsstudio", "OBSProject.OBSStudio"),
        new Utilitario("Medal.tv", "Clipes rápidos de partidas com codificação por hardware.",
            "https://medal.tv/", "https://install.medal.tv/", "Medal", null, "Sistema, criação e desenvolvimento", "logo_medaltv", "MedalB.V.Medal"),
        new Utilitario("VLC Media Player", "Reprodutor multimídia leve e open source (VideoLAN).",
            "https://www.videolan.org/vlc/", "https://get.videolan.org/vlc/last/win64/", "VideoLAN", null, "Sistema, criação e desenvolvimento", "logo_vlcplayer", "VideoLAN.VLC"),
        new Utilitario("Spotify", "Streaming de música e podcasts.",
            "https://www.spotify.com/download/windows/", "https://download.scdn.co/SpotifySetup.exe", "Spotify", null, "Sistema, criação e desenvolvimento", "logo_spotify", "Spotify.Spotify"),
        new Utilitario("Stremio", "Central de filmes e séries com catálogo e complementos (Smart Code).",
            "https://www.stremio.com/downloads", "https://dl.strem.io/stremio-shell-ng/v5.0.13/StremioSetup-v5.0.13.exe", "Smart Code", null, "Sistema, criação e desenvolvimento", "logo_stremio", "Stremio.Stremio"),

        // ── Acesso remoto ──
        new Utilitario("AnyDesk", "Acesso remoto rápido a outro PC (suporte técnico, trabalho remoto).",
            "https://anydesk.com/pt/downloads/windows", "https://download.anydesk.com/AnyDesk.exe", "AnyDesk", null, "Sistema, criação e desenvolvimento", "logo_anydesk", "AnyDeskSoftwareGmbH.AnyDesk"),
        new Utilitario("RustDesk", "Acesso remoto open source, com opção de servidor próprio.",
            "https://rustdesk.com/", "https://api.github.com/repos/rustdesk/rustdesk/releases/latest", "PURSLANE", null, "Sistema, criação e desenvolvimento", "logo_rustdesk", "RustDesk.RustDesk"),

        // ── Sistema e personalização ──
        new Utilitario("Open-Shell", "Menu Iniciar clássico do Windows (open source).",
            "https://open-shell.github.io/Open-Shell-Menu/", "https://api.github.com/repos/Open-Shell/Open-Shell-Menu/releases/latest", null, null, "Sistema, criação e desenvolvimento", "logo_openshell", "Open-Shell.Open-Shell-Menu"),
        new Utilitario("StartAllBack", "Restaura a barra de tarefas/Explorer clássicos (pago).",
            "https://www.startallback.com/", "https://s3.amazonaws.com/startisback/StartAllBack_3.8.13_setup.exe", "StartIsBack", null, "Sistema, criação e desenvolvimento", "logo_startallback", "StartIsBack.StartAllBack"),
        new Utilitario("Windows Update Blocker (Sordum)", "Liga e desliga o Windows Update por completo (portátil, Sordum). Desligado, o PC deixa de receber correções de segurança.",
            "https://www.sordum.org/9470/windows-update-blocker-v1-8/", "https://www.sordum.org/9470/windows-update-blocker-v1-8/", "Sordum", null, "Sistema, criação e desenvolvimento", "logo_wub", null,
            SoPagina: "Desligar o Windows Update deixa o PC sem correções de segurança, e o download oficial passa por um link do Google Drive; baixe pela página oficial só se souber o que está fazendo e religue as atualizações depois."),

        // ── Desenvolvimento ──
        new Utilitario("Visual Studio Code", "Editor de código da Microsoft.",
            "https://code.visualstudio.com/", "https://code.visualstudio.com/sha/download?build=stable&os=win32-x64-user", "Microsoft", null, "Sistema, criação e desenvolvimento", "logo_vscode", "Microsoft.VisualStudioCode"),
        new Utilitario("Visual Studio", "IDE da Microsoft.",
            "https://visualstudio.microsoft.com/", "https://aka.ms/vs/17/release/vs_community.exe", "Microsoft", null, "Sistema, criação e desenvolvimento", "logo_visualstudio", "Microsoft.VisualStudio.2022.Community"),
    };

    public static readonly IReadOnlyList<string> OrdemCategorias = new[]
    {
        "Jogos e comunidade", "Desempenho e diagnóstico", "Drivers e componentes", "Aplicativos essenciais",
        "Sistema, criação e desenvolvimento",
    };

    public static IReadOnlyList<(string Categoria, int Qtd)> ContagemPorCategoria()
    {
        var por = Todos.GroupBy(u => u.Categoria).ToDictionary(g => g.Key, g => g.Count());
        var ordem = OrdemCategorias.Where(por.ContainsKey)
            .Concat(por.Keys.Where(k => !OrdemCategorias.Contains(k)).OrderBy(k => k));
        return ordem.Select(c => (c, por[c])).ToList();
    }

    /// <summary>Busca do catálogo: cada palavra precisa aparecer no nome, na descrição, na categoria ou no publisher,
    /// sem diferenciar maiúsculas nem acentos ("comunicacao" acha "Comunicação"). <paramref name="categoria"/> null = todas. Pura.</summary>
    public static List<Utilitario> Filtrar(IEnumerable<Utilitario> itens, string? busca, string? categoria)
    {
        static string Norm(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
        var termos = Norm(busca ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return itens.Where(u => categoria is null || u.Categoria == categoria)
            .Where(u =>
            {
                var alvo = Norm($"{u.Nome} {u.Descricao} {u.Categoria} {u.PublisherEsperado}");
                return termos.All(alvo.Contains);
            }).ToList();
    }

    public static readonly IReadOnlyList<string> Excluidos = new[] { "KMS Activator" };
}
