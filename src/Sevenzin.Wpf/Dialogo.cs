// O app inteiro usa o diálogo do tema no lugar da caixa branca do Windows, sem mudar as chamadas existentes.
global using MessageBox = Sevenzin.Wpf.Dialogo;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sevenzin.Wpf;

/// <summary>Confirmação no visual do app, com a mesma assinatura e os mesmos retornos do MessageBox.
/// Esc ou fechar = resposta que NÃO executa a ação. Em avisos, o foco inicial fica na opção segura.</summary>
static class Dialogo
{
    public static MessageBoxResult Show(string texto, string titulo, MessageBoxButton botoes, MessageBoxImage imagem) =>
        Show(Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
             ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(w => w.IsVisible), texto, titulo, botoes, imagem);

    public static MessageBoxResult Show(Window? dono, string texto, string titulo, MessageBoxButton botoes, MessageBoxImage imagem)
    {
        object R(string k) => Application.Current.Resources[k];
        var (sim, nao, rotuloSim, rotuloNao) = botoes switch
        {
            MessageBoxButton.YesNo => (MessageBoxResult.Yes, MessageBoxResult.No, "Sim", "Não"),
            MessageBoxButton.OKCancel => (MessageBoxResult.OK, MessageBoxResult.Cancel, "Continuar", "Cancelar"),
            _ => (MessageBoxResult.OK, MessageBoxResult.OK, "Entendi", ""),
        };
        bool temNao = rotuloNao.Length > 0, aviso = imagem is MessageBoxImage.Warning or MessageBoxImage.Error;
        var resultado = nao;

        var win = new Window
        {
            Title = titulo, Owner = dono, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = dono is null, SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = dono is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            FontFamily = (FontFamily)R("UiFont"), Topmost = dono is null,
        };

        var corpo = new StackPanel { Width = 420 };
        var cabeca = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        if (aviso)
        {
            var ponto = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = (Brush)R(imagem == MessageBoxImage.Error ? "DangerBrush" : "WarningBrush"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            cabeca.Children.Add(ponto);
        }
        cabeca.Children.Add(new TextBlock { Text = titulo, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        corpo.Children.Add(cabeca);
        corpo.Children.Add(new ScrollViewer
        {
            MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
            Content = new TextBlock { Text = texto, FontSize = 13, LineHeight = 19, Foreground = (Brush)R("TextSecondaryBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) },
        });

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        Button Botao(string rotulo, string estilo, MessageBoxResult r)
        {
            var b = new Button { Content = rotulo, Style = (Style)R(estilo), MinWidth = 96, Margin = new Thickness(8, 0, 0, 0) };
            b.Click += (_, _) => { resultado = r; win.Close(); };
            acoes.Children.Add(b);
            return b;
        }
        var bNao = temNao ? Botao(rotuloNao, "SecondaryButton", nao) : null;
        var bSim = Botao(rotuloSim, "PrimaryButton", sim);
        corpo.Children.Add(acoes);

        var painel = new Border
        {
            Background = (Brush)R("Surface1Brush"), BorderBrush = (Brush)R("BorderStrongBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(24, 22, 24, 20), Margin = new Thickness(24), Child = corpo,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.5 },
        };
        win.Content = painel;
        AutomationProperties.SetName(win, titulo);
        AutomationProperties.SetHelpText(win, texto);
        painel.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) win.DragMove(); };
        win.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; win.Close(); } };
        win.Loaded += (_, _) =>
        {
            Movimento.AbrirModal(painel, painel);
            (aviso && bNao is not null ? bNao : bSim).Focus();
        };
        win.ShowDialog();
        return resultado;
    }
}
