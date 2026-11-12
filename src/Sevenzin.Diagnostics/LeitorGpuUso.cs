using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Uso de GPU (%) em tempo real pelos contadores de performance "GPU Engine" do Windows (Win10+).
/// Somatório da utilização das engines 3D (como o Gerenciador de Tarefas). Só-leitura; sem driver/overclock.
/// Devolve null quando a categoria/contador não existe (Windows antigo, GPU sem suporte) — nunca 0 forjado.</summary>
public static class LeitorGpuUso
{
    const string Categoria = "GPU Engine";
    const string Contador = "Utilization Percentage";

    /// <summary>Soma a utilização das instâncias de engine 3D e limita a 0..100. Função pura (testável).</summary>
    public static double AgregarUso3D(IEnumerable<(string Instancia, float Valor)> amostras)
    {
        double soma = amostras
            .Where(a => a.Instancia.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Valor);
        return Math.Clamp(soma, 0, 100);
    }

    /// <summary>Lê o uso agregado da GPU. null = indisponível (categoria ausente ou sem instâncias).</summary>
    public static double? LerUtilizacao()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(Categoria)) return null;
            var cat = new PerformanceCounterCategory(Categoria);
            var amostras = new List<(string, float)>();
            foreach (var inst in cat.GetInstanceNames())
            {
                if (!inst.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using var c = new PerformanceCounter(Categoria, Contador, inst, readOnly: true);
                    amostras.Add((inst, c.NextValue())); // "Utilization Percentage" é PERF_RAW_FRACTION: 1 leitura já traz valor real
                }
                catch { /* instância morreu entre enumerar e ler — ignora */ }
            }
            return amostras.Count > 0 ? AgregarUso3D(amostras) : null;
        }
        catch { return null; } // sem contadores de GPU neste equipamento
    }
}
