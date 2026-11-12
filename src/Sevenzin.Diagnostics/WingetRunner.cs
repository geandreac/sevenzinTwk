using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Allowlist do winget EXATAMENTE como confirmada na engenharia reversa
/// (110_Honest.WindowsMedia.BuildMedia.ps1 → `$allowed=@{...}`; 05_metricas §8). Nada fora da lista
/// é instalado — e nada roda sem confirmação explícita do usuário na tela Instaladores.</summary>
public static class WingetAllowlist
{
    /// <summary>Mapa chave → ID do winget, literal do original.</summary>
    public static readonly IReadOnlyDictionary<string, string> Itens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["steam"] = "Valve.Steam",
        ["discord"] = "Discord.Discord",
        ["7zip"] = "7zip.7zip",
        ["chrome"] = "Google.Chrome",
        ["vlc"] = "VideoLAN.VLC",
    };

    /// <summary>IDs aceitos, em ordem estável (para a UI).</summary>
    public static IReadOnlyList<(string Chave, string Id)> Lista => Itens.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)).ToList();

    /// <summary>Código de retorno do winget que significa "já instalado" (confirmado no original).</summary>
    public const int CodigoJaInstalado = -1978335189;
}

/// <summary>Constrói e executa o comando winget da allowlist. A construção do comando é pura (testável);
/// a execução é separada e SEMPRE precedida de confirmação na UI.</summary>
public static class WingetRunner
{
    /// <summary>Caminho do winget no Windows 10/11 (o original esperava até 30×3 s no primeiro logon).</summary>
    public static string CaminhoWinget =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");

    /// <summary>Argumentos exatos do original: instala pelo ID, fonte winget, silencioso, aceita os acordos,
    /// sem interatividade e sem upgrade de pacotes já instalados. Função pura (testável).</summary>
    public static string ArgumentosPara(string id) =>
        $"install --id {id} --exact --source winget --silent --accept-source-agreements --accept-package-agreements --disable-interactivity --no-upgrade";

    /// <summary>Resultado de uma instalação: Ok, já instalado (código confirmado) ou falha com saída.</summary>
    public sealed record ResultadoWinget(bool Ok, bool JaInstalado, string Detalhe);

    /// <summary>Executa a instalação. NUNCA chame sem o usuário ter confirmado.</summary>
    public static async Task<ResultadoWinget> InstalarAsync(string id, CancellationToken ct = default)
    {
        if (!WingetAllowlist.Itens.Values.Contains(id, StringComparer.OrdinalIgnoreCase))
            return new(false, false, "ID fora da allowlist — nada foi executado.");

        var psi = new ProcessStartInfo(CaminhoWinget)
        {
            ArgumentList = { "install", "--id", id, "--exact", "--source", "winget", "--silent", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity", "--no-upgrade" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return new(false, false, "Não foi possível iniciar o winget.");
            var saida = await p.StandardOutput.ReadToEndAsync(ct);
            var erro = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode == 0) return new(true, false, "Instalado com sucesso.");
            if (p.ExitCode == WingetAllowlist.CodigoJaInstalado) return new(true, true, "Já estava instalado.");
            return new(false, false, $"winget terminou com código {p.ExitCode}." + (erro.Length > 0 ? $" {erro.Trim()}" : ""));
        }
        catch (Exception ex)
        {
            return new(false, false, ex.Message);
        }
    }
}
