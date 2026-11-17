using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sevenzin.Diagnostics;

/// <summary>Um utilitário do catálogo: origem OFICIAL e o que validar antes de executar.
/// Sha256Esperado e PublisherEsperado são opcionais, mas a assinatura é sempre exigida.</summary>
public sealed record Utilitario(
    string Nome, string Descricao, string UrlOficial, string? UrlDownload,
    string? PublisherEsperado, string? Sha256Esperado, string Categoria = "Sistema");

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
        catch { return true; } // não foi possível medir: deixa o download tentar e falhar com erro claro
    }

    /// <param name="handler">injetável para teste; por padrão usa HttpClient real.</param>
    public static async Task BaixarAsync(string url, string destino, IProgress<ProgressoDownload>? progresso = null,
        CancellationToken ct = default, HttpMessageHandler? handler = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
        var part = destino + ".part";
        try
        {
            using var http = handler is null ? new HttpClient() : new HttpClient(handler);
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1;
            if (total > 0 && !EspacoSuficiente(Path.GetDirectoryName(Path.GetFullPath(destino))!, total))
                throw new IOException("Espaço em disco insuficiente para este download.");

            await using var origem = await resp.Content.ReadAsStreamAsync(ct);
            await using var arquivo = File.Create(part);
            var buffer = new byte[81920];
            long lido = 0; int n;
            var crono = System.Diagnostics.Stopwatch.StartNew();
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
            arquivo.Dispose(); // fecha antes do move atômico (Windows exige)
            File.Move(part, destino, overwrite: true); // move atômico só no sucesso
        }
        catch
        {
            try { if (File.Exists(part) && !File.Exists(destino)) File.Delete(part); } catch { }
            throw;
        }
    }
}

/// <summary>Catálogo de utilitários benignos com origem oficial. Nada é baixado ou executado sem o usuário,
/// e só após verificação de assinatura. Para um download direto, preencha UrlDownload (+ hash, opcional).</summary>
public static class CatalogoUtilitarios
{
    public static readonly IReadOnlyList<Utilitario> Todos = new[]
    {
        new Utilitario("Coreinfo (Sysinternals)", "Mostra o mapeamento de núcleos lógicos/físicos, cache e topologia (Microsoft).",
            "https://learn.microsoft.com/sysinternals/downloads/coreinfo", "https://live.sysinternals.com/Coreinfo.exe", "Microsoft", null, "Monitoramento"),
        new Utilitario("CPU-Z", "Informações detalhadas de CPU, placa-mãe e memória (CPUID).",
            "https://www.cpuid.com/softwares/cpu-z.html", null, "CPUID", null, "Monitoramento"),
        new Utilitario("CrystalDiskInfo", "Saúde e S.M.A.R.T. dos discos (Crystal Dew World).",
            "https://crystalmark.info/en/software/crystaldiskinfo/", null, null, null, "Monitoramento"),
        new Utilitario("HWiNFO", "Monitoramento de sensores (temperatura, clock, uso) de CPU/GPU/placa (REALiX).",
            "https://www.hwinfo.com/download/", null, null, null, "Monitoramento"),
        new Utilitario("GPU-Z", "Informações e sensores da placa de vídeo (TechPowerUp).",
            "https://www.techpowerup.com/gpuz/", null, null, null, "Monitoramento"),
        new Utilitario("Process Explorer (Sysinternals)", "Gerenciador de processos avançado da Microsoft (handles, árvore, CPU por thread).",
            "https://learn.microsoft.com/sysinternals/downloads/process-explorer", "https://live.sysinternals.com/procexp64.exe", "Microsoft", null, "Monitoramento"),
        new Utilitario("Autoruns (Sysinternals)", "Lista tudo que inicia com o Windows, para revisar e desligar itens de inicialização (Microsoft).",
            "https://learn.microsoft.com/sysinternals/downloads/autoruns", "https://live.sysinternals.com/Autoruns64.exe", "Microsoft", null, "Sistema"),

        // ── Ampliação F3: nomes confirmados na engenharia reversa (05_metricas §6, 35 logos) ──
        // Política: sempre fonte OFICIAL; download direto só quando a URL é estável e assinável.
        new Utilitario("7-Zip", "Compactador de arquivos leve e aberto (Igor Pavlov).",
            "https://www.7-zip.org/", null, null, null, "Sistema"),
        new Utilitario("Brave Browser", "Navegador com bloqueio de anúncios e rastreadores embutido.",
            "https://brave.com/", "https://laptop-updates.brave.com/latest/winx64", null, null, "Sistema"),
        new Utilitario("Google Chrome", "Navegador do Google.",
            "https://www.google.com/chrome/", "https://dl.google.com/chrome/install/latest/chrome_installer.exe", null, null, "Sistema"),
        new Utilitario("Visual Studio Code", "Editor de código da Microsoft.",
            "https://code.visualstudio.com/", "https://code.visualstudio.com/sha/download?build=stable&os=win32-x64-user", null, null, "Sistema"),
        new Utilitario("Microsoft Edge", "Navegador da Microsoft.",
            "https://www.microsoft.com/edge", null, null, null, "Sistema"),
        new Utilitario("DirectX End-User Runtime", "Bibliotecas DirectX legadas para jogos antigos (Microsoft).",
            "https://www.microsoft.com/en-us/download/details.aspx?id=35", null, null, null, "Sistema"),
        new Utilitario("Epic Games Launcher", "Loja e launcher de jogos da Epic.",
            "https://store.epicgames.com/", null, null, null, "Plataformas"),
        new Utilitario("Rockstar Games Launcher", "Launcher da Rockstar Games.",
            "https://socialclub.rockstargames.com/rockstar-games-launcher", null, null, null, "Plataformas"),
        new Utilitario("Ubisoft Connect", "Launcher da Ubisoft.",
            "https://ubisoftconnect.com/", null, null, null, "Plataformas"),
        new Utilitario("VALORANT", "Jogo de tiro tático da Riot (com anti-cheat Vanguard).",
            "https://playvalorant.com/", null, null, null, "Jogos"),
        new Utilitario("FACEIT", "Plataforma competitiva de partidas e anti-cheat.",
            "https://www.faceit.com/", null, null, null, "Plataformas"),
        new Utilitario("Gamers Club", "Plataforma brasileira de partidas competitivas.",
            "https://gamersclub.com.br/", null, null, null, "Plataformas"),
        new Utilitario("TeamSpeak", "Comunicação por voz com servidores próprios.",
            "https://teamspeak.com/en/downloads/", null, null, null, "Sistema"),
        new Utilitario("LatencyMon", "Mede a latência DPC/ISR do sistema (Resplendence).",
            "https://www.resplendence.com/latencymon", null, null, null, "Monitoramento"),
        new Utilitario("MSI Afterburner", "Overclock e monitoramento de GPU (MSI).",
            "https://www.msi.com/Landing/afterburner/graphics-cards", null, null, null, "Monitoramento"),
        new Utilitario("Logitech G HUB", "Periféricos Logitech G (RGB, perfis).",
            "https://www.logitechg.com/en-us/innovation/g-hub.html", null, null, null, "Sistema"),
        new Utilitario("NVCleanstall", "Instalação limpa do driver NVIDIA (TechPowerUp).",
            "https://www.techpowerup.com/download/techpowerup-nvcleanstall/", null, null, null, "Drivers"),
        new Utilitario("OCAT", "Captura de FPS por PresentMon (GPUOpen/AMD).",
            "https://gpuopen.com/ocat/", null, null, null, "Monitoramento"),
        new Utilitario("Open-Shell", "Menu Iniciar clássico do Windows (open source).",
            "https://open-shell.github.io/Open-Shell-Menu/", null, null, null, "Sistema"),
        new Utilitario("StartAllBack", "Restaura a barra de tarefas/Explorer clássicos (pago).",
            "https://www.startallback.com/", null, null, null, "Sistema"),
        new Utilitario("Visual Studio", "IDE da Microsoft.",
            "https://visualstudio.microsoft.com/", null, null, null, "Sistema"),
        new Utilitario("DDU (Display Driver Uninstaller)", "Remove drivers de vídeo de forma limpa (Wagnardsoft).",
            "https://www.wagnardsoft.com/forums/viewforum.php?f=5", null, null, null, "Drivers"),
        new Utilitario("Radeon Software Slimmer", "Instalação enxuta do driver AMD (open source).",
            "https://github.com/GSDragoon/RadeonSoftwareSlimmer", null, null, null, "Drivers"),
        new Utilitario("ZenTimings", "Tempos de memória em placas AMD (open source).",
            "https://zentimings.protonrom.com/", null, null, null, "Monitoramento"),
        new Utilitario("Prime95", "Teste de estabilidade de CPU/memória (GIMPS).",
            "https://www.mersenne.org/download/", null, null, null, "Monitoramento"),
        new Utilitario("Geekbench", "Benchmark de CPU/GPU multiplataforma (Primate Labs).",
            "https://www.geekbench.com/download/", null, null, null, "Monitoramento"),
        new Utilitario("OCCT", "Teste de estabilidade e sensores (Ocbase).",
            "https://www.ocbase.com/", null, null, null, "Monitoramento"),
        new Utilitario("DriverStore Explorer", "Gerencia drivers instalados (open source, GitHub).",
            "https://github.com/lostindark/DriverStoreExplorer", null, null, null, "Drivers"),
        new Utilitario("Internet / Wi-Fi driver", "Página oficial de drivers de rede (Intel).",
            "https://www.intel.com/content/www/us/en/download-center/home.html", null, null, null, "Drivers"),
    };

    // Ordem de exibição das categorias (como no original); categorias fora desta lista vão ao fim, em ordem alfabética.
    static readonly string[] OrdemCategorias = { "Sistema", "Drivers", "Plataformas", "Jogos", "Monitoramento" };

    /// <summary>Categorias presentes no catálogo e quantos utilitários cada uma tem, na ordem de exibição.
    /// Usado pelas "pills" de filtro. Puro/testável.</summary>
    public static IReadOnlyList<(string Categoria, int Qtd)> ContagemPorCategoria()
    {
        var por = Todos.GroupBy(u => u.Categoria).ToDictionary(g => g.Key, g => g.Count());
        var ordem = OrdemCategorias.Where(por.ContainsKey)
            .Concat(por.Keys.Where(k => !OrdemCategorias.Contains(k)).OrderBy(k => k));
        return ordem.Select(c => (c, por[c])).ToList();
    }

    /// <summary>Nomes do original que NÃO entram no catálogo por decisão registrada:
    /// KMS Activator (ativador de licença não-oficial — risco de malware e violação de licença).</summary>
    public static readonly IReadOnlyList<string> Excluidos = new[] { "KMS Activator" };
}
