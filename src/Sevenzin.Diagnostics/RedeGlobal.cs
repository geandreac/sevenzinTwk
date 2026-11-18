using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Resultado da aplicação das otimizações globais de TCP.</summary>
public sealed record ResultadoRedeGlobal(bool Ok, string Mensagem);

/// <summary>Otimizações GLOBAIS de TCP do original (paridade com 03_comandos_sistema.txt):
/// <c>netsh int tcp set global autotuninglevel=normal</c> + congestion provider CTCP.
/// No Windows 11 moderno, o parâmetro global <c>congestionprovider</c> foi REMOVIDO — o comando literal
/// do original (<c>set global congestionprovider=ctcp</c>) hoje retorna "Parâmetro incorreto" (validado em
/// máquina real em 17/11/2026). O equivalente atual é
/// <c>netsh int tcp set supplemental internet congestionprovider=ctcp</c>. O Sevenzin usa o comando moderno
/// para preservar a INTENÇÃO do original (CTCP) e registra a divergência de comando. Exige administrador.
/// Reversível. A releitura do estado real confirma a aplicação antes de declarar sucesso.</summary>
public static class RedeGlobal
{
    /// <summary>Parse da saída do netsh (EN/PT) para autotuning e provedor de congestionamento.
    /// Reconhece tanto "show global" (legado "Congestão de Complementos") quanto "show supplemental"
    /// ("congestionamento"). Função pura.</summary>
    public static (string? Autotuning, string? Congestionamento) AnalisarSaida(string saida)
    {
        string? autotuning = null, congestionamento = null;
        foreach (var linha in (saida ?? "").Split('\n'))
        {
            var l = linha.Trim();
            if (autotuning is null && (l.Contains("Auto-Tuning", StringComparison.OrdinalIgnoreCase) || l.Contains("Ajuste Automático", StringComparison.OrdinalIgnoreCase)))
                autotuning = Valor(l);
            // Só a linha do PROVEDOR: "Janela de congestionamento inicial (MSS) : 10" vem antes e também contém "congest".
            if (congestionamento is null && l.Contains("congest", StringComparison.OrdinalIgnoreCase) &&
                (l.Contains("provedor", StringComparison.OrdinalIgnoreCase) || l.Contains("provider", StringComparison.OrdinalIgnoreCase)))
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

    /// <summary>Estado atual — autotuning de "show global"; congestionamento de "show supplemental"
    /// (comando moderno), com fallback para "show global" em Windows antigos. Só leitura.</summary>
    public static (string? Autotuning, string? Congestionamento) EstadoAtual()
    {
        var global = AnalisarSaida(Rodar("int tcp show global").Saida);
        var supp = AnalisarSaida(Rodar("int tcp show supplemental internet").Saida); // o mesmo modelo que Aplicar altera
        return (global.Autotuning, supp.Congestionamento ?? global.Congestionamento);
    }

    /// <summary>Aplica autotuninglevel=normal (global) + congestion provider ctcp (supplemental — comando
    /// moderno equivalente ao original) e relê o estado real.</summary>
    public static ResultadoRedeGlobal Aplicar()
    {
        try
        {
            var (c1, _) = Rodar("int tcp set global autotuninglevel=normal");
            var (c2, _) = Rodar("int tcp set supplemental internet congestionprovider=ctcp");
            if (c1 != 0 || c2 != 0) return new(false, "Falha ao aplicar (exige administrador).");

            var (at, cong) = EstadoAtual();
            bool ok = (at is null || at == "normal") && (cong is null || cong == "ctcp");
            return ok
                ? new(true, "TCP global aplicado: autotuning normal + congestion provider ctcp (intenção do original; comando moderno 'set supplemental').")
                : new(false, $"Aplicado, mas a releitura divergiu (autotuning={at ?? "?"}, congestionamento={cong ?? "?"}).");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Reverte para o padrão do Windows (autotuninglevel=normal + congestion provider default).</summary>
    public static ResultadoRedeGlobal Restaurar()
    {
        try
        {
            Rodar("int tcp set global autotuninglevel=normal");
            Rodar("int tcp set supplemental internet congestionprovider=default");
            return new(true, "TCP global restaurado (autotuning normal + congestionamento padrão do Windows).");
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
