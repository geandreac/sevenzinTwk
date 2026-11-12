using System.Security.Cryptography;

namespace Sevenzin.Diagnostics;

/// <summary>Política de força bruta: nº de falhas numa janela que dispara o bloqueio temporário.</summary>
public sealed record PoliticaForcaBruta(int MaxTentativas = 5, TimeSpan? Janela = null, TimeSpan? Bloqueio = null)
{
    public TimeSpan JanelaEf => Janela ?? TimeSpan.FromMinutes(5);
    public TimeSpan BloqueioEf => Bloqueio ?? TimeSpan.FromMinutes(5);
}

public enum StatusAuth { Sucesso, CredenciaisInvalidas, Bloqueado, DadosInvalidos, Exige2FA }

/// <summary>Resultado da autenticação. Mensagens são genéricas — não revelam se o e-mail existe.</summary>
public sealed record ResultadoAuth(StatusAuth Status, Usuario? Usuario = null, TimeSpan? Espere = null, string Mensagem = "")
{
    public bool Ok => Status == StatusAuth.Sucesso;
}

/// <summary>Autentica por e-mail+senha com proteção a força bruta (limite + bloqueio temporário) e
/// resposta genérica (não distingue "e-mail inexistente" de "senha errada", nem por texto nem por tempo).</summary>
public sealed class ServicoAutenticacao
{
    const string ErroGenerico = "E-mail ou senha inválidos.";
    const string MsgBloqueio = "Muitas tentativas. Aguarde e tente novamente.";

    readonly IRepositorioUsuarios _repo;
    readonly Func<DateTime> _agora;
    readonly PoliticaForcaBruta _pol;

    sealed class Registro { public int Falhas; public DateTime JanelaInicioUtc; public DateTime? BloqueadoAteUtc; }
    readonly Dictionary<string, Registro> _tentativas = new(StringComparer.OrdinalIgnoreCase);

    public ServicoAutenticacao(IRepositorioUsuarios repo, Func<DateTime>? agora = null, PoliticaForcaBruta? pol = null)
    {
        _repo = repo;
        _agora = agora ?? (() => DateTime.UtcNow);
        _pol = pol ?? new PoliticaForcaBruta();
    }

    public ResultadoAuth Autenticar(string? email, string? senha, string? codigo2fa = null)
    {
        var e = ValidacaoCredenciais.NormalizarEmail(email);
        var agora = _agora();

        if (e.Length == 0 || string.IsNullOrEmpty(senha))
            return new ResultadoAuth(StatusAuth.DadosInvalidos, Mensagem: "Informe e-mail e senha.");

        if (_tentativas.TryGetValue(e, out var reg) && reg.BloqueadoAteUtc is { } ate)
        {
            if (agora < ate) return new ResultadoAuth(StatusAuth.Bloqueado, Espere: ate - agora, Mensagem: MsgBloqueio);
            _tentativas.Remove(e); // bloqueio expirou → recomeça
        }

        var u = _repo.PorEmail(e);
        bool ok = u is not null && SegurancaSenha.Confere(senha, u.HashSenha);
        if (u is null) SegurancaSenha.Confere(senha, SegurancaSenha.HashFalso); // equaliza o tempo quando não há conta

        if (!ok)
        {
            RegistrarFalha(e, agora);
            if (_tentativas.TryGetValue(e, out var r) && r.BloqueadoAteUtc is { } bloqAte && agora < bloqAte)
                return new ResultadoAuth(StatusAuth.Bloqueado, Espere: bloqAte - agora, Mensagem: MsgBloqueio);
            return new ResultadoAuth(StatusAuth.CredenciaisInvalidas, Mensagem: ErroGenerico);
        }

        // Senha correta; 2FA local opcional (TOTP).
        if (u!.TotpAtivo && u.TotpSegredo is { } segredo)
        {
            if (string.IsNullOrWhiteSpace(codigo2fa))
                return new ResultadoAuth(StatusAuth.Exige2FA, u, Mensagem: "Informe o código do autenticador.");
            if (!Totp.Confere(segredo, codigo2fa, agora))
            {
                RegistrarFalha(e, agora);
                return new ResultadoAuth(StatusAuth.CredenciaisInvalidas, Mensagem: "Código de verificação inválido.");
            }
        }

        _tentativas.Remove(e);
        return new ResultadoAuth(StatusAuth.Sucesso, u);
    }

    public bool EstaBloqueado(string? email)
    {
        var e = ValidacaoCredenciais.NormalizarEmail(email);
        return _tentativas.TryGetValue(e, out var r) && r.BloqueadoAteUtc is { } ate && _agora() < ate;
    }

    void RegistrarFalha(string email, DateTime agora)
    {
        if (!_tentativas.TryGetValue(email, out var reg) || agora - reg.JanelaInicioUtc > _pol.JanelaEf)
            reg = new Registro { JanelaInicioUtc = agora, Falhas = 0 };
        reg.Falhas++;
        if (reg.Falhas >= _pol.MaxTentativas) reg.BloqueadoAteUtc = agora + _pol.BloqueioEf;
        _tentativas[email] = reg;
    }
}

/// <summary>Sessão autenticada com token opaco e validade (expiração absoluta, renovável).</summary>
public sealed record Sessao(string Token, Usuario Usuario, DateTime ExpiraUtc);

/// <summary>Cria, valida, renova e encerra a sessão atual. Expiração deslizante (renova a cada atividade).</summary>
public sealed class ServicoSessao
{
    readonly Func<DateTime> _agora;
    readonly TimeSpan _duracao;

    public ServicoSessao(Func<DateTime>? agora = null, TimeSpan? duracao = null)
    {
        _agora = agora ?? (() => DateTime.UtcNow);
        _duracao = duracao ?? TimeSpan.FromMinutes(30);
    }

    public Sessao? Atual { get; private set; }

    public Sessao Abrir(Usuario u)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Atual = new Sessao(token, u, _agora() + _duracao);
        return Atual;
    }

    public bool Valida => Atual is { } s && _agora() < s.ExpiraUtc;

    /// <summary>Estende a validade se ainda válida; se expirou, encerra e devolve null.</summary>
    public Sessao? Renovar()
    {
        if (!Valida) { Atual = null; return null; }
        Atual = Atual! with { ExpiraUtc = _agora() + _duracao };
        return Atual;
    }

    public void Encerrar() => Atual = null;

    public bool PodeAcessarAdmin => Valida && Atual!.Usuario.Papel == PapelUsuario.Administrador;
}

/// <summary>Autorização por papel (função pura — facilita teste de "sem permissão").</summary>
public static class Autorizacao
{
    public static bool PodeAcessarAdmin(Sessao? s, DateTime agora)
        => s is not null && agora < s.ExpiraUtc && s.Usuario.Papel == PapelUsuario.Administrador;
}

/// <summary>Semeia o administrador inicial a partir de variáveis de ambiente — nunca de credenciais no código.</summary>
public static class SemeadorAdmin
{
    public const string VarEmail = "SEVENZIN_ADMIN_EMAIL";
    public const string VarSenha = "SEVENZIN_ADMIN_SENHA";

    /// <summary>Cria o admin das variáveis de ambiente, se definidas e ainda não existir. Devolve o e-mail semeado ou null.</summary>
    public static string? Semear(ServicoContas contas, Func<string, string?> lerEnv)
    {
        var email = lerEnv(VarEmail);
        var senha = lerEnv(VarSenha);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return null;

        var e = ValidacaoCredenciais.NormalizarEmail(email);
        if (contas.Existe(e)) return null;
        var nome = ValidacaoCredenciais.NomeDeEmail(e);
        // env inválido não deve derrubar a inicialização — apenas não semeia.
        if (ValidacaoCredenciais.ValidarEmail(e).Count > 0 || ValidacaoCredenciais.ValidarSenha(senha, e, nome).Count > 0) return null;
        contas.Criar(e, senha, PapelUsuario.Administrador, nome);
        return e;
    }
}
