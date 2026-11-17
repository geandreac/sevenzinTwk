namespace Sevenzin.Diagnostics;

/// <summary>Regras puras (testáveis) do fluxo "Esqueci minha senha" por CÓDIGO de e-mail (Supabase Auth):
/// 1) <c>POST /auth/v1/recover</c> envia o e-mail com o código ({{ .Token }} no template "Reset Password");
/// 2) <c>POST /auth/v1/verify</c> (type=recovery) troca o código por uma sessão de recuperação;
/// 3) <c>PUT /auth/v1/user</c> grava a nova senha. A rede fica no <see cref="ClienteSupabase"/>; aqui só validação.</summary>
public static class RecuperacaoSenha
{
    /// <summary>Intervalo mínimo entre pedidos de código (o Supabase também limita por e-mail).</summary>
    public const int EsperaReenvioSegundos = 60;

    /// <summary>Só os dígitos (tolera espaços/traços colados do e-mail, ex.: "123 456").</summary>
    public static string NormalizarCodigo(string? codigo) => new((codigo ?? "").Where(char.IsDigit).ToArray());

    /// <summary>O OTP do Supabase tem 6 dígitos por padrão (configurável até 10).</summary>
    public static bool CodigoValido(string? codigo) => NormalizarCodigo(codigo).Length is >= 6 and <= 10;

    /// <summary>Erros da etapa "código + nova senha" (mesma política de senha forte do resto do app).</summary>
    public static List<string> Validar(string? email, string? codigo, string? senha, string? confirmar)
    {
        var erros = new List<string>();
        if (!CodigoValido(codigo)) erros.Add("Digite o código de 6 dígitos que chegou no seu e-mail.");
        erros.AddRange(ValidacaoCredenciais.ValidarSenha(senha, email, ValidacaoCredenciais.NomeDeEmail(email)));
        if ((senha ?? "") != (confirmar ?? "")) erros.Add("As senhas não conferem.");
        return erros.Distinct().ToList();
    }

    /// <summary>Traduz a resposta do Supabase para uma mensagem útil ao usuário (sem expor detalhe técnico).</summary>
    public static string Mensagem(int status, string? erroServidor)
    {
        var e = (erroServidor ?? "").ToLowerInvariant();
        if (status == 429 || e.Contains("rate limit") || e.Contains("security purposes"))
            return "Muitos pedidos seguidos. Aguarde um minuto e tente de novo.";
        if (e.Contains("expired") || e.Contains("invalid") || e.Contains("otp") || status == 403)
            return "Código inválido ou expirado. Confira o e-mail mais recente ou peça um novo código.";
        if (e.Contains("different from the old") || e.Contains("same_password") || e.Contains("same password"))
            return "A nova senha precisa ser diferente da atual.";
        if (e.Contains("password") && (e.Contains("weak") || e.Contains("at least") || e.Contains("characters")))
            return "O servidor recusou a senha por ser fraca. Use 12+ caracteres com maiúsculas, minúsculas, número e símbolo.";
        return string.IsNullOrWhiteSpace(erroServidor) ? "Não foi possível concluir agora. Tente de novo em instantes." : erroServidor!;
    }
}
