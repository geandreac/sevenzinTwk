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
        // Rede por adaptador (dinâmica): 5 tweaks por interface, valores do original, reversíveis.
        Lista.Children.Add(new TextBlock { Text = "REDE (POR ADAPTADOR)", Style = SectionStyle, Margin = new Thickness(0, 6, 0, 10) });
        Lista.Children.Add(CartaoRede());
        AtualizarContador();
    }

    Border CartaoRede()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Ajustes de rede por adaptador (valores do original: ACK imediato, NoDelay, DelAck, NetBIOS off, DNS multicast off). Exige administrador; cada aplicação pede confirmação e tudo é desfazível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        var lista = new StackPanel();
        sp.Children.Add(lista);
        var atualizar = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Atualizar adaptadores", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7) };
        atualizar.Click += (_, _) => MontarRede(lista);
        sp.Children.Add(atualizar);
        MontarRede(lista);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    void MontarRede(StackPanel host)
    {
        host.Children.Clear();
        var interfaces = RedePorInterface.Listar();
        if (interfaces.Count == 0)
        {
            host.Children.Add(new TextBlock { Text = "Nenhum adaptador IP ativo.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });
            return;
        }
        foreach (var i in interfaces)
        {
            var tweaks = RedePorInterface.TweaksPara(i.Guid);
            int aplicados = tweaks.Count(_motor.Aplicado);

            var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = i.Descricao, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            info.Children.Add(new TextBlock { Text = $"{aplicados} de {tweaks.Count} ajustes aplicados · reversível e auditado", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });

            var botoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var aplicar = new Button { Style = (Style)Application.Current.Resources["PrimaryButton"], Content = "Aplicar os 5", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
            aplicar.Click += (_, _) =>
            {
                if (MessageBox.Show($"Aplicar os 5 ajustes de rede no adaptador:\n\n{i.Descricao}\n\nRisco médio — meça A/B. O estado anterior fica salvo para desfazer.",
                        "Confirmação — rede por adaptador", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
                try
                {
                    foreach (var t in tweaks) _motor.Aplicar(t);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
                MontarRede(host);
            };
            var desfazer = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Desfazer", Padding = new Thickness(12, 7, 12, 7) };
            desfazer.Click += (_, _) =>
            {
                foreach (var t in tweaks) _motor.Desfazer(t);
                MontarRede(host);
            };
            botoes.Children.Add(aplicar);
            botoes.Children.Add(desfazer);

            var linha = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(info, 0);
            Grid.SetColumn(botoes, 1);
            linha.Children.Add(info);
            linha.Children.Add(botoes);
            host.Children.Add(linha);
        }
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
        if (t.ExigeConfirmacao) desc += $"  ·  risco: {t.Risco}";
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
        // Itens de risco ALTO/CRÍTICO pedem confirmação explícita antes de aplicar (regra de segurança nº 2).
        if (t.ExigeConfirmacao && sw.IsChecked == true)
        {
            sw.IsChecked = _motor.Aplicado(t);
            if (MessageBox.Show($"{t.Nome}\n\nRisco: {t.Risco}.\n\nVocê pode desfazer depois (o valor anterior fica salvo).\nAplicar mesmo assim?",
                    "Confirmação — ajuste de segurança", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            sw.IsChecked = true;
        }
        try
        {
            if (sw.IsChecked == true)
            {
                _motor.Aplicar(t);
                NotificacoesStore.Padrao.Adicionar("Ajustes", $"'{t.Nome}' aplicado", $"Novo valor: {(t.Tipo == TipoValor.String ? t.ValorTexto : t.ValorOtimizado.ToString())}. Desfazer restaura o anterior.");
            }
            else if (!_motor.Desfazer(t)) sw.IsChecked = true;
            else NotificacoesStore.Padrao.Adicionar("Ajustes", $"'{t.Nome}' desfeito", "Valor anterior restaurado.");
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
