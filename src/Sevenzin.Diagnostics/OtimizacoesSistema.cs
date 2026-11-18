using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

public sealed record EstadoMmAgent(bool? CompressaoMemoria, bool? PreLaunch);
public sealed record ResultadoOtimizacaoSistema(bool Ok, string Mensagem);

/// <summary>
/// Otimizações de sistema via cmdlets nativos do Windows confirmados em `03_comandos_sistema.txt`
/// (`MMAgent` e Offloads/Economia de Energia do adaptador de rede), com leitura de estado e reversão completa.
/// </summary>
public static class OtimizacoesSistema
{
    static readonly Regex RxNomeInseguro = new(@"['"";|`$\r\n]", RegexOptions.Compiled);

    /// <summary>Analisa a saída textual de `Get-MMAgent` (Format-List). Puro/testável.</summary>
    public static EstadoMmAgent AnalisarSaidaMmAgent(string? saida)
    {
        if (string.IsNullOrWhiteSpace(saida)) return new(null, null);
        bool? comp = ExtrairBool(saida, "MemoryCompression");
        bool? pre = ExtrairBool(saida, "ApplicationPreLaunch");
        return new(comp, pre);
    }

    static bool? ExtrairBool(string texto, string chave)
    {
        var m = Regex.Match(texto, $@"(?im)^\s*{Regex.Escape(chave)}\s*:\s*(True|False)\b");
        if (!m.Success) return null;
        return m.Groups[1].Value.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Valida o nome amigável do adaptador contra injeção de comandos PowerShell. Puro/testável.</summary>
    public static bool NomeAdaptadorValido(string? nome) =>
        !string.IsNullOrWhiteSpace(nome) && nome.Length <= 128 && !RxNomeInseguro.IsMatch(nome);

    public static EstadoMmAgent LerMmAgent() =>
        AnalisarSaidaMmAgent(RodarPowerShell("Get-MMAgent | Format-List MemoryCompression,ApplicationPreLaunch"));

    public static ResultadoOtimizacaoSistema DesativarMmAgent()
    {
        try
        {
            RodarPowerShell("Disable-MMAgent -MemoryCompression -ApplicationPreLaunch -ErrorAction SilentlyContinue");
            var apos = LerMmAgent();
            bool ok = apos.CompressaoMemoria == false && apos.PreLaunch == false;
            return ok
                ? new(true, "Compressão de memória e Application PreLaunch desativados (reversível).")
                : new(false, "Não foi possível confirmar a alteração no MMAgent — execute como Administrador.");
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public static ResultadoOtimizacaoSistema RestaurarMmAgent()
    {
        try
        {
            RodarPowerShell("Enable-MMAgent -MemoryCompression -ApplicationPreLaunch -ErrorAction SilentlyContinue");
            var apos = LerMmAgent();
            bool ok = apos.CompressaoMemoria == true;
            return ok
                ? new(true, "Compressão de memória e Application PreLaunch reativados (padrão do Windows).")
                : new(false, "Não foi possível confirmar a restauração no MMAgent — execute como Administrador.");
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public static IReadOnlyList<string> ListarNomesAdaptadoresAtivos()
    {
        var txt = RodarPowerShell("Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -ExpandProperty Name");
        return txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(NomeAdaptadorValido)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ResultadoOtimizacaoSistema DesativarOffloadsRede(string nomeAdaptador)
    {
        if (!NomeAdaptadorValido(nomeAdaptador))
            return new(false, "Nome de adaptador inválido.");
        try
        {
            var script = string.Join("; ",
                $"Disable-NetAdapterLso -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Disable-NetAdapterRsc -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Disable-NetAdapterChecksumOffload -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Disable-NetAdapterIPsecOffload -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Disable-NetAdapterPowerManagement -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue");
            RodarPowerShell(script);
            return new(true, $"Offloads (LSO, RSC, Checksum, IPsec) e Economia de Energia desativados em '{nomeAdaptador}'.");
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    public static ResultadoOtimizacaoSistema RestaurarOffloadsRede(string nomeAdaptador)
    {
        if (!NomeAdaptadorValido(nomeAdaptador))
            return new(false, "Nome de adaptador inválido.");
        try
        {
            var script = string.Join("; ",
                $"Enable-NetAdapterLso -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Enable-NetAdapterRsc -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Enable-NetAdapterChecksumOffload -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Enable-NetAdapterIPsecOffload -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue",
                $"Enable-NetAdapterPowerManagement -Name '{nomeAdaptador}' -NoRestart -ErrorAction SilentlyContinue");
            RodarPowerShell(script);
            return new(true, $"Offloads e gerenciamento de energia restaurados ao padrão em '{nomeAdaptador}'.");
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    static string RodarPowerShell(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"{comando}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return string.Empty;
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(20000);
            return outp.Trim();
        }
        catch { return string.Empty; }
    }
}
