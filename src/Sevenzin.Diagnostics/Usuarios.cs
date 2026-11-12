using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

public enum PapelUsuario { Administrador, Usuario }

/// <summary>Conta local. A senha NUNCA é guardada em texto — só o hash bcrypt (salt embutido no próprio hash).
/// 2FA opcional: <paramref name="TotpSegredo"/> (Base32) + <paramref name="TotpAtivo"/> — verificação local (TOTP).</summary>
public sealed record Usuario(string Email, string Nome, PapelUsuario Papel, string HashSenha, DateTime CriadoUtc,
    string? TotpSegredo = null, bool TotpAtivo = false);

/// <summary>Força da senha por requisito (medidor do Figma ADM-02). Nível 0..4; Ok quando todos atendidos.</summary>
public sealed record ForcaSenha(bool Tamanho, bool Caixa, bool Numero, bool Simbolo, bool SemDadosPessoais)
{
    public int Nivel => (Tamanho ? 1 : 0) + (Caixa ? 1 : 0) + (Numero ? 1 : 0) + (Simbolo ? 1 : 0);
    public bool Ok => Tamanho && Caixa && Numero && Simbolo && SemDadosPessoais;
}

/// <summary>Hash de senha com bcrypt (work factor 12, salt aleatório embutido), verificação resistente a timing.</summary>
public static class SegurancaSenha
{
    const int FatorTrabalho = 12;

    public static string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, FatorTrabalho);

    public static bool Confere(string senha, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(senha, hash); }
        catch { return false; }
    }

    // Hash válido de um valor aleatório: serve para gastar o mesmo tempo de verificação quando o
    // e-mail não existe, evitando distinguir "sem conta" de "senha errada" pelo tempo de resposta.
    public static readonly string HashFalso = Gerar(Guid.NewGuid().ToString("N"));
}

/// <summary>Regras de validação de e-mail e senha. Devolve a lista de erros (vazia = válido).</summary>
public static class ValidacaoCredenciais
{
    static readonly Regex EmailRe = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string NormalizarEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>Parte do e-mail antes do @ (usada como nome padrão quando não há nome completo).</summary>
    public static string NomeDeEmail(string? email)
    {
        var e = NormalizarEmail(email);
        var at = e.IndexOf('@');
        return at > 0 ? e[..at] : e;
    }

    public static IReadOnlyList<string> ValidarEmail(string? email)
    {
        var erros = new List<string>();
        var e = (email ?? "").Trim();
        if (e.Length == 0) erros.Add("Informe o e-mail.");
        else if (e.Length > 254 || !EmailRe.IsMatch(e)) erros.Add("E-mail inválido.");
        return erros;
    }

    /// <summary>Avalia a senha pelos requisitos do Figma (≥12, maiúsc/minúsc, número, símbolo, sem dados pessoais).</summary>
    public static ForcaSenha AvaliarSenha(string? senha, string? email = null, string? nome = null)
    {
        senha ??= "";
        bool tamanho = senha.Length >= 12;
        bool caixa = senha.Any(char.IsLower) && senha.Any(char.IsUpper);
        bool numero = senha.Any(char.IsDigit);
        bool simbolo = senha.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        bool semPessoais = senha.Length == 0 || !ContemDadoPessoal(senha, email, nome);
        return new ForcaSenha(tamanho, caixa, numero, simbolo, semPessoais);
    }

    static bool ContemDadoPessoal(string senha, string? email, string? nome)
    {
        var s = senha.ToLowerInvariant();
        foreach (var tok in Tokens(email, nome))
            if (tok.Length >= 3 && s.Contains(tok)) return true;
        return false;
    }

    static IEnumerable<string> Tokens(string? email, string? nome)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var e = email.Trim().ToLowerInvariant();
            yield return e;
            var at = e.IndexOf('@');
            if (at > 0) yield return e[..at];
        }
        if (!string.IsNullOrWhiteSpace(nome))
            foreach (var p in nome.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return p;
    }

    public static IReadOnlyList<string> ValidarSenha(string? senha, string? email = null, string? nome = null)
    {
        var f = AvaliarSenha(senha, email, nome);
        var erros = new List<string>();
        if (!f.Tamanho) erros.Add("A senha deve ter ao menos 12 caracteres.");
        if (!f.Caixa) erros.Add("Use letras maiúsculas e minúsculas.");
        if (!f.Numero) erros.Add("Inclua ao menos um número.");
        if (!f.Simbolo) erros.Add("Inclua ao menos um símbolo (!@#$…).");
        if (!f.SemDadosPessoais) erros.Add("A senha não pode conter seu nome nem o e-mail.");
        return erros;
    }

    /// <summary>Gera uma senha aleatória de 16 caracteres que atende a todos os requisitos.</summary>
    public static string GerarSenhaForte()
    {
        const string min = "abcdefghijkmnpqrstuvwxyz", mai = "ABCDEFGHJKLMNPQRSTUVWXYZ", num = "23456789", sim = "!@#$%&*?";
        char Pega(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
        var chars = new List<char> { Pega(min), Pega(min), Pega(mai), Pega(mai), Pega(num), Pega(num), Pega(sim), Pega(sim) };
        string todos = min + mai + num + sim;
        while (chars.Count < 16) chars.Add(Pega(todos));
        for (int i = chars.Count - 1; i > 0; i--) { int j = RandomNumberGenerator.GetInt32(i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars.ToArray());
    }
}

/// <summary>Armazém de contas (abstração de persistência — permite trocar arquivo por memória nos testes).</summary>
public interface IRepositorioUsuarios
{
    IReadOnlyList<Usuario> Todos { get; }
    Usuario? PorEmail(string email);
    void Adicionar(Usuario u);
    void Atualizar(Usuario u);
    bool Remover(string email);
}

/// <summary>Persistência em JSON local (%LOCALAPPDATA%\Sevenzin\usuarios.json). Sem servidor, sem HWID/Discord.</summary>
public sealed class RepositorioUsuariosArquivo : IRepositorioUsuarios
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    readonly string _arquivo;
    readonly List<Usuario> _usuarios;

    public RepositorioUsuariosArquivo(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "usuarios.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _usuarios = Carregar();
    }

    public IReadOnlyList<Usuario> Todos => _usuarios;
    public Usuario? PorEmail(string email) => _usuarios.FirstOrDefault(u => u.Email.Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase));
    public void Adicionar(Usuario u) { _usuarios.Add(u); Salvar(); }
    public void Atualizar(Usuario u)
    {
        int i = _usuarios.FindIndex(x => x.Email.Equals(u.Email, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) { _usuarios[i] = u; Salvar(); }
    }
    public bool Remover(string email)
    {
        bool algum = _usuarios.RemoveAll(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) > 0;
        if (algum) Salvar();
        return algum;
    }

    List<Usuario> Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<Usuario>>(File.ReadAllText(_arquivo), Opts) ?? new() : new(); }
        catch { return new(); }
    }
    void Salvar() => File.WriteAllText(_arquivo, JsonSerializer.Serialize(_usuarios, Opts));
}

/// <summary>Repositório em memória (para testes).</summary>
public sealed class RepositorioUsuariosMemoria : IRepositorioUsuarios
{
    readonly List<Usuario> _usuarios = new();
    public IReadOnlyList<Usuario> Todos => _usuarios;
    public Usuario? PorEmail(string email) => _usuarios.FirstOrDefault(u => u.Email.Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase));
    public void Adicionar(Usuario u) => _usuarios.Add(u);
    public void Atualizar(Usuario u)
    {
        int i = _usuarios.FindIndex(x => x.Email.Equals(u.Email, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) _usuarios[i] = u;
    }
    public bool Remover(string email) => _usuarios.RemoveAll(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) > 0;
}

/// <summary>Criação/remoção de contas com validação e hash. Não autentica (ver <see cref="ServicoAutenticacao"/>).</summary>
public sealed class ServicoContas
{
    readonly IRepositorioUsuarios _repo;
    public ServicoContas(IRepositorioUsuarios repo) => _repo = repo;

    public IReadOnlyList<Usuario> Todos => _repo.Todos;
    public bool Existe(string email) => _repo.PorEmail(ValidacaoCredenciais.NormalizarEmail(email)) is not null;

    /// <summary>Cria uma conta. Lança <see cref="ArgumentException"/> se inválida ou já existir.
    /// <paramref name="nome"/> vazio é substituído pela parte local do e-mail.</summary>
    public Usuario Criar(string email, string senha, PapelUsuario papel, string? nome = null)
    {
        var e = ValidacaoCredenciais.NormalizarEmail(email);
        var n = string.IsNullOrWhiteSpace(nome) ? ValidacaoCredenciais.NomeDeEmail(e) : nome.Trim();
        var erros = ValidacaoCredenciais.ValidarEmail(e).Concat(ValidacaoCredenciais.ValidarSenha(senha, e, n)).ToList();
        if (erros.Count > 0) throw new ArgumentException(string.Join(" ", erros));
        if (Existe(e)) throw new ArgumentException("Já existe uma conta com esse e-mail.");

        var u = new Usuario(e, n, papel, SegurancaSenha.Gerar(senha), DateTime.UtcNow);
        _repo.Adicionar(u);
        return u;
    }

    public bool Remover(string email) => _repo.Remover(ValidacaoCredenciais.NormalizarEmail(email));

    /// <summary>Inicia a ativação do 2FA: grava um segredo novo (ainda INATIVO) e devolve o Base32
    /// para o usuário cadastrar no app autenticador. null se a conta não existe.</summary>
    public string? GerarSegredo2FA(string email)
    {
        var e = ValidacaoCredenciais.NormalizarEmail(email);
        var u = _repo.PorEmail(e);
        if (u is null) return null;
        var segredo = Totp.GerarSegredo();
        _repo.Atualizar(u with { TotpSegredo = segredo, TotpAtivo = false });
        return segredo;
    }

    /// <summary>Confirma o 2FA com um código TOTP válido — só então o fator fica ATIVO.</summary>
    public bool Confirmar2FA(string email, string? codigo, DateTime? agora = null)
    {
        var u = _repo.PorEmail(ValidacaoCredenciais.NormalizarEmail(email));
        if (u?.TotpSegredo is not { } segredo) return false;
        if (!Totp.Confere(segredo, codigo, agora ?? DateTime.UtcNow)) return false;
        _repo.Atualizar(u with { TotpAtivo = true });
        return true;
    }

    /// <summary>Desativa o 2FA da conta (remove o segredo).</summary>
    public void Desativar2FA(string email)
    {
        var e = ValidacaoCredenciais.NormalizarEmail(email);
        var u = _repo.PorEmail(e);
        if (u is not null) _repo.Atualizar(u with { TotpSegredo = null, TotpAtivo = false });
    }
}
