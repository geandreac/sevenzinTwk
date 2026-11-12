using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Licença LOCAL do Sevenzin (substitui o KeyAuth/licença por servidor do original por algo
/// offline e transparente). A chave é guardada só na máquina; NÃO existe validação por servidor, HWID
/// ou Discord — a chave é um registro formal, não um bloqueio de funcionalidade.</summary>
public sealed record Licenca(string? Chave, DateTime? AtivadaUtc)
{
    public bool Ativa => ValidadorLicenca.ChaveValida(Chave);
}

/// <summary>Validação de formato da chave local: 4 grupos de 4 caracteres alfanuméricos (ex.: A1B2-C3D4-E5F6-G7H8).
/// Função pura (testável) — não é "proteção", é saneamento do que o usuário digita.</summary>
public static class ValidadorLicenca
{
    static readonly Regex Formato = new(@"^[A-Z0-9]{4}(-[A-Z0-9]{4}){3}$", RegexOptions.Compiled);

    public static string Normalizar(string? chave) => (chave ?? "").Trim().ToUpperInvariant().Replace(" ", "-");

    public static bool ChaveValida(string? chave) => Formato.IsMatch(Normalizar(chave));
}

/// <summary>Persistência da licença local (%LOCALAPPDATA%\Sevenzin\licenca.json).</summary>
public sealed class LicencaStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    readonly string _arquivo;

    public LicencaStore(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "licenca.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
    }

    public Licenca Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<Licenca>(File.ReadAllText(_arquivo), Opts) ?? new(null, null) : new(null, null); }
        catch { return new(null, null); }
    }

    public void Salvar(Licenca l)
    {
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(l, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}
