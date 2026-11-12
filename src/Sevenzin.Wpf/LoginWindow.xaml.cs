using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Gate de acesso: login por e-mail+senha (admin-only, sem criar conta/social/licença).
/// Se não houver nenhuma conta e nenhum seed, permite criar o primeiro administrador (bootstrap).</summary>
public partial class LoginWindow : Window
{
    static readonly string ArqEmail = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "ultimo_email.txt");
    // Senha "lembrada" é guardada CIFRADA por DPAPI (escopo do usuário do Windows): só este usuário, nesta máquina,
    // consegue decifrar. Nunca em texto puro. Serve apenas para preencher o login do próprio app.
    static readonly string ArqSenha = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "senha_lembrada.bin");

    /// <summary>Ligado pelo app Admin antes de abrir o login, para diferenciar a tela (selo "Painel administrativo").</summary>
    public static bool ModoAdmin;

    readonly bool _bootstrap;
    string? _email2fa, _senha2fa; // credenciais aguardando o código do 2FA

    public LoginWindow()
    {
        InitializeComponent();
        JanelaArredondada.Arredondar(this);
        _bootstrap = Servicos.Contas.Todos.Count == 0;

        if (ModoAdmin)
        {
            AdminBadge.Visibility = Visibility.Visible;
            Title = "Sevenzin Admin — Acesso";
        }

        if (_bootstrap)
        {
            Subtitulo.Text = "Crie o primeiro administrador";
            ConfirmarBloco.Visibility = Visibility.Visible;
            SenhaAjuda.Visibility = Visibility.Visible;
            LinhaOpcoes.Visibility = Visibility.Collapsed;
            EntrarBtn.Content = "Criar administrador";
        }
        else
        {
            try
            {
                if (File.Exists(ArqEmail)) { Email.Text = File.ReadAllText(ArqEmail).Trim(); Lembrar.IsChecked = Email.Text.Length > 0; }
                if (Lembrar.IsChecked == true && CarregarSenhaLembrada() is { } s) Senha.Password = s;
            }
            catch { }
        }

        if (ModoAdmin && !_bootstrap) Subtitulo.Text = "Acesso de administrador";

        // Com e-mail e senha já preenchidos (lembrados), o foco vai direto no botão Entrar.
        Loaded += (_, _) => (Senha.Password.Length > 0 ? (Control)EntrarBtn : Email.Text.Length > 0 ? Senha : Email).Focus();
    }

    string SenhaAtual() => SenhaVisivel.Visibility == Visibility.Visible ? SenhaVisivel.Text : Senha.Password;

    void Olho_Click(object sender, RoutedEventArgs e)
    {
        if (OlhoBtn.IsChecked == true) { SenhaVisivel.Text = Senha.Password; SenhaVisivel.Visibility = Visibility.Visible; Senha.Visibility = Visibility.Collapsed; SenhaVisivel.Focus(); SenhaVisivel.CaretIndex = SenhaVisivel.Text.Length; }
        else { Senha.Password = SenhaVisivel.Text; Senha.Visibility = Visibility.Visible; SenhaVisivel.Visibility = Visibility.Collapsed; Senha.Focus(); }
    }

    void Campo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Entrar_Click(sender, e);
    }

    void Entrar_Click(object sender, RoutedEventArgs e)
    {
        var email = Email.Text;
        var senha = SenhaAtual();
        if (_bootstrap) { CriarPrimeiroAdmin(email, senha); return; }

        // 2ª etapa do 2FA: senha já validada, falta o código do autenticador.
        if (_email2fa is not null)
        {
            var r2 = Servicos.Auth.Autenticar(_email2fa, _senha2fa, Totp.Text);
            if (r2.Ok)
            {
                RegistroAcessos.Registrar(_email2fa, "Sucesso (2FA)", true);
                Servicos.Sessao.Abrir(r2.Usuario!);
                LembrarCredenciais(_email2fa, _senha2fa!);
                DialogResult = true;
                return;
            }
            RegistroAcessos.Registrar(_email2fa, "Código 2FA inválido", false);
            Erro(r2.Mensagem);
            Totp.Clear();
            Totp.Focus();
            return;
        }

        var r = Servicos.Auth.Autenticar(email, senha);
        if (r.Ok)
        {
            RegistroAcessos.Registrar(email, "Sucesso", true);
            Servicos.Sessao.Abrir(r.Usuario!);
            LembrarCredenciais(email, senha);
            DialogResult = true;
            return;
        }
        if (r.Status == StatusAuth.Exige2FA)
        {
            _email2fa = email;
            _senha2fa = senha;
            TotpBloco.Visibility = Visibility.Visible;
            EntrarBtn.Content = "Verificar código";
            Subtitulo.Text = "Verificação em dois fatores";
            Mensagem.Visibility = Visibility.Collapsed;
            Totp.Focus();
            return;
        }
        RegistroAcessos.Registrar(email, r.Status == StatusAuth.Bloqueado ? "Bloqueado (força bruta)" : "Falha de login", false);
        Erro(r.Status == StatusAuth.Bloqueado && r.Espere is { } t
            ? $"Muitas tentativas. Aguarde ~{Math.Ceiling(t.TotalMinutes)} min e tente de novo."
            : r.Mensagem);
    }

    void CriarPrimeiroAdmin(string email, string senha)
    {
        var nome = ValidacaoCredenciais.NomeDeEmail(email);
        var erros = ValidacaoCredenciais.ValidarEmail(email).Concat(ValidacaoCredenciais.ValidarSenha(senha, email, nome)).ToList();
        if (senha != Confirmar.Password) erros.Add("As senhas não conferem.");
        if (erros.Count > 0) { Erro("• " + string.Join("\n• ", erros.Distinct())); return; }
        try
        {
            var u = Servicos.Contas.Criar(email, senha, PapelUsuario.Administrador, nome);
            Servicos.Sessao.Abrir(u);
            DialogResult = true;
        }
        catch (Exception ex) { Erro(ex.Message); }
    }

    void Esqueci_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "Este é um app local, sem servidor: não há recuperação por e-mail.\n\n" +
            "Para redefinir a senha, peça a outro administrador (tela Admin) ou refaça o seed " +
            "pelas variáveis de ambiente SEVENZIN_ADMIN_EMAIL / SEVENZIN_ADMIN_SENHA.",
            "Esqueci minha senha", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Com "Lembrar de mim" marcado, guarda o e-mail e a senha (cifrada por DPAPI) para preencher o próximo
    /// login. Desmarcado, apaga os dois. A senha nunca é gravada em texto puro.</summary>
    void LembrarCredenciais(string email, string senha)
    {
        try
        {
            if (Lembrar.IsChecked == true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArqEmail)!);
                File.WriteAllText(ArqEmail, email.Trim());
                var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(senha), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(ArqSenha, cifrado);
            }
            else
            {
                if (File.Exists(ArqEmail)) File.Delete(ArqEmail);
                if (File.Exists(ArqSenha)) File.Delete(ArqSenha);
            }
        }
        catch { }
    }

    /// <summary>Decifra a senha lembrada (DPAPI, escopo do usuário). null se não há, está corrompida ou é de outro usuário/máquina.</summary>
    static string? CarregarSenhaLembrada()
    {
        try
        {
            if (!File.Exists(ArqSenha)) return null;
            var claro = ProtectedData.Unprotect(File.ReadAllBytes(ArqSenha), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(claro);
        }
        catch { return null; }
    }

    void Erro(string msg)
    {
        Mensagem.Text = msg;
        Mensagem.Visibility = Visibility.Visible;
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
}
