using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Perfil" (tela 15 do original): conta local, 2FA (TOTP offline) e licença local —
/// substituindo o KeyAuth por algo transparente. Sem servidor, sem HWID, sem Discord.</summary>
public partial class PerfilView : UserControl
{
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];

    readonly LicencaStore _licenca = new();
    string? _segredoPendente;

    Usuario? Eu => Servicos.Sessao.Atual?.Usuario;

    public PerfilView()
    {
        InitializeComponent();
        Loaded += (_, _) => Montar();
    }

    void Montar()
    {
        Lista.Children.Clear();
        var eu = Eu;
        if (eu is null) { Lista.Children.Add(new TextBlock { Text = "Sem sessão ativa.", Foreground = Muted }); return; }

        // CONTA
        Lista.Children.Add(new TextBlock { Text = "CONTA", Style = SectionStyle });
        var conta = new StackPanel();
        conta.Children.Add(Campo("E-mail", eu.Email));
        conta.Children.Add(Campo("Papel", eu.Papel == PapelUsuario.Administrador ? "Administrador" : "Usuário"));
        conta.Children.Add(Campo("Criada em", eu.CriadoUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = conta });

        // 2FA
        Lista.Children.Add(new TextBlock { Text = "VERIFICAÇÃO EM DOIS FATORES (2FA)", Style = SectionStyle });
        var fa = new StackPanel();
        fa.Children.Add(new TextBlock
        {
            Text = eu.TotpAtivo ? "Ativo — o login pede o código do seu aplicativo autenticador." : "Inativo — o login usa só e-mail e senha.",
            Foreground = eu.TotpAtivo ? Success : Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap,
        });
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        if (eu.TotpAtivo)
        {
            var desativar = Botao(Secundario, "Desativar 2FA", () =>
            {
                if (MessageBox.Show("Desativar a verificação em dois fatores?", "2FA", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    Servicos.Contas.Desativar2FA(eu.Email);
                    Montar();
                }
            });
            acoes.Children.Add(desativar);
        }
        else
        {
            var ativar = Botao(Primario, "Ativar 2FA", () => IniciarAtivacao2FA(fa));
            acoes.Children.Add(ativar);
        }
        fa.Children.Add(acoes);
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = fa });

        // LICENÇA
        var lic = _licenca.Carregar();
        Lista.Children.Add(new TextBlock { Text = "LICENÇA LOCAL", Style = SectionStyle });
        var li = new StackPanel();
        li.Children.Add(new TextBlock
        {
            Text = lic.Ativa ? $"✔ Chave ativa: {lic.Chave} (desde {lic.AtivadaUtc?.ToLocalTime():dd/MM/yyyy})" : "Sem chave registrada.",
            Foreground = lic.Ativa ? Success : Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap,
        });
        li.Children.Add(new TextBlock
        {
            Text = "A chave é um registro LOCAL (formato A1B2-C3D4-E5F6-G7H8). Não há servidor, HWID ou bloqueio de recursos: tudo continua funcionando com ou sem chave.",
            Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var chaveTxt = new TextBox { Style = (Style)Application.Current.Resources["DarkBox"], Width = 240, Tag = "A1B2-C3D4-E5F6-G7H8", Text = lic.Chave ?? "" };
        linha.Children.Add(chaveTxt);
        var salvar = Botao(Primario, "Salvar chave", () =>
        {
            var c = ValidadorLicenca.Normalizar(chaveTxt.Text);
            if (!ValidadorLicenca.ChaveValida(c))
            {
                MessageBox.Show("Formato esperado: 4 grupos de 4 caracteres alfanuméricos (ex.: A1B2-C3D4-E5F6-G7H8).", "Chave inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _licenca.Salvar(new Licenca(c, DateTime.UtcNow));
            Montar();
        });
        linha.Children.Add(salvar);
        if (lic.Ativa)
        {
            var limpar = Botao(Secundario, "Remover", () => { _licenca.Salvar(new Licenca(null, null)); Montar(); });
            linha.Children.Add(limpar);
        }
        li.Children.Add(linha);
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = li });

        // RESUMO LOCAL
        Lista.Children.Add(new TextBlock { Text = "RESUMO LOCAL", Style = SectionStyle });
        var res = new StackPanel();
        var catalogo = new CatalogoJogos();
        int capturas = Directory.Exists(CapturaTela.PastaCapturas) ? Directory.GetFiles(CapturaTela.PastaCapturas, "*.png").Length : 0;
        res.Children.Add(Campo("Jogos com perfil", catalogo.Todos.Count.ToString()));
        res.Children.Add(Campo("Capturas salvas", capturas.ToString()));
        res.Children.Add(Campo("Notificações no histórico", NotificacoesStore.Padrao.Todos.Count.ToString()));
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = res });
    }

    void IniciarAtivacao2FA(StackPanel host)
    {
        var eu = Eu;
        if (eu is null) return;
        _segredoPendente = Servicos.Contas.GerarSegredo2FA(eu.Email);
        if (_segredoPendente is null) { MessageBox.Show("Conta não encontrada.", "2FA", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var bloco = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        bloco.Children.Add(new TextBlock
        {
            Text = "1. Cadastre este segredo no seu aplicativo autenticador (Google Authenticator, Authy, etc.):",
            Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap,
        });
        bloco.Children.Add(new TextBlock { Text = _segredoPendente, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        bloco.Children.Add(new TextBlock
        {
            Text = "2. Digite o código de 6 dígitos mostrado pelo aplicativo para confirmar:",
            Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var codigo = new TextBox { Style = (Style)Application.Current.Resources["DarkBox"], Width = 140, Tag = "000000", MaxLength = 6 };
        linha.Children.Add(codigo);
        var confirmar = Botao(Primario, "Confirmar código", () =>
        {
            if (Servicos.Contas.Confirmar2FA(eu.Email, codigo.Text))
            {
                MessageBox.Show("Verificação em dois fatores ativada. No próximo login o código será pedido.", "2FA", MessageBoxButton.OK, MessageBoxImage.Information);
                _segredoPendente = null;
                Montar();
            }
            else
            {
                MessageBox.Show("Código inválido ou expirado. Tente de novo com o código atual do aplicativo.", "2FA", MessageBoxButton.OK, MessageBoxImage.Warning);
                codigo.Clear();
                codigo.Focus();
            }
        });
        linha.Children.Add(confirmar);
        var cancelar = Botao(Secundario, "Cancelar", () =>
        {
            Servicos.Contas.Desativar2FA(eu.Email); // remove o segredo pendente (nunca ficou ativo)
            _segredoPendente = null;
            Montar();
        });
        linha.Children.Add(cancelar);
        bloco.Children.Add(linha);
        host.Children.Add(bloco);
    }

    static Button Botao(Style estilo, string texto, Action acao)
    {
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
        b.Click += (_, _) => acao();
        return b;
    }

    static UIElement Campo(string rotulo, string valor)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var r = new TextBlock { Text = rotulo, Foreground = Muted, FontSize = 13 };
        var v = new TextBlock { Text = valor, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        g.Children.Add(r); g.Children.Add(v);
        return g;
    }
}
