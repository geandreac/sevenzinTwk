using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sevenzin.Wpf;

/// <summary>Peças de layout repetidas entre telas.</summary>
static class Ui
{
    /// <summary>Uma superfície só, com linhas separadas por divisória — no lugar de um cartão por item.</summary>
    public static Border Grupo(IEnumerable<UIElement> linhas, double margemBaixo = 24)
    {
        var sp = new StackPanel();
        foreach (var l in linhas)
        {
            if (sp.Children.Count > 0)
                sp.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["BorderDefaultBrush"] });
            sp.Children.Add(new Border { Padding = new Thickness(0, 14, 0, 14), Child = l });
        }
        return new Border { Style = (Style)Application.Current.Resources["Card"], Padding = new Thickness(18, 2, 18, 2), Margin = new Thickness(0, 0, 0, margemBaixo), Child = sp };
    }
}
