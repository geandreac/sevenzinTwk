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
    const string SubtituloAdmin = "Área restrita para administradores";

    /// <summary>"Acesso Administrativo" → letras separadas por thin space e palavras por em space (WPF não tem letter-spacing).</summary>
    static string Espacado(string s) => string.Join(" ", s.Split(' ').Select(p => string.Join(" ", p.ToCharArray())));

    /// <summary>Layout do Admin: a dica do campo de senha ("Digite sua senha…") some quando há texto (o template
    /// lê Tag="cheio", porque PasswordBox não expõe a senha para gatilhos de XAML).</summary>
    void Senha_Dica(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox p) p.Tag = p.Password.Length > 0 ? "cheio" : null;
    }

    readonly bool _bootstrap;
    string? _email2fa, _senha2fa; // credenciais aguardando o código do 2FA
    ClienteSupabase? _clienteMfa;  // sessão Supabase aguardando o código MFA
    string? _fatorMfa, _emailMfa, _senhaMfa;

    public LoginWindow()
    {
        InitializeComponent();
        JanelaArredondada.SemMolduraNativa(this); // o cartão desenha a própria borda; a do Windows sobrava como moldura cinza
        // ADMINISTRADOR GLOBAL ÚNICO: o otimizador (User) nunca cria administrador nem usa conta local — contas só no
        // servidor. "Crie o primeiro administrador" existe só no app Admin, e só sem servidor configurado (modo local).
        _bootstrap = ModoAdmin && !Servicos.UsaSupabase && Servicos.Contas.Todos.Count == 0;

        if (ModoAdmin)
        {
            // Mesmo layout do User; no Admin muda só o vocabulário: escudo no lugar do ícone de conta e "Acesso Administrativo".
            AdminBadge.Visibility = Visibility.Visible;
            IconeConta.Visibility = Visibility.Collapsed;
            Tagline.Text = Espacado("Acesso Administrativo");
            System.Windows.Automation.AutomationProperties.SetName(Tagline, "Acesso Administrativo");
            Title = "SevenzinTwks Admin — Acesso";
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

        if (ModoAdmin && !_bootstrap) Subtitulo.Text = SubtituloAdmin;
        if (ModoAdmin || _bootstrap) SemAcessoTxt.Visibility = Visibility.Collapsed;

        _timerReenvio.Tick += (_, _) => AtualizarReenvio();
        // Entrada em dois tempos: formulário primeiro, painel da marca 80ms depois (leitura da esquerda p/ direita).
        Loaded += (_, _) => { Movimento.Entrar(Formulario, 12, 260); Movimento.Entrar(PainelMarca, 12, 260, 80); };
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

    async void Entrar_Click(object sender, RoutedEventArgs e)
    {
        var email = Email.Text;
        var senha = SenhaAtual();
        if (_bootstrap) { CriarPrimeiroAdmin(email, senha); return; }

        // Modo Supabase: autentica no backend; o papel (admin/usuário) vem da tabela perfis (RLS).
        if (Servicos.UsaSupabase)
        {
            await EntrarSupabase(email, senha);
            return;
        }
        if (!ModoAdmin)
        {
            Erro("Esta instalação não tem o servidor de contas configurado. Reinstale o SevenzinTwks pelo instalador oficial ou fale com o administrador.");
            return;
        }

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

    async Task EntrarSupabase(string email, string senha)
    {
        // 2ª etapa: código MFA (TOTP) de uma sessão já autenticada por senha.
        if (_clienteMfa is { } cmfa && _fatorMfa is { } fid)
        {
            EntrarBtn.IsEnabled = false;
            var rv = await cmfa.VerificarTotpAsync(fid, Totp.Text);
            EntrarBtn.IsEnabled = true;
            if (!rv.Ok)
            {
                _ = cmfa.RegistrarAcessoAsync(_emailMfa!, "Código 2FA inválido", false);
                Erro(rv.Mensagem ?? "Código inválido."); Totp.Clear(); Totp.Focus();
                return;
            }
            _ = cmfa.RegistrarAcessoAsync(_emailMfa!, "Sucesso (2FA)", true);
            CompletarSupabase(cmfa, _emailMfa!, _senhaMfa!);
            return;
        }

        EntrarBtn.IsEnabled = false;
        Mensagem.Visibility = Visibility.Collapsed;
        try
        {
            var cliente = new ClienteSupabase();
            var r = await cliente.LoginAsync(email, senha);
            if (!r.Ok)
            {
                _ = cliente.RegistrarAcessoAsync(email, "Falha de login", false);
                Erro(r.Mensagem ?? "E-mail ou senha inválidos.");
                return;
            }
            // Tem MFA verificado? Pede o código antes de concluir (eleva a sessão a aal2).
            if (ClienteSupabase.TotpVerificado(await cliente.FatoresAsync()) is { } fator)
            {
                _clienteMfa = cliente; _fatorMfa = fator.Id; _emailMfa = email; _senhaMfa = senha;
                TotpBloco.Visibility = Visibility.Visible;
                EntrarBtn.Content = "Verificar código";
                Subtitulo.Text = "Verificação em dois fatores";
                Totp.Focus();
                return;
            }
            _ = cliente.RegistrarAcessoAsync(email, "Sucesso", true);
            CompletarSupabase(cliente, email, senha);
        }
        finally { if (DialogResult != true) EntrarBtn.IsEnabled = true; }
    }

    // Sessão local sintética com o papel do Supabase — o resto da UI (checagem de admin) segue igual.
    void CompletarSupabase(ClienteSupabase cliente, string email, string senha)
    {
        var papel = cliente.EhAdmin ? PapelUsuario.Administrador : PapelUsuario.Usuario;
        var nome = string.IsNullOrWhiteSpace(cliente.NomePerfil) ? ValidacaoCredenciais.NomeDeEmail(cliente.Sessao!.Email) : cliente.NomePerfil!;
        Servicos.Sessao.Abrir(new Usuario(cliente.Sessao!.Email, nome, papel, "-", DateTime.UtcNow), autenticadoPorBackend: true);
        Servicos.ClienteSupabase = cliente;
        LembrarCredenciais(email, senha);
        DialogResult = true;
    }

    void CriarPrimeiroAdmin(string email, string senha)    {
        // ADMINISTRADOR ÚNICO: o bootstrap só cria o administrador se o e-mail for EXATAMENTE a
        // identidade provisionada. Sem identidade configurada, nega e instrui — nunca concede ADM.
        var identidade = IdentidadeAdmin.EmailConfigurado();
        if (identidade is null)
        {
            Erro("Administrador único não configurado.\n\nDefina as variáveis SEVENZIN_ADMIN_EMAIL e " +
                 "SEVENZIN_ADMIN_SENHA (e-mail do Geandre + senha forte) e abra o app de novo. " +
                 "Nenhum administrador é criado sem isso.");
            return;
        }
        if (!IdentidadeAdmin.EhAdminProtegido(email, identidade))
        {
            Erro("Somente o administrador provisionado pode criar a primeira conta de administrador.\n" +
                 "Use o e-mail configurado em SEVENZIN_ADMIN_EMAIL.");
            return;
        }

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

    // ---- Redefinir senha por código de e-mail (Supabase): e-mail → código + nova senha → pronto ----

    readonly System.Windows.Threading.DispatcherTimer _timerReenvio = new() { Interval = TimeSpan.FromSeconds(1) };
    DateTime _podeReenviarEm;
    string _emailRedef = "";

    void Esqueci_Click(object sender, RoutedEventArgs e)
    {
        Mensagem.Visibility = Visibility.Collapsed;
        LoginCampos.Visibility = Visibility.Collapsed;
        RedefinirBloco.Visibility = Visibility.Visible;
        Subtitulo.Text = "Redefinir senha";
        bool online = Servicos.UsaSupabase;
        RedefLocal.Visibility = online ? Visibility.Collapsed : Visibility.Visible;
        MostrarEtapa(online ? EtapaEmail : null);
        if (online)
        {
            RedefEmail.Text = Email.Text.Trim();
            (RedefEmail.Text.Length > 0 ? (Control)EnviarCodigoBtn : RedefEmail).Focus();
        }
    }

    void MostrarEtapa(StackPanel? etapa)
    {
        foreach (var p in new[] { EtapaEmail, EtapaCodigo, EtapaOk }) p.Visibility = p == etapa ? Visibility.Visible : Visibility.Collapsed;
        VoltarLinha.Visibility = etapa == EtapaOk ? Visibility.Collapsed : Visibility.Visible;
        Mensagem.Visibility = Visibility.Collapsed;
        Movimento.Entrar(etapa ?? (UIElement)RedefLocal); // troca de etapa: só o corpo entra, o cabeçalho fica
    }

    void RedefCampo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (EtapaEmail.Visibility == Visibility.Visible) EnviarCodigo_Click(sender, e);
        else if (EtapaCodigo.Visibility == Visibility.Visible) SalvarSenha_Click(sender, e);
    }

    async void EnviarCodigo_Click(object sender, RoutedEventArgs e)
    {
        var email = RedefEmail.Text.Trim();
        var erros = ValidacaoCredenciais.ValidarEmail(email);
        if (erros.Count > 0) { Erro(erros[0]); RedefEmail.Focus(); return; }
        if (await PedirCodigo(email, EnviarCodigoBtn, "Enviar código"))
        {
            _emailRedef = email;
            RedefInfo.Text = $"Se existir uma conta com {email}, enviamos um código. Confira a caixa de entrada e o spam.";
            RedefCodigo.Clear(); RedefSenha.Clear(); RedefConfirmar.Clear();
            MostrarEtapa(EtapaCodigo);
            RedefCodigo.Focus();
        }
    }

    /// <summary>Pede o código ao Supabase e inicia a espera para reenviar. Mostra o erro no rodapé se falhar.</summary>
    async Task<bool> PedirCodigo(string email, Button? botao, string rotulo)
    {
        if (botao is not null) { botao.IsEnabled = false; botao.Content = "Enviando…"; }
        var r = await new ClienteSupabase().ResetSenhaAsync(email);
        if (botao is not null) { botao.IsEnabled = true; botao.Content = rotulo; }
        if (!r.Ok) { Erro(r.Mensagem ?? "Não foi possível enviar o código."); return false; }
        _podeReenviarEm = DateTime.UtcNow.AddSeconds(RecuperacaoSenha.EsperaReenvioSegundos);
        AtualizarReenvio();
        _timerReenvio.Start();
        return true;
    }

    void AtualizarReenvio()
    {
        var falta = (int)Math.Ceiling((_podeReenviarEm - DateTime.UtcNow).TotalSeconds);
        ReenviarLink.IsEnabled = falta <= 0;
        ReenviarLink.Foreground = (System.Windows.Media.Brush)FindResource(falta > 0 ? "TextMutedBrush" : "Brand400Brush"); // desabilitado não parece clicável
        ReenviarTxt.Text = falta > 0 ? $"Reenviar código em {falta}s" : "Reenviar código";
        if (falta <= 0) _timerReenvio.Stop();
    }

    async void Reenviar_Click(object sender, RoutedEventArgs e)
    {
        if (await PedirCodigo(_emailRedef, null, ""))
        {
            Mensagem.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            Mensagem.Text = "Novo código enviado. Use o código do e-mail mais recente.";
            Mensagem.Visibility = Visibility.Visible;
        }
    }

    void OutroEmail_Click(object sender, RoutedEventArgs e)
    {
        MostrarEtapa(EtapaEmail);
        RedefEmail.Focus();
        RedefEmail.SelectAll();
    }

    async void SalvarSenha_Click(object sender, RoutedEventArgs e)
    {
        var erros = RecuperacaoSenha.Validar(_emailRedef, RedefCodigo.Text, RedefSenha.Password, RedefConfirmar.Password);
        if (erros.Count > 0) { Erro("• " + string.Join("\n• ", erros)); return; }

        SalvarSenhaBtn.IsEnabled = false; SalvarSenhaBtn.Content = "Salvando…";
        var r = await new ClienteSupabase().RedefinirSenhaComCodigoAsync(_emailRedef, RedefCodigo.Text, RedefSenha.Password);
        SalvarSenhaBtn.IsEnabled = true; SalvarSenhaBtn.Content = "Salvar nova senha";
        if (!r.Ok) { Erro(r.Mensagem ?? "Não foi possível salvar a nova senha."); return; }

        // A senha "lembrada" (DPAPI) ficou velha: apaga para não preencher a antiga no próximo login.
        try { if (File.Exists(ArqSenha)) File.Delete(ArqSenha); } catch { }
        RedefSenha.Clear(); RedefConfirmar.Clear(); RedefCodigo.Clear();
        _timerReenvio.Stop();
        Subtitulo.Text = "Tudo certo";
        MostrarEtapa(EtapaOk);
    }

    void VoltarLogin_Click(object sender, RoutedEventArgs e)
    {
        _timerReenvio.Stop();
        RedefinirBloco.Visibility = Visibility.Collapsed;
        LoginCampos.Visibility = Visibility.Visible;
        Movimento.Entrar(LoginCampos);
        Subtitulo.Text = ModoAdmin ? SubtituloAdmin : "Acesse sua conta";
        Mensagem.Visibility = Visibility.Collapsed;
        if (_emailRedef.Length > 0) Email.Text = _emailRedef;
        if (EtapaOk.Visibility == Visibility.Visible) { Senha.Clear(); SenhaVisivel.Clear(); }
        (Email.Text.Length > 0 ? (Control)Senha : Email).Focus();
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
        Mensagem.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"); // pode ter ficado verde (aviso de reenvio)
        Mensagem.Text = msg;
        Mensagem.Visibility = Visibility.Visible;
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
}
