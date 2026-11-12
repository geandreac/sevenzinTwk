using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Uso de CPU do sistema (%) via Win32 <c>GetSystemTimes</c> — paridade com o original
/// (05_metricas §3, "tempo de CPU por thread" ampliado para o total do sistema). Só leitura e mais
/// barato que WMI: cada <see cref="LeitorCpuUso.Ler"/> é uma chamada de kernel.</summary>
public static class LeitorCpuUso
{
    [DllImport("kernel32.dll")]
    static extern bool GetSystemTimes(out long tempoOcioso, out long tempoKernel, out long tempoUsuario);

    /// <summary>Instantâneo dos tempos acumulados (100 ns) do sistema desde o boot.</summary>
    public readonly record struct Instantaneo(long Ocioso, long Kernel, long Usuario);

    /// <summary>Lê o instantâneo atual; null quando a API falha (nunca valores forjados).</summary>
    public static Instantaneo? LerInstantaneo()
    {
        try
        {
            return GetSystemTimes(out long idle, out long kernel, out long user)
                ? new Instantaneo(idle, kernel, user)
                : null;
        }
        catch { return null; }
    }

    /// <summary>Uso de CPU (%) entre dois instantâneos. Função pura (testável).
    /// Uso = 1 − Δocioso / (Δkernel + Δusuário). Devolve null quando o intervalo é nulo,
    /// os contadores voltaram (sleep/hibernação) ou o resultado é negativo — nunca 0 forjado.</summary>
    public static double? CalcularUso(Instantaneo anterior, Instantaneo atual)
    {
        long dIdle = atual.Ocioso - anterior.Ocioso;
        long dTotal = (atual.Kernel - anterior.Kernel) + (atual.Usuario - anterior.Usuario);
        if (dTotal <= 0 || dIdle < 0) return null;
        double uso = (1.0 - (double)dIdle / dTotal) * 100.0;
        return Math.Clamp(uso, 0.0, 100.0);
    }
}

/// <summary>Leitor incremental: guarda o instantâneo anterior e devolve a % de CPU desde a última
/// chamada. A 1ª chamada devolve null (ainda não há intervalo para medir).</summary>
public sealed class MedidorCpuUso
{
    LeitorCpuUso.Instantaneo? _anterior;

    public double? Ler()
    {
        var agora = LeitorCpuUso.LerInstantaneo();
        if (agora is null) return null;
        var antes = _anterior;
        _anterior = agora;
        return antes is { } a ? LeitorCpuUso.CalcularUso(a, agora.Value) : null;
    }
}
