using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Resultado da aplicação das otimizações globais de TCP.</summary>
public sealed record ResultadoRedeGlobal(bool Ok, string Mensagem);

/// <summary>Otimizações GLOBAIS de TCP do original (paridade com 03_comandos_sistema.txt):
/// <c>netsh int tcp set global autotuninglevel=normal</c> e <c>congestionprovider=ctcp</c>.
/// Exige administrador. Reversível (volta ao "normal"/"default"). A releitura do estado real
/// confirma a aplicação antes de declarar sucesso.</summary>
public static class RedeGlobal
{
    /// <summary>Parse da saída de `netsh int tcp show global` (EN/PT). Devolve os valores atuais de
    /// autotuning e do provedor de congestionamento, ou null quando não identificados. Função pura.</summary>
    public static (string? Autotuning, string? Congestionamento) AnalisarSaida(string saida)
    {
        string? autotuning = null, congestionamento = null;
        foreach (var linha in (saida ?? "").Split('\n'))
        {
            var l = linha.Trim();
            if (autotuning is null && (l.Contains("Auto-Tuning", StringComparison.OrdinalIgnoreCase) || l.Contains("Ajuste Automático", StringComparison.OrdinalIgnoreCase)))
                autotuning = Valor(l);
            if (congestionamento is null && (l.Contains("Congestion Control Provider", StringComparison.OrdinalIgnoreCase) || l.Contains("Controle de Congestionamento", StringComparison.OrdinalIgnoreCase) || l.Contains("Congestion Provider", StringComparison.OrdinalIgnoreCase)))
                congestionamento = Valor(l);
        }
        return (autotuning, congestionamento);
    }

    static string? Valor(string linha)
    {
        var i = linha.IndexOf(':');
        return i >= 0 ? linha[(i + 1)..].Trim().ToLowerInvariant() : null;
    }

    static (int Codigo, string Saida) Rodar(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, saida);
    }

    /// <summary>Estado atual (autotuning, congestionamento) — só leitura.</summary>
    public static (string? Autotuning, string? Congestionamento) EstadoAtual() =>
        AnalisarSaida(Rodar("int tcp show global").Saida);

    /// <summary>Aplica autotuninglevel=normal + congestionprovider=ctcp (valores do original) e relê o estado.</summary>
    public static ResultadoRedeGlobal Aplicar()
    {
        try
        {
            var (c1, _) = Rodar("int tcp set global autotuninglevel=normal");
            var (c2, _) = Rodar("int tcp set global congestionprovider=ctcp");
            if (c1 != 0 || c2 != 0) return new(false, "Falha ao aplicar (exige administrador).");

            var (at, cong) = EstadoAtual();
            bool ok = (at is null || at == "normal") && (cong is null || cong == "ctcp");
            return ok
                ? new(true, "TCP global aplicado: autotuning normal + congestion provider ctcp (valores do original).")
                : new(false, $"Aplicado, mas a releitura divergiu (autotuning={at ?? "?"}, congestionamento={cong ?? "?"}).");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Reverte para o padrão do Windows (autotuninglevel=normal + congestionprovider=default).</summary>
    public static ResultadoRedeGlobal Restaurar()
    {
        try
        {
            Rodar("int tcp set global autotuninglevel=normal");
            Rodar("int tcp set global congestionprovider=default");
            return new(true, "TCP global restaurado (autotuning normal + congestionamento padrão do Windows).");
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
