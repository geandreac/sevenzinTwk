using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Sevenzin.Wpf.Views;

namespace Sevenzin.Wpf;

public partial class MainWindow : Window
{
    // (tag, glifo Segoe MDL2, rótulo). Ordem do Figma.
    static readonly (string Tag, string Glifo, string Rotulo)[] Itens =
    {
        ("inicio", "", "Início"),
        ("ajustes", "", "Ajustes"),
        ("gpu", "", "GPU"),
        ("instaladores", "", "Instaladores"),
        ("jogos", "", "Jogos"),
        ("bios", "", "BIOS"),
        ("capturas", "", "Capturas"),
        ("windows", "", "Windows"),
        ("protecao", "", "Proteção"),
        ("config", "", "Configurações"),
        ("inventario", "", "Inventário"),
        ("telemetria", "", "Telemetria"),
        ("benchmark", "", "Benchmark"),
        ("admin", "", "Admin"),
    };

    readonly List<TextBlock> _rotulos = new(); // rótulos ocultos na sidebar estreita; aparecem ao expandir
    bool _expandido;

    public MainWindow()
    {
        InitializeComponent();
        bool podeAdmin = Servicos.Sessao.PodeAcessarAdmin;
        foreach (var (tag, glifo, rotulo) in Itens)
        {
            if (tag == "admin" && !podeAdmin) continue; // proteção de rota por papel: só admin vê a tela Admin
            var item = new ListBoxItem { Content = ConteudoNav(tag, glifo, rotulo), Tag = tag, ToolTip = rotulo };
            System.Windows.Automation.AutomationProperties.SetName(item, rotulo); // acessibilidade (nome do item)
            Nav.Items.Add(item);
        }
        Nav.SelectedIndex = 0;
        UsuarioLabel.Text = Servicos.Sessao.Atual?.Usuario.Email ?? "";
    }

    // Logout / expiração de sessão: encerra e volta ao login; se o login for cancelado, o app fecha.
    void Sair_Click(object sender, RoutedEventArgs e) => ForcarRelogin();

    void ForcarRelogin()
    {
        Servicos.Sessao.Encerrar();
        var login = new LoginWindow();
        bool ok = login.ShowDialog() == true && Servicos.Sessao.Valida;
        if (ok) { new MainWindow().Show(); Close(); }
        else { Close(); Application.Current.Shutdown(); } // login cancelado → encerra o app
    }

    // Hambúrguer (Figma): alterna entre estreita (66, só ícone) e expandida (210, com rótulos),
    // com transição discreta na largura. Respeita "reduzir movimento" do Windows.
    void Menu_Click(object sender, RoutedEventArgs e)
    {
        _expandido = !_expandido;
        double alvo = _expandido ? 210 : 66;
        foreach (var l in _rotulos) l.Visibility = _expandido ? Visibility.Visible : Visibility.Collapsed;
        if (SystemParameters.ClientAreaAnimation)
            Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(Sidebar.ActualWidth, alvo, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { Sidebar.BeginAnimation(WidthProperty, null); Sidebar.Width = alvo; }
    }

    // Ícones da sidebar vindos do Figma (ic_*). Itens extras (inventário/telemetria/benchmark/admin) usam glifo Fluent.
    static readonly Dictionary<string, string> IconeFigma = new()
    {
        ["inicio"] = "ic_inicio", ["ajustes"] = "ic_ajustes", ["gpu"] = "ic_gpu", ["instaladores"] = "ic_instaladores",
        ["jogos"] = "ic_jogos", ["bios"] = "ic_bios", ["capturas"] = "ic_capturas", ["windows"] = "ic_windows", ["config"] = "ic_config",
        ["inventario"] = "ic_inventario", ["telemetria"] = "ic_telemetria", ["benchmark"] = "ic_benchmark", ["admin"] = "ic_admin",
        ["protecao"] = "ic_protecao",
    };

    StackPanel ConteudoNav(string tag, string glifo, string rotulo)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var host = new Grid { Width = 50 }; // centraliza o ícone na coluna de 66px
        host.Children.Add(IconeNav(tag, glifo));
        sp.Children.Add(host);
        var lbl = new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0), Visibility = Visibility.Collapsed };
        _rotulos.Add(lbl);
        sp.Children.Add(lbl);
        return sp;
    }

    static UIElement IconeNav(string tag, string glifo)
    {
        if (IconeFigma.TryGetValue(tag, out var key) && Application.Current.Resources[key] is Geometry g)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = g,
                StrokeThickness = 1.6,
                Fill = null,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };
            p.SetBinding(Shape.StrokeProperty, new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) },
            });
            return new Viewbox { Width = 18, Height = 18, Child = p, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        return new TextBlock { Text = glifo, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Seleciona uma aba pelo tag (usado pelo render headless).</summary>
    public void SelecionarAba(string tag)
    {
        foreach (ListBoxItem it in Nav.Items)
            if ((string)it.Tag == tag) { Nav.SelectedItem = it; return; }
    }

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Sessão deslizante: renova a cada navegação; se expirou, força novo login.
        if (Servicos.Sessao.Atual is not null && Servicos.Sessao.Renovar() is null) { ForcarRelogin(); return; }

        var tag = (Nav.SelectedItem as ListBoxItem)?.Tag as string;
        if (tag is not null) TituloBarra.Text = Itens.First(i => i.Tag == tag).Rotulo;
        Host.Content = tag switch
        {
            "inicio" => new InicioView(),
            "ajustes" => new AjustesView(),
            "gpu" => new GpuView(),
            "instaladores" => new InstaladoresView(),
            "jogos" => new JogosView(),
            "bios" => new BiosView(),
            "capturas" => new CapturasView(),
            "windows" => new WindowsView(),
            "protecao" => new ProtecaoView(),
            "config" => new ConfiguracoesView(),
            "inventario" => new InventarioView(),
            "telemetria" => new TelemetriaView(),
            "benchmark" => new BenchmarkView(),
            "admin" => new AdminView(),
            _ => EmBreve(Itens.First(i => i.Tag == tag).Rotulo),
        };
        AnimarEntrada(Host);
    }

    // Transição suave ao trocar de tela: fade + leve slide para cima. Respeita "reduzir movimento".
    static void AnimarEntrada(UIElement el)
    {
        if (!SystemParameters.ClientAreaAnimation) { el.Opacity = 1; el.RenderTransform = null; return; }
        el.Opacity = 0;
        var tt = new TranslateTransform(0, 10);
        el.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        el.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
    }

    static UIElement EmBreve(string rotulo)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = rotulo, FontSize = 20, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Em migração para WPF — próxima fatia.", Margin = new Thickness(0, 8, 0, 0), Foreground = (Brush)Application.Current.Resources["TextMutedBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        return sp;
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Max_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
    }
    void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
