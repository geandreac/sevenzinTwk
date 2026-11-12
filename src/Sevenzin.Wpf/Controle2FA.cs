using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Painel reutilizável de gestão de 2FA (TOTP do Supabase) da conta logada: mostra o status,
/// ativa (exibindo o segredo para cadastrar no autenticador) e desativa. Autogerencia o próprio conteúdo.</summary>
public static class Controle2FA
{
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];

    public static StackPanel Criar()
    {
        var fa = new StackPanel();
        _ = Recarregar(fa);
        return fa;
    }

    static async Task Recarregar(StackPanel fa)
    {
        fa.Children.Clear();
        var status = new TextBlock { Text = "Carregando status do 2FA…", Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        fa.Children.Add(status);
        fa.Children.Add(acoes);

        var cli = Servicos.ClienteSupabase;
        if (cli is null) { status.Text = "Sessão não disponível (2FA só no modo Supabase)."; return; }

        var fator = ClienteSupabase.TotpVerificado(await cli.FatoresAsync());
        if (fator is not null)
        {
            status.Text = "Ativo — o login pede o código do seu aplicativo autenticador.";
            status.Foreground = Success;
            acoes.Children.Add(Botao(Secundario, "Desativar 2FA", async () =>
            {
                if (MessageBox.Show("Desativar a verificação em dois fatores?", "2FA", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                if (await cli.DesinscreverFatorAsync(fator.Id)) await Recarregar(fa);
                else MessageBox.Show("Não foi possível desativar agora.", "2FA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }
        else
        {
            status.Text = "Inativo — o login usa só e-mail e senha.";
            status.Foreground = Muted;
            acoes.Children.Add(Botao(Primario, "Ativar 2FA", async () => await Iniciar(fa, cli)));
        }
    }

    static async Task Iniciar(StackPanel fa, ClienteSupabase cli)
    {
        var insc = await cli.InscreverTotpAsync();
        if (insc is null) { MessageBox.Show("Não foi possível iniciar o 2FA agora.", "2FA", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var bloco = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        bloco.Children.Add(new TextBlock { Text = "1. Cadastre este segredo no seu aplicativo autenticador (Google Authenticator, Authy…):", Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        bloco.Children.Add(new TextBlock { Text = insc.Segredo, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        bloco.Children.Add(new TextBlock { Text = "2. Digite o código de 6 dígitos para confirmar:", Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var codigo = new TextBox { Style = (Style)Application.Current.Resources["DarkBox"], Width = 140, Tag = "000000", MaxLength = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        linha.Children.Add(codigo);
        linha.Children.Add(Botao(Primario, "Confirmar código", async () =>
        {
            var r = await cli.VerificarTotpAsync(insc.FatorId, codigo.Text);
            if (r.Ok) { MessageBox.Show("Verificação em dois fatores ativada. No próximo login o código será pedido.", "2FA", MessageBoxButton.OK, MessageBoxImage.Information); await Recarregar(fa); }
            else { MessageBox.Show(r.Mensagem ?? "Código inválido ou expirado.", "2FA", MessageBoxButton.OK, MessageBoxImage.Warning); codigo.Clear(); codigo.Focus(); }
        }));
        linha.Children.Add(Botao(Secundario, "Cancelar", async () => { await cli.DesinscreverFatorAsync(insc.FatorId); await Recarregar(fa); }));
        bloco.Children.Add(linha);
        fa.Children.Add(bloco);
        codigo.Focus();
    }

    static Button Botao(Style estilo, string texto, Action acao)
    {
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7), VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => acao();
        return b;
    }
}
