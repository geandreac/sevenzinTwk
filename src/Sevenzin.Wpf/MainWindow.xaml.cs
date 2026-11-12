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
    // (tag, glifo Segoe MDL2, rótulo). Ordem por prioridade de uso (blocos lógicos — ver docs/revisao_ajustes.md §6):
    // Essenciais · Hardware & desempenho · Sistema (ações de risco mais abaixo) · Ferramentas · Técnico/Conta (no fim).
    static readonly (string Tag, string Glifo, string Rotulo)[] Itens =
    {
        // Essenciais (uso diário: ver estado → otimizar → por jogo)
        ("inicio", "", "Início"),
        ("ajustes", "", "Ajustes"),
        ("jogos", "", "Jogos"),
        // Hardware & desempenho (diagnosticar e medir)
        ("gpu", "", "GPU"),
        ("inventario", "", "Inventário"),
        ("benchmark", "", "Benchmark"),
        // Sistema (Windows e apps; ações de risco — Proteção/BIOS/Mídia — ao fim do bloco)
        ("windows", "", "Windows"),
        ("instaladores", "", "Instaladores"),
        ("protecao", "", "Proteção"),
        ("bios", "", "BIOS"),
        ("midia", "", "Mídia"),
        // Ferramentas (uso pontual)
        ("capturas", "", "Capturas"),
        ("assistente", "", "Assistente"),
        ("guias", "", "Guias"),
        ("notificacoes", "", "Notificações"),
        // Técnico / Conta (configura-uma-vez — no fim)
        ("telemetria", "", "Telemetria"),
        ("perfil", "", "Perfil"),
        ("config", "", "Configurações"),
    };

    readonly List<TextBlock> _rotulos = new(); // rótulos ocultos na sidebar estreita; aparecem ao expandir
    bool _expandido;

    public MainWindow()
    {
        InitializeComponent();
        JanelaArredondada.Arredondar(this);
        foreach (var (tag, glifo, rotulo) in Itens)
        {
            var item = new ListBoxItem { Content = ConteudoNav(tag, glifo, rotulo), Tag = tag, ToolTip = rotulo };
            // Nome ao lado do ícone (sidebar recolhida): tooltip à direita, aparecendo rápido. Desligado quando expandida.
            ToolTipService.SetPlacement(item, System.Windows.Controls.Primitives.PlacementMode.Right);
            ToolTipService.SetHorizontalOffset(item, 10);
            ToolTipService.SetInitialShowDelay(item, 250);
            ToolTipService.SetIsEnabled(item, !_expandido);
            System.Windows.Automation.AutomationProperties.SetName(item, rotulo); // acessibilidade (nome do item)
            Nav.Items.Add(item);
        }
        Nav.SelectedIndex = 0;
        UsuarioLabel.Text = Servicos.Sessao.Atual?.Usuario.Email ?? "";
        if (!App.ModoRender) _ = VerificarAtualizacaoAsync(); // aviso silencioso na partida; nada é baixado sozinho
    }

    string? _urlAtualizacao;

    // Checa a release mais recente do GitHub em segundo plano. Se houver versão maior, mostra o banner
    // (dispensável). Falha/offline não aparece — o usuário pode checar manualmente em Configurações.
    async Task VerificarAtualizacaoAsync()
    {
        var versao = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        var r = await Sevenzin.Diagnostics.VerificadorAtualizacoes.VerificarAsync(versao);
        if (r is null || !r.HaNova || r.Url.Length == 0) return;
        _urlAtualizacao = r.Url;
        AvisoAtualizaTxt.Text = $"Sevenzin {r.VersaoRemota} disponível (você tem {r.VersaoAtual}).";
        AvisoAtualiza.Visibility = Visibility.Visible;
    }

    void BaixarAviso_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_urlAtualizacao)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_urlAtualizacao) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    void DispensarAviso_Click(object sender, RoutedEventArgs e) => AvisoAtualiza.Visibility = Visibility.Collapsed;

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
        foreach (ListBoxItem it in Nav.Items) ToolTipService.SetIsEnabled(it, !_expandido); // expandida já mostra o rótulo; evita tooltip redundante
        if (SystemParameters.ClientAreaAnimation)
            Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(Sidebar.ActualWidth, alvo, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { Sidebar.BeginAnimation(WidthProperty, null); Sidebar.Width = alvo; }
    }

    // Ícones da sidebar vindos do Figma (ic_*). Itens extras (inventário/telemetria/benchmark/admin) usam glifo Fluent.
    static readonly Dictionary<string, string> IconeFigma = new()
    {
        ["inicio"] = "ic_inicio", ["ajustes"] = "ic_ajustes", ["gpu"] = "ic_gpu", ["instaladores"] = "ic_instaladores",
        ["jogos"] = "ic_jogos", ["bios"] = "ic_bios", ["capturas"] = "ic_capturas", ["windows"] = "ic_windows", ["config"] = "ic_config",
        ["midia"] = "ic_midia",
        ["assistente"] = "ic_assistente",
        ["inventario"] = "ic_inventario", ["telemetria"] = "ic_telemetria", ["benchmark"] = "ic_benchmark", ["admin"] = "ic_admin",
        ["protecao"] = "ic_protecao", ["guias"] = "ic_guias", ["notificacoes"] = "ic_notificacoes", ["perfil"] = "ic_perfil",
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
            "guias" => new GuiasView(),
            "notificacoes" => new NotificacoesView(),
            "bios" => new BiosView(),
            "assistente" => new AssistenteView(),
            "capturas" => new CapturasView(),
            "windows" => new WindowsView(),
            "midia" => new MediaView(),
            "protecao" => new ProtecaoView(),
            "config" => new ConfiguracoesView(),
            "perfil" => new PerfilView(),
            "inventario" => new InventarioView(),
            "telemetria" => new TelemetriaView(),
            "benchmark" => new BenchmarkView(),
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
        MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
    }
    void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
