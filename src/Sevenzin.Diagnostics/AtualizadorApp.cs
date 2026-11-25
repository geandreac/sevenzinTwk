using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Sevenzin.Diagnostics;

/// <summary>
/// Gerencia o fluxo "Restart to Update": baixa o instalador oficial da release do GitHub,
/// confere o SHA-256 publicado pelo GitHub e agenda um auxiliar destacado
/// para encerrar o app atual, atualizar via setup silencioso e reabrir a nova versão automaticamente.
/// </summary>
public static class AtualizadorApp
{
    public const string AppGuidUninstall = "{8F3A6C21-5B4E-4D9A-9C2F-1E7A0B3D6F84}_is1";

    const string PrefixoPermitido = "https://github.com/geandreac/sevenzinTwk/releases/download/";

    static readonly HttpClient Http = CriarHttpClient();

    static HttpClient CriarHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SevenzinTwks-Updater", "2.0"));
        return c;
    }

    /// <summary>Só assets de release deste repositório (o GitHub redireciona para o CDN; quem garante o conteúdo é o SHA-256).</summary>
    public static bool UrlPermitida(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.AbsoluteUri.StartsWith(PrefixoPermitido, StringComparison.OrdinalIgnoreCase);

    public static bool Sha256Confere(string caminho, string? esperado)
    {
        if (esperado is not { Length: 64 }) return false;
        using var fs = File.OpenRead(caminho);
        return Convert.ToHexString(SHA256.HashData(fs)).Equals(esperado, StringComparison.OrdinalIgnoreCase);
    }

    public static string PastaAtualizacoes(string? pastaBase = null) =>
        pastaBase ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sevenzin",
            "Atualizacoes");

    public static async Task<string> BaixarInstaladorAsync(
        string url,
        string? nomeArquivo,
        string? sha256,
        IProgress<(long Baixados, long? Total)>? progresso = null,
        CancellationToken ct = default,
        string? pastaDestino = null)
    {
        if (!UrlPermitida(url))
            throw new InvalidOperationException("URL de atualização fora dos domínios oficiais do GitHub.");

        var pasta = PastaAtualizacoes(pastaDestino);
        Directory.CreateDirectory(pasta);

        var nomeLimpo = Path.GetFileName(string.IsNullOrWhiteSpace(nomeArquivo) ? "SevenzinTwks-setup.exe" : nomeArquivo);
        if (!nomeLimpo.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            nomeLimpo += ".exe";

        var destino = Path.Combine(pasta, nomeLimpo);
        var temp = destino + ".tmp";

        try
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength;
            await using (var input = await resp.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(temp))
            {
                var buffer = new byte[65536];
                long baixados = 0;
                int lidos;
                while ((lidos = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, lidos), ct);
                    baixados += lidos;
                    progresso?.Report((baixados, total));
                }
            }

            if (!Sha256Confere(temp, sha256))
                throw new InvalidDataException("O arquivo baixado não confere com o SHA-256 publicado no GitHub.");

            File.Move(temp, destino, overwrite: true);
            return destino;
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            throw;
        }
    }

    static string EscaparPs(string s) => s.Replace("'", "''");

    public static string MontarScriptAtualizacaoPowerShell(string caminhoInstalador, string sha256, string? caminhoExeAtual, int pidAtual)
    {
        var instEsc = EscaparPs(caminhoInstalador);
        var exeEsc = EscaparPs(caminhoExeAtual ?? "");
        return $@"
$ErrorActionPreference = 'SilentlyContinue'
$pidAlvo = {pidAtual}
$limite = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $limite) {{
    if (-not (Get-Process -Id $pidAlvo -ErrorAction SilentlyContinue)) {{ break }}
    Start-Sleep -Milliseconds 250
}}

$inst = '{instEsc}'
if ((Get-FileHash -LiteralPath $inst -Algorithm SHA256).Hash -eq '{EscaparPs(sha256)}') {{
    Start-Process -FilePath $inst -ArgumentList '/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /SP-' -Wait
}}

$alvos = @()
$reg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuidUninstall}' -ErrorAction SilentlyContinue
if ($reg -and $reg.InstallLocation) {{
    $alvos += (Join-Path $reg.InstallLocation 'Sevenzin.exe')
}}
$alvos += (Join-Path $env:ProgramFiles 'SevenzinTwks\Sevenzin.exe')
if ('{exeEsc}' -ne '') {{ $alvos += '{exeEsc}' }}

foreach ($exe in $alvos) {{
    if ($exe -and (Test-Path -LiteralPath $exe)) {{
        Start-Process -FilePath $exe
        break
    }}
}}
".Trim();
    }

    public static bool IniciarAtualizacaoEReiniciar(string caminhoInstalador, string sha256)
    {
        try
        {
            if (!Sha256Confere(caminhoInstalador, sha256)) return false;
            var script = MontarScriptAtualizacaoPowerShell(caminhoInstalador, sha256, Environment.ProcessPath, Environment.ProcessId);
            // -EncodedCommand: o script roda elevado, então não passa por um arquivo que outro programa poderia trocar.
            using var p = Process.Start(new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return p is not null;
        }
        catch
        {
            return false;
        }
    }
}
