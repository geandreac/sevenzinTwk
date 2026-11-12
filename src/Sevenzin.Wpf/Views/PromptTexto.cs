using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sevenzin.Wpf.Views;

/// <summary>Prompt de texto rápido (tema escuro) para a anotação de texto do editor de capturas.</summary>
public static class PromptTexto
{
    public static string? Perguntar(Window? dono, string titulo)
    {
        var caixa = new TextBox
        {
            MinWidth = 260,
            Background = (Brush)Application.Current.Resources["Surface1Brush"],
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = (FontFamily)Application.Current.Resources["FontBody"],
        };

        var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 0) };
        sp.Children.Add(new TextBlock
        {
            Text = titulo,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            Margin = new Thickness(0, 0, 0, 10),
        });
        sp.Children.Add(caixa);

        var ok = new Button { Content = "OK", Style = (Style)Application.Current.Resources["PrimaryButton"], Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancelar = new Button { Content = "Cancelar", Style = (Style)Application.Current.Resources["SecondaryButton"], Width = 90 };
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        botoes.Children.Add(ok);
        botoes.Children.Add(cancelar);
        sp.Children.Add(botoes);

        var janela = new Window
        {
            Title = titulo,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = dono,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Application.Current.Resources["Surface0Brush"],
            Content = sp,
        };

        string? resultado = null;
        ok.Click += (_, _) => { resultado = caixa.Text.Trim(); janela.DialogResult = true; };
        cancelar.Click += (_, _) => janela.DialogResult = false;
        caixa.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { resultado = caixa.Text.Trim(); janela.DialogResult = true; } };
        janela.Loaded += (_, _) => { caixa.Focus(); caixa.SelectAll(); };
        return janela.ShowDialog() == true ? resultado : null;
    }
}
