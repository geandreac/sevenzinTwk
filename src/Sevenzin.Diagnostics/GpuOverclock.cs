using System.Diagnostics;
using System.Globalization;

namespace Sevenzin.Diagnostics;

/// <summary>Telemetria real de GPU (NVIDIA, via nvidia-smi). Valores lidos, nunca inventados.</summary>
public sealed record GpuTelemetria(
    string Nome, int ClockMHz, int ClockMaxMHz, int MemClockMHz, int MemMaxMHz,
    double TempC, double PowerW, double PowerLimitW, double PowerMaxW, double UsoPct,
    long MemUsadaMB, long MemTotalMB, double PowerPadraoW = 0, double PowerMinW = 0);

public sealed record ResultadoOverclock(bool Ok, string Mensagem);

/// <summary>Overclock de GPU (NVIDIA) dentro do envelope de fábrica, via <c>nvidia-smi</c> oficial.
/// O que dá para fazer com segurança e reversão: travar o clock de boost (<c>-lgc</c>, reset em <c>-rgc</c>)
/// e ajustar o power limit (<c>-pl</c>). Overclock ALÉM do boost de fábrica (curva tensão/frequência)
/// exige NVAPI/MSI Afterburner — fora do escopo seguro; a limitação é registrada em vez de simulada.
/// Exige administrador.</summary>
public static class GpuOverclock
{
    const string Query =
        "--query-gpu=name,clocks.sm,clocks.max.sm,clocks.mem,clocks.max.mem,temperature.gpu,power.draw,power.limit,power.max_limit,utilization.gpu,memory.used,memory.total,power.default_limit,power.min_limit --format=csv,noheader,nounits";

    static (int Codigo, string Saida) Rodar(string args)
    {
        var psi = new ProcessStartInfo("nvidia-smi", args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, saida);
    }

    /// <summary>true quando o driver NVIDIA + nvidia-smi estão disponíveis.</summary>
    public static bool NvidiaDisponivel()
    {
        try { return Rodar("--query-gpu=name --format=csv,noheader").Codigo == 0; }
        catch { return false; }
    }

    /// <summary>Parse da linha CSV do nvidia-smi (com <c>nounits</c>). Função pura/testável.
    /// Ordem: nome, sm, sm.max, mem, mem.max, temp, power.draw, power.limit, power.max, uso, mem.usada, mem.total [, power.default, power.min].</summary>
    public static GpuTelemetria? AnalisarCsv(string linha)
    {
        var c = (linha ?? "").Split(',');
        if (c.Length < 12) return null;
        try
        {
            double pl = double.Parse(c[7], CultureInfo.InvariantCulture);
            double pMax = double.Parse(c[8], CultureInfo.InvariantCulture);
            double pDef = c.Length >= 13 && double.TryParse(c[12], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : pl;
            double pMin = c.Length >= 14 && double.TryParse(c[13], NumberStyles.Float, CultureInfo.InvariantCulture, out var mn) ? mn : Math.Round(pl * 0.5, 2);
            return new GpuTelemetria(
                c[0].Trim(),
                (int)Math.Round(double.Parse(c[1], CultureInfo.InvariantCulture)),
                (int)Math.Round(double.Parse(c[2], CultureInfo.InvariantCulture)),
                (int)Math.Round(double.Parse(c[3], CultureInfo.InvariantCulture)),
                (int)Math.Round(double.Parse(c[4], CultureInfo.InvariantCulture)),
                double.Parse(c[5], CultureInfo.InvariantCulture),
                double.Parse(c[6], CultureInfo.InvariantCulture),
                pl,
                pMax,
                double.Parse(c[9], CultureInfo.InvariantCulture),
                long.Parse(c[10], CultureInfo.InvariantCulture),
                long.Parse(c[11], CultureInfo.InvariantCulture),
                pDef,
                pMin);
        }
        catch { return null; }
    }

    /// <summary>Valida um power limit solicitado contra os limites informados pelo driver. Puro/testável.</summary>
    public static string? ValidarPowerLimit(double watts, GpuTelemetria t)
    {
        if (double.IsNaN(watts) || double.IsInfinity(watts) || watts <= 0)
            return "Power limit inválido.";
        double min = t.PowerMinW > 0 ? t.PowerMinW : 1;
        double max = t.PowerMaxW > 0 ? t.PowerMaxW : t.PowerLimitW;
        if (watts < min - 0.1 || watts > max + 0.1)
            return $"Fora da faixa permitida pela placa ({min:0.#} W a {max:0.#} W).";
        return null;
    }

    /// <summary>Telemetria atual da primeira GPU NVIDIA, ou null se indisponível.</summary>
    public static GpuTelemetria? Ler()
    {
        try
        {
            var (cod, saida) = Rodar(Query);
            if (cod != 0) return null;
            var primeira = saida.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return primeira is null ? null : AnalisarCsv(primeira);
        }
        catch { return null; }
    }

    /// <summary>Trava o clock de boost na faixa [min, max] MHz (sustained boost). Reversível com <see cref="ResetarClock"/>.</summary>
    public static ResultadoOverclock TravarClock(int minMHz, int maxMHz)
    {
        if (minMHz <= 0 || maxMHz <= minMHz) return new(false, "Faixa de clock inválida.");
        try
        {
            var (cod, saida) = Rodar($"-lgc {minMHz},{maxMHz}");
            return cod == 0
                ? new(true, $"Clock travado em {minMHz}–{maxMHz} MHz (boost sustentado). Reversível.")
                : new(false, "Falha ao travar o clock (exige administrador): " + saida.Trim());
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Reseta o clock para o comportamento padrão (remove o lock).</summary>
    public static ResultadoOverclock ResetarClock()
    {
        try
        {
            var (cod, _) = Rodar("-rgc");
            return cod == 0 ? new(true, "Clock resetado para o padrão (boost dinâmico).") : new(false, "Falha ao resetar o clock.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Ajusta o power limit em watts dentro da faixa de fábrica. Reversível.</summary>
    public static ResultadoOverclock DefinirPowerLimit(double watts)
    {
        if (double.IsNaN(watts) || double.IsInfinity(watts) || watts <= 0) return new(false, "Power limit inválido.");
        try
        {
            var (cod, saida) = Rodar($"-pl {watts.ToString(CultureInfo.InvariantCulture)}");
            return cod == 0
                ? new(true, $"Power limit ajustado para {watts:0.##} W.")
                : new(false, "Falha ao ajustar o power limit: " + saida.Trim());
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Restaura o padrão: reseta o clock e volta o power limit ao padrão de fábrica.</summary>
    public static ResultadoOverclock RestaurarPadrao(double powerPadrao)
    {
        try
        {
            var (c1, s1) = Rodar("-rgc");
            int c2 = 0; string s2 = "";
            if (powerPadrao > 0) (c2, s2) = Rodar($"-pl {powerPadrao.ToString(CultureInfo.InvariantCulture)}");
            return c1 == 0 && c2 == 0
                ? new(true, "Overclock e power limit restaurados ao padrão de fábrica.")
                : new(false, $"Restauração parcial/falhou (exige administrador): {(s1 + " " + s2).Trim()}");
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
