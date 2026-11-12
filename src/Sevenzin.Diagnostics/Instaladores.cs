using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sevenzin.Diagnostics;

/// <summary>Um utilitário do catálogo: origem OFICIAL e o que validar antes de executar.
/// Sha256Esperado e PublisherEsperado são opcionais, mas a assinatura é sempre exigida.</summary>
public sealed record Utilitario(
    string Nome, string Descricao, string UrlOficial, string? UrlDownload,
    string? PublisherEsperado, string? Sha256Esperado);

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

/// <summary>Baixa um instalador da URL oficial para um arquivo local, reportando progresso. Não executa nada.</summary>
public static class DownloaderInstalador
{
    public static async Task BaixarAsync(string url, string destino, IProgress<double>? progresso = null, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        await using var origem = await resp.Content.ReadAsStreamAsync(ct);
        await using var arquivo = File.Create(destino);
        var buffer = new byte[81920];
        long lido = 0; int n;
        while ((n = await origem.ReadAsync(buffer, ct)) > 0)
        {
            await arquivo.WriteAsync(buffer.AsMemory(0, n), ct);
            lido += n;
            if (total > 0) progresso?.Report((double)lido / total);
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
            "https://learn.microsoft.com/sysinternals/downloads/coreinfo", "https://live.sysinternals.com/Coreinfo.exe", "Microsoft", null),
        new Utilitario("CPU-Z", "Informações detalhadas de CPU, placa-mãe e memória (CPUID).",
            "https://www.cpuid.com/softwares/cpu-z.html", null, "CPUID", null),
        new Utilitario("CrystalDiskInfo", "Saúde e S.M.A.R.T. dos discos (Crystal Dew World).",
            "https://crystalmark.info/en/software/crystaldiskinfo/", null, null, null),
        new Utilitario("HWiNFO", "Monitoramento de sensores (temperatura, clock, uso) de CPU/GPU/placa (REALiX).",
            "https://www.hwinfo.com/download/", null, null, null),
        new Utilitario("GPU-Z", "Informações e sensores da placa de vídeo (TechPowerUp).",
            "https://www.techpowerup.com/gpuz/", null, null, null),
        new Utilitario("Process Explorer (Sysinternals)", "Gerenciador de processos avançado da Microsoft (handles, árvore, CPU por thread).",
            "https://learn.microsoft.com/sysinternals/downloads/process-explorer", "https://live.sysinternals.com/procexp64.exe", "Microsoft", null),
        new Utilitario("Autoruns (Sysinternals)", "Lista tudo que inicia com o Windows, para revisar e desligar itens de inicialização (Microsoft).",
            "https://learn.microsoft.com/sysinternals/downloads/autoruns", "https://live.sysinternals.com/Autoruns64.exe", "Microsoft", null),
    };
}
