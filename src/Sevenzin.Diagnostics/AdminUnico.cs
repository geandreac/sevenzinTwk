using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Identidade do ADMINISTRADOR ÚNICO (Geandre): provisionada por CONFIGURAÇÃO SEGURA
/// (variáveis de ambiente SEVENZIN_ADMIN_EMAIL/SEVENZIN_ADMIN_SENHA) — nunca por credencial no código.
/// Sem identidade configurada, NENHUM usuário recebe privilégios administrativos.</summary>
public static class IdentidadeAdmin
{
    public const string VarEmail = "SEVENZIN_ADMIN_EMAIL";
    public const string VarSenha = "SEVENZIN_ADMIN_SENHA";

    /// <summary>E-mail do administrador provisionado (normalizado), ou null se não configurado.</summary>
    public static string? EmailConfigurado(Func<string, string?>? lerEnv = null)
    {
        lerEnv ??= Environment.GetEnvironmentVariable;
        var bruto = lerEnv(VarEmail);
        return string.IsNullOrWhiteSpace(bruto) ? null : ValidacaoCredenciais.NormalizarEmail(bruto);
    }

    /// <summary>true somente quando o e-mail é EXATAMENTE a identidade provisionada. Função pura (testável).</summary>
    public static bool EhAdminProtegido(string? email, string? emailConfigurado) =>
        emailConfigurado is not null
        && ValidacaoCredenciais.NormalizarEmail(email).Equals(emailConfigurado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gera um código de convite de 12 caracteres (sem ambíguos).</summary>
    public static string GerarCodigoConvite()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[12];
        for (int i = 0; i < chars.Length; i++) chars[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
        return new string(chars);
    }

    /// <summary>Formato aceitável de código de convite. Função pura (testável).</summary>
    public static bool CodigoConviteValido(string? codigo) =>
        codigo is not null && codigo.Trim().Length == 12 && codigo.Trim().All(c => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(char.ToUpperInvariant(c)));

    /// <summary>Hash do código (só o hash é persistido — o código em claro nunca fica em disco).</summary>
    public static string HashCodigo(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo.Trim().ToUpperInvariant())));
}

/// <summary>Armazém de convites (somente o ADMINISTRADOR ÚNICO gera). Códigos de uso único,
/// persistidos apenas como SHA-256. %LOCALAPPDATA%\Sevenzin\convites.json.</summary>
public sealed class RepositorioConvites
{
    sealed record Linha(string Hash, DateTime CriadoUtc, bool Usado);
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    readonly object _trava = new();
    readonly string _arquivo;
    readonly List<Linha> _linhas;

    public RepositorioConvites(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "convites.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _linhas = Carregar();
    }

    public int Disponiveis { get { lock (_trava) return _linhas.Count(l => !l.Usado); } }

    /// <summary>Gera um convite e devolve o CÓDIGO EM CLARO uma única vez (para o admin repassar).</summary>
    public string Gerar()
    {
        var codigo = IdentidadeAdmin.GerarCodigoConvite();
        lock (_trava)
        {
            _linhas.Add(new Linha(IdentidadeAdmin.HashCodigo(codigo), DateTime.UtcNow, false));
            Salvar();
        }
        return codigo;
    }

    /// <summary>Consome um convite (validação + uso único). false = código inválido ou já usado.</summary>
    public bool Consumir(string? codigo)
    {
        if (!IdentidadeAdmin.CodigoConviteValido(codigo)) return false;
        var hash = IdentidadeAdmin.HashCodigo(codigo!);
        lock (_trava)
        {
            var linha = _linhas.FirstOrDefault(l => l.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) && !l.Usado);
            if (linha is null) return false;
            _linhas[_linhas.IndexOf(linha)] = linha with { Usado = true };
            Salvar();
            return true;
        }
    }

    List<Linha> Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<Linha>>(File.ReadAllText(_arquivo), Opts) ?? new() : new(); }
        catch { return new(); }
    }
    void Salvar()
    {
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_linhas, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}
