using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

public sealed record ResultadoEnergia(bool Ok, string Mensagem);

/// <summary>Plano de energia "Sevenzin": DUPLICA o plano ativo (os planos de fábrica do Windows permanecem
/// intactos — "originalidade do sistema"), renomeia a cópia para "Sevenzin" e a ativa. Totalmente reversível
/// (restaura o "Equilibrado" e apaga a cópia). Usa powercfg e exige administrador. Não toca em segurança.</summary>
public static class GestorEnergia
{
    public const string Nome = "Sevenzin";
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

    /// <summary>Duplica o plano ativo, renomeia a cópia para "Sevenzin" e a ativa. Se já existe um "Sevenzin", só ativa.</summary>
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
                Rodar($"-changename {alvo} \"{Nome}\" \"Plano de energia do Sevenzin, baseado no seu plano ativo. Reversível.\"");
            }
            Rodar($"-setactive {alvo}");
            return GuidAtivo() == alvo
                ? new(true, $"Plano \"{Nome}\" criado e ativado. Os planos originais do Windows continuam intactos.")
                : new(false, "O plano foi criado, mas não ficou ativo. É preciso executar como administrador.");
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
            return new(true, "Plano restaurado para 'Equilibrado' e o plano 'Sevenzin' removido.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
