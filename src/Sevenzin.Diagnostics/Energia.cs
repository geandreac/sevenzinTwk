using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

public sealed record ResultadoEnergia(bool Ok, string Mensagem);

/// <summary>Plano de energia "Sevenzin": DUPLICA o plano ativo (os planos de fábrica do Windows permanecem
/// intactos — "originalidade do sistema"), renomeia a cópia para "Sevenzin" e a ativa. Totalmente reversível
/// (restaura o "Equilibrado" e apaga a cópia). Usa powercfg e exige administrador. Não toca em segurança.</summary>
public static class GestorEnergia
{
    public const string Nome = "SevenzinTwks";
    const string Equilibrado = "381b4222-f694-41f0-9685-ff5bb260df2e"; // GUID fixo do plano "Equilibrado" do Windows

    static readonly Regex RxGuid = new(@"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", RegexOptions.Compiled);

    /// <summary>Primeiro GUID na saída do powercfg (ex.: "Esquema de Energia GUID: &lt;guid&gt; (nome)"). Puro/testável.</summary>
    public static string? ExtrairGuid(string saida)
    {
        var m = RxGuid.Match(saida ?? "");
        return m.Success ? m.Value : null;
    }

    /// <summary>Nome amigável entre parênteses na saída (ex.: "(Equilibrado)"). Puro/testável.</summary>
    public static string? ExtrairNome(string linha)
    {
        var m = Regex.Match(linha ?? "", @"\(([^)]*)\)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>GUIDs de planos com o nome dado, a partir da saída de <c>powercfg /list</c>. Puro/testável.</summary>
    public static List<string> GuidsPorNome(string saidaList, string nome)
    {
        var r = new List<string>();
        foreach (var linha in (saidaList ?? "").Split('\n'))
            if (ExtrairNome(linha) is { } n && n.Equals(nome, StringComparison.OrdinalIgnoreCase) && ExtrairGuid(linha) is { } g)
                r.Add(g);
        return r;
    }

    static string Rodar(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var outp = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return outp;
    }

    public static string? GuidAtivo() => ExtrairGuid(Rodar("/getactivescheme"));
    public static string? NomeAtivo() => ExtrairNome(Rodar("/getactivescheme"));

    /// <summary>true = hibernação habilitada (HibernateEnabled=1); null = chave ausente/indeterminado. Só leitura.</summary>
    public static bool? HibernacaoAtiva()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power");
            return k?.GetValue("HibernateEnabled") is int v ? v == 1 : (bool?)null;
        }
        catch { return null; }
    }

    /// <summary>Liga/desliga a hibernação via powercfg e relê o estado real. Exige administrador.</summary>
    public static ResultadoEnergia DefinirHibernacao(bool ativa)
    {
        try
        {
            Rodar(ativa ? "/hibernate on" : "/hibernate off");
            bool? real = HibernacaoAtiva();
            bool ok = real == ativa;
            return ok
                ? new(true, ativa ? "Hibernação reativada." : "Hibernação desativada (valor literal do original).")
                : new(false, "O comando foi enviado, mas o estado lido não confirmou a mudança — execute como administrador.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Duplica o plano ativo, renomeia a cópia para "Sevenzin", aplica os 134 índices de energia
    /// LITERAIS do original (geandre.exe) à cópia e a ativa. Reversível: restaurar volta ao "Equilibrado".</summary>
    public static ResultadoEnergia AplicarSevenzin()
    {
        try
        {
            var ativo = GuidAtivo();
            if (ativo is null) return new(false, "Não foi possível identificar o plano de energia ativo.");

            var alvo = GuidsPorNome(Rodar("/list"), Nome).FirstOrDefault();
            if (alvo is null)
            {
                alvo = ExtrairGuid(Rodar($"-duplicatescheme {ativo}"));
                if (alvo is null) return new(false, "Falha ao duplicar o plano ativo. Reabra como administrador e tente de novo.");
                Rodar($"-changename {alvo} \"{Nome}\" \"Plano de energia do SevenzinTwks, baseado no seu plano ativo. Reversível.\"");
            }

            // Aplica os 134 índices AC e os 134 índices DC do ORIGINAL à cópia (paridade total; só o plano "SevenzinTwks" é tocado).
            int falhas = 0;
            foreach (var (sub, conf, valor) in IndicesEnergiaOriginal.Todos)
            {
                var saida = Rodar($"-setacvalueindex {alvo} {sub} {conf} {valor}");
                if (saida.Trim().Length > 0) falhas++;
            }
            foreach (var (sub, conf, valor) in IndicesEnergiaOriginal.TodosDc)
            {
                var saida = Rodar($"-setdcvalueindex {alvo} {sub} {conf} {valor}");
                if (saida.Trim().Length > 0) falhas++;
            }

            Rodar($"-setactive {alvo}");
            bool ativoOk = GuidAtivo() == alvo;
            if (!ativoOk) return new(false, "O plano foi criado, mas não ficou ativo. É preciso executar como administrador.");
            int totalIndices = IndicesEnergiaOriginal.Todos.Count + IndicesEnergiaOriginal.TodosDc.Count;
            return falhas == 0
                ? new(true, $"Plano \"{Nome}\" criado e ativado com os {IndicesEnergiaOriginal.Todos.Count} índices AC + {IndicesEnergiaOriginal.TodosDc.Count} índices DC do original. Os planos de fábrica continuam intactos.")
                : new(true, $"Plano \"{Nome}\" ativado, mas {falhas} de {totalIndices} índices falharam (verifique administrador).");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Volta ao "Equilibrado" e remove os planos "Sevenzin" criados. Reversão completa.</summary>
    public static ResultadoEnergia Restaurar()
    {
        try
        {
            Rodar($"-setactive {Equilibrado}"); // precisa estar inativo para poder apagar
            foreach (var g in GuidsPorNome(Rodar("/list"), Nome)) Rodar($"-delete {g}");
            return new(true, "Plano restaurado para 'Equilibrado' e o plano 'SevenzinTwks' removido.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
