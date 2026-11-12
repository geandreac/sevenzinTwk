using System.Text.Json;
using Sevenzin.Diagnostics;

if (args.Contains("--selftest"))
    return SelfTest();

if (args.Contains("--telemetria"))
    return Telemetria(args);

var itens = Relatorio.Coletar();
var dir = Directory.GetCurrentDirectory();
var md = Path.Combine(dir, "relatorio-pc.md");
var js = Path.Combine(dir, "relatorio-pc.json");
File.WriteAllText(md, Relatorio.Markdown(itens));
File.WriteAllText(js, Relatorio.Json(itens));

Console.WriteLine($"Inventário somente leitura: {itens.Count} itens em {itens.Select(i => i.Categoria).Distinct().Count()} categorias.");
Console.WriteLine($"  {md}");
Console.WriteLine($"  {js}");
return 0;

// Sessão de diagnóstico real (§21.2), iniciada e encerrada aqui, só leitura.
static int Telemetria(string[] args)
{
    int i = Array.IndexOf(args, "--telemetria");
    int seg = (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var s)) ? Math.Clamp(s, 1, 120) : 10;
    Console.WriteLine($"Sessão de diagnóstico (só leitura), {seg}s, amostrando a cada 500 ms...");
    var sessao = new SessaoTelemetria(500);
    sessao.Iniciar();
    Thread.Sleep(seg * 1000);
    var resumo = sessao.Parar();
    Console.WriteLine($"Amostras: {resumo.Amostras} · overhead medido da coleta: {resumo.OverheadPct:0.00}%");
    foreach (var m in resumo.Metricas)
        Console.WriteLine($"  {m.Nome}: média {m.Media:0.0} {m.Unidade} · p95 {m.P95:0.0} · p99 {m.P99:0.0} · desvio {m.Desvio:0.0} · faixa {m.Min:0.0}–{m.Max:0.0} (n={m.N})");
    return resumo.Amostras > 0 ? 0 : 1;
}

static int SelfTest()
{
    int falhas = 0;
    void Check(bool ok, string nome) { Console.WriteLine($"  [{(ok ? "ok" : "FALHA")}] {nome}"); if (!ok) falhas++; }

    var itens = Relatorio.Coletar();
    Check(itens.Count > 0, "coleta retorna itens");
    Check(itens.All(i => !string.IsNullOrEmpty(i.Categoria) && !string.IsNullOrEmpty(i.Nome) && !string.IsNullOrEmpty(i.Fonte)), "todo item tem categoria, nome e fonte");

    // JSON válido e com round-trip
    var json = Relatorio.Json(itens);
    try { using var _ = JsonDocument.Parse(json); Check(true, "JSON do relatório é válido"); }
    catch { Check(false, "JSON do relatório é válido"); }

    // nenhum identificador pessoal vazou para os valores
    var proibidos = new[] { Environment.MachineName, Environment.UserName, Environment.UserDomainName }
        .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 2).ToArray();
    var textos = string.Join("\n", itens.Select(i => $"{i.Valor} {i.Observacao}"));
    Check(proibidos.All(p => !textos.Contains(p, StringComparison.OrdinalIgnoreCase)),
        "relatório não contém nome do PC, usuário nem domínio");

    // a marcação Fato/Inferência/Hipótese está presente no Markdown
    var md = Relatorio.Markdown(itens);
    Check(md.Contains("Confiança") && md.Contains("Fato"), "Markdown traz a coluna de confiança");

    Console.WriteLine(falhas == 0 ? "self-test OK" : $"self-test com {falhas} falha(s)");
    return falhas == 0 ? 0 : 1;
}
