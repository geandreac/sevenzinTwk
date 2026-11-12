using System.IO;
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

    readonly bool _bootstrap;

    public LoginWindow()
    {
        InitializeComponent();
        _bootstrap = Servicos.Contas.Todos.Count == 0;

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
            try { if (File.Exists(ArqEmail)) { Email.Text = File.ReadAllText(ArqEmail).Trim(); Lembrar.IsChecked = Email.Text.Length > 0; } } catch { }
        }

        Loaded += (_, _) => (Email.Text.Length > 0 ? (Control)Senha : Email).Focus();
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

        var r = Servicos.Auth.Autenticar(email, senha);
        if (r.Ok)
        {
            Servicos.Sessao.Abrir(r.Usuario!);
            LembrarEmail(email);
            DialogResult = true;
            return;
        }
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

    void LembrarEmail(string email)
    {
        try
        {
            if (Lembrar.IsChecked == true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArqEmail)!);
                File.WriteAllText(ArqEmail, email.Trim());
            }
            else if (File.Exists(ArqEmail)) File.Delete(ArqEmail);
        }
        catch { }
    }

    void Erro(string msg)
    {
        Mensagem.Text = msg;
        Mensagem.Visibility = Visibility.Visible;
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
}
