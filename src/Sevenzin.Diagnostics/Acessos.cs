namespace Sevenzin.Diagnostics;

/// <summary>Uma entrada do log de acessos (tentativa de login).</summary>
public sealed record Acesso(DateTime Utc, string Email, string Resultado, bool Sucesso);

/// <summary>Log de acessos compartilhado entre os dois aplicativos (otimizador e admin), em
/// <c>%LOCALAPPDATA%\Sevenzin\acessos.log</c> (TSV). Alimenta o dashboard de acessos do app Admin.</summary>
public static class RegistroAcessos
{
    static readonly string Arq = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "acessos.log");

    public static void Registrar(string email, string resultado, bool sucesso)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Arq)!);
            File.AppendAllText(Arq,
                $"{DateTime.UtcNow:o}\t{(email ?? "").Replace('\t', ' ')}\t{(resultado ?? "").Replace('\t', ' ')}\t{(sucesso ? 1 : 0)}\n");
        }
        catch { }
    }

    /// <summary>Converte uma linha TSV em <see cref="Acesso"/>. Puro/testável. null se a linha for inválida.</summary>
    public static Acesso? Parse(string linha)
    {
        var p = (linha ?? "").Split('\t');
        if (p.Length < 4) return null;
        if (!DateTime.TryParse(p[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc)) return null;
        return new Acesso(utc, p[1], p[2], p[3].Trim() == "1");
    }

    public static List<Acesso> Ler()
    {
        try
        {
            if (!File.Exists(Arq)) return new();
            return File.ReadAllLines(Arq).Select(Parse).Where(a => a is not null).Select(a => a!).ToList();
        }
        catch { return new(); }
    }
}
