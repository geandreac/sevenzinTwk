using System.Globalization;

namespace Sevenzin.Diagnostics;

/// <summary>Um evento do registro de auditoria dos tweaks (aplicar/desfazer).</summary>
public sealed record EventoAuditoria(DateTime Utc, string Acao, string Tweak, string De, string Para);

/// <summary>Lê o registro local de auditoria gravado pelo MotorTweaks (linha: ISO\tacao\tid\tde-&gt;para).
/// Somente leitura, local — base do painel Admin.</summary>
public static class LeitorAuditoria
{
    public static string ArquivoPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "tweaks-auditoria.log");

    public static IReadOnlyList<EventoAuditoria> Ler(string? caminho = null)
    {
        var arq = caminho ?? ArquivoPadrao;
        var lista = new List<EventoAuditoria>();
        if (!File.Exists(arq)) return lista;
        foreach (var linha in File.ReadAllLines(arq))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var p = linha.Split('\t');
            if (p.Length < 3) continue;
            DateTime.TryParse(p[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc);
            var (de, para) = SepararDePara(p.Length > 3 ? p[3] : "");
            lista.Add(new EventoAuditoria(utc, p[1], p[2], de, para));
        }
        return lista;
    }

    static (string De, string Para) SepararDePara(string s)
    {
        int i = s.IndexOf("->", StringComparison.Ordinal);
        return i < 0 ? ("", "") : (s[..i], s[(i + 2)..]);
    }
}
