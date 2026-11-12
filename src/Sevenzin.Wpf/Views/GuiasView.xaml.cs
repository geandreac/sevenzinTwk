using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Guias" (tela 11 do original — 03_telas §11). Conteúdo reconstruído do que o Sevenzin
/// realmente faz; os textos exatos do original são lacuna (#US cifrado), registrada no plano.</summary>
public partial class GuiasView : UserControl
{
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];

    public GuiasView()
    {
        InitializeComponent();
        foreach (var g in CatalogoGuias.Todos) Lista.Children.Add(Cartao(g));
    }

    Border Cartao(Guia g)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = g.Titulo, FontSize = 16, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock { Text = g.Resumo, Foreground = Secondary, FontSize = 13, Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap });
        for (int i = 0; i < g.Passos.Length; i++)
        {
            sp.Children.Add(new TextBlock
            {
                Text = $"{i + 1}.  {g.Passos[i]}",
                Foreground = Muted, FontSize = 13, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap,
            });
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }
}
