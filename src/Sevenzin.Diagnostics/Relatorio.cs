using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sevenzin.Diagnostics;

/// <summary>Relatório "Analisar meu PC" em modo SOMENTE LEITURA (seção 21.8 / Fase 3).
/// Não aplica nenhuma alteração, não acessa a rede e não coleta identificadores.</summary>
public static class Relatorio
{
    public static List<ItemInventario> Coletar() => new List<Func<List<ItemInventario>>>
    {
        ColetorSistema.Coletar, ColetorCpu.Coletar, ColetorGpu.Coletar,
        ColetorMemoria.Coletar, ColetorArmazenamento.Coletar, ColetorEnergia.Coletar,
    }.SelectMany(f => f()).ToList();

    static string Marca(Confianca c) => c switch { Confianca.Fato => "Fato", Confianca.Inferencia => "Inferência", _ => "Hipótese" };

    public static string Markdown(IReadOnlyList<ItemInventario> itens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Analisar meu PC — inventário (somente leitura)").AppendLine();
        sb.AppendLine($"Gerado em {DateTime.Now:yyyy-MM-dd HH:mm} · leitura local, sem rede, sem alterações, sem identificadores pessoais.").AppendLine();
        sb.AppendLine("Cada dado traz a origem e o grau de confiança (**Fato**, **Inferência** ou **Hipótese**).").AppendLine();
        foreach (var grupo in itens.GroupBy(i => i.Categoria))
        {
            sb.AppendLine($"## {grupo.Key}").AppendLine();
            sb.AppendLine("| Item | Valor | Confiança | Fonte | Observação |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var i in grupo)
                sb.AppendLine($"| {i.Nome} | {i.Valor ?? "—"} | {Marca(i.Tipo)} | {i.Fonte} | {i.Observacao ?? ""} |");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string Json(IReadOnlyList<ItemInventario> itens) =>
        JsonSerializer.Serialize(new { gerado = DateTime.UtcNow, somenteLeitura = true, itens },
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() }, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
