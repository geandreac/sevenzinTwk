using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Ajustes" (Figma) — tweaks reversíveis por seção, backed pelo MotorTweaks real.
/// Toggle liga = Aplicar (prévia no efeito descrito), desliga = Desfazer. Todo item tem prévia,
/// backup, desfazer e auditoria.</summary>
public partial class AjustesView : UserControl
{
    readonly MotorTweaks _motor = new(new RegistroWindows());
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Style Switch => (Style)Application.Current.Resources["ToggleSwitch"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];

    public AjustesView()
    {
        InitializeComponent();
        foreach (var grupo in CatalogoTweaks.Todos.GroupBy(t => t.Categoria))
        {
            Lista.Children.Add(new TextBlock { Text = grupo.Key.ToUpperInvariant(), Style = SectionStyle });
            Lista.Children.Add(Cartao(grupo.Select(LinhaTweak)));
        }
        AtualizarContador();
    }

    static Border Cartao(IEnumerable<UIElement> linhas)
    {
        var sp = new StackPanel();
        bool primeiro = true;
        foreach (var l in linhas)
        {
            if (!primeiro) sp.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["BorderDefaultBrush"], Margin = new Thickness(0, 4, 0, 4) });
            sp.Children.Add(l);
            primeiro = false;
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    UIElement LinhaTweak(Tweak t)
    {
        var desc = t.ExigeAdmin ? t.Descricao + "  ·  requer administrador" : t.Descricao;
        var g = LinhaBase(t.Nome, desc, out _);
        var sw = new ToggleButton { Style = Switch, VerticalAlignment = VerticalAlignment.Center, Tag = t, IsChecked = _motor.Aplicado(t) };
        sw.Click += Toggle_Click;
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return g;
    }

    static Grid LinhaBase(string titulo, string descricao, out StackPanel left)
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(new TextBlock { Text = titulo, FontSize = 14 });
        left.Children.Add(new TextBlock { Text = descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        return g;
    }

    void Toggle_Click(object sender, RoutedEventArgs e)
    {
        var sw = (ToggleButton)sender;
        var t = (Tweak)sw.Tag;

        // Elevação sob demanda: ajustes de máquina (HKLM) exigem administrador.
        if (t.ExigeAdmin && !Elevacao.EhAdmin())
        {
            sw.IsChecked = _motor.Aplicado(t); // mantém o estado real
            if (MessageBox.Show("Esta otimização mexe no sistema (HKLM) e exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        try
        {
            if (sw.IsChecked == true) _motor.Aplicar(t);
            else if (!_motor.Desfazer(t)) sw.IsChecked = true; // sem backup: nada a desfazer
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = _motor.Aplicado(t);
            MessageBox.Show("Esta otimização exige executar o Sevenzin como administrador.", "Permissão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            sw.IsChecked = _motor.Aplicado(t);
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        AtualizarContador();
    }

    void AtualizarContador()
    {
        int ativos = CatalogoTweaks.Todos.Count(_motor.Aplicado);
        Contador.Text = $"{ativos} de {CatalogoTweaks.Todos.Count} ativos · reversível e auditado · máquina (HKLM) pede admin · meça A/B antes de confiar no ganho";
    }
}
