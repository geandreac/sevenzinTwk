using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Sevenzin.Diagnostics;
using Sevenzin.Wpf.Views;

namespace Sevenzin.Wpf;

public partial class MainWindow : Window
{
    static readonly (string Tag, string Glifo, string Rotulo)[] Itens =
    {
        // Essenciais (uso diário: ver estado → otimizar → por jogo)
        ("inicio", "", "Início"),
        ("ajustes", "", "Ajustes"),
        ("jogos", "", "Jogos"),
        // Hardware & Meu PC
        ("gpu", "", "GPU"),
        ("inventario", "", "Meu PC"),
        // Sistema e Instaladores
        ("windows", "", "Windows"),
        ("instaladores", "", "Instaladores"),
        ("limpeza", "", "Limpeza"),
        ("protecao", "", "Proteção"),
        ("bios", "", "BIOS"),
        // Configurações (reúne Geral, Preferências, Perfil, Notificações, Telemetria e Benchmark)
        ("config", "", "Configurações"),
    };

    readonly List<TextBlock> _rotulos = new();
    readonly BandejaSistema _bandeja;
    bool _expandido;
    bool _encerramentoExplicito;
    string? _subAbaConfigPendente;

    public ToastHost ToastHost => ToastHostPanel;

    public MainWindow()
    {
        InitializeComponent();
        JanelaArredondada.Arredondar(this);
        JanelaMaximizada.Aplicar(this);

        _bandeja = new BandejaSistema(
            aoRestaurar: RestaurarDaBandeja,
            aoAbrirConfig: () => { RestaurarDaBandeja(); SelecionarAba("config"); },
            aoSair: EncerrarCompletamente);

        foreach (var (tag, glifo, rotulo) in Itens)
        {
            var item = new ListBoxItem { Content = ConteudoNav(tag, glifo, rotulo), Tag = tag, ToolTip = rotulo };
            ToolTipService.SetPlacement(item, System.Windows.Controls.Primitives.PlacementMode.Right);
            ToolTipService.SetHorizontalOffset(item, 10);
            ToolTipService.SetInitialShowDelay(item, 220);
            ToolTipService.SetIsEnabled(item, !_expandido);
            System.Windows.Automation.AutomationProperties.SetName(item, rotulo);
            Nav.Items.Add(item);
        }
        Nav.SelectedIndex = 0;
        UsuarioLabel.Text = Servicos.Sessao.Atual?.Usuario.Email ?? "";

        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        SincronizarIconeBandeja();
        if (!App.ModoRender) _ = VerificarAtualizacaoAsync();
        if (!App.ModoRender) IniciarConferenciaDeSessao();
    }

    System.Windows.Threading.DispatcherTimer? _conferenciaSessao;

    /// <summary>Sessão única por conta: a cada 5 min pergunta ao servidor se esta ainda é a sessão ativa. Se a conta
    /// entrou em outro PC (ou o admin liberou o vínculo), volta para o login. Erro de rede não derruba ninguém.</summary>
    void IniciarConferenciaDeSessao()
    {
        if (Servicos.ClienteSupabase is null) return;
        _conferenciaSessao = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _conferenciaSessao.Tick += async (_, _) =>
        {
            if (Servicos.ClienteSupabase is not { } cli || await cli.SessaoAindaValidaAsync() != false) return;
            _conferenciaSessao?.Stop();
            MessageBox.Show(this, "Sua conta foi aberta em outro computador ou o vínculo deste PC foi alterado.\n\n" +
                "Entre novamente. Se não foi você, troque sua senha e abra um ticket no Discord.",
                "Sessão encerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            ForcarRelogin();
        };
        _conferenciaSessao.Start();
    }

    void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.ModoRender || App.RecuperacaoRecente is null) return;
        var rec = App.RecuperacaoRecente;
        App.RecuperacaoRecente = null;
        Toasts.Mostrar(
            this,
            TipoToast.Aviso,
            "Reaberto após fechamento inesperado",
            $"Motivo: {rec.Motivo} · registrado em Notificações.",
            () => SelecionarAba("notificacoes"), "Ver detalhes");
    }

    public void SincronizarIconeBandeja()
    {
        if (App.ModoRender) return;
        var prefs = PreferenciasUiStore.Padrao.Carregar();
        if (prefs.MinimizarParaBandeja || prefs.FecharParaBandeja || !IsVisible)
            _bandeja.Exibir();
        else if (IsVisible)
            _bandeja.Ocultar();
    }

    /// <summary>
    /// Inicia o SevenzinTwks diretamente na bandeja do sistema (perto do relógio)
    /// sem abrir ou piscar a janela na tela do usuário.
    /// </summary>
    public void IniciarMinimizadoNaBandeja()
    {
        _bandeja.Exibir();
        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
        Hide();
    }

    void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Vale também para Win+↑ e duplo clique na barra: o botão sempre mostra a ação que fará.
        bool max = WindowState == WindowState.Maximized;
        MaxBtn.ToolTip = max ? "Restaurar" : "Maximizar";
        MaxIcone.Visibility = max ? Visibility.Collapsed : Visibility.Visible;
        RestaurarIcone.Visibility = max ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(MaxBtn, (string)MaxBtn.ToolTip);
        if (App.ModoRender) return;
        if (WindowState == WindowState.Minimized && PreferenciasUiStore.Padrao.Carregar().MinimizarParaBandeja)
        {
            _bandeja.Exibir();
            Hide();
        }
    }

    public void RestaurarDaBandeja()
    {
        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        SincronizarIconeBandeja();
    }

    public void EncerrarCompletamente()
    {
        _encerramentoExplicito = true;
        RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
        _bandeja.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_encerramentoExplicito && !App.ModoRender && PreferenciasUiStore.Padrao.Carregar().FecharParaBandeja)
        {
            e.Cancel = true;
            _bandeja.Exibir();
            Hide();
            return;
        }
        _bandeja.Dispose();
        base.OnClosing(e);
    }

    async Task VerificarAtualizacaoAsync()
    {
        var versao = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        var r = await VerificadorAtualizacoes.VerificarAsync(versao);
        if (r is not null && r.HaNova)
        {
            _atualizacao = r;
            AvisoAtualizaBtn.Visibility = Visibility.Visible;
        }
    }

    ResultadoAtualizacao? _atualizacao;
    bool _atualizando;

    async void IrParaAtualizacao_Click(object sender, RoutedEventArgs e)
    {
        if (_atualizacao is { Sha256: not null } r)
            await AtualizarEReiniciarAsync(r, t => AvisoAtualizaBtn.Tag = t);
        else
            SelecionarAba("preferencias");
    }

    /// <summary>"Reiniciar para atualizar": confirma, baixa o Setup da release, confere o SHA-256, fecha o app,
    /// instala e reabre a versão nova.</summary>
    public async Task AtualizarEReiniciarAsync(ResultadoAtualizacao r, Action<string> status)
    {
        if (_atualizando || r.UrlInstalador is null || r.Sha256 is null) return;
        if (MessageBox.Show(this, $"Atualizar o SevenzinTwks de {r.VersaoAtual} para {r.VersaoRemota}?\n\n" +
                "O instalador oficial será baixado do GitHub e conferido. O app fecha, a nova versão é instalada e ele reabre sozinho. Seus ajustes e sua conta são mantidos.",
                "Reiniciar para atualizar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _atualizando = true;
        try
        {
            status("Baixando…");
            var progresso = new Progress<(long Baixados, long? Total)>(p =>
                { if (_atualizando && p.Total is > 0) status($"Baixando {p.Baixados * 100 / p.Total}%"); });
            var setup = await AtualizadorApp.BaixarInstaladorAsync(r.UrlInstalador, r.NomeInstalador, r.Sha256, progresso);
            if (!AtualizadorApp.IniciarAtualizacaoEReiniciar(setup, r.Sha256))
                throw new InvalidOperationException("Não foi possível iniciar a instalação.");
            EncerrarCompletamente();
        }
        catch (Exception ex)
        {
            _atualizando = false;
            status("Tentar atualizar de novo");
            Toasts.Mostrar(this, TipoToast.Erro, "Falha ao atualizar", ex.Message);
        }
    }

    void Sair_Click(object sender, RoutedEventArgs e) => ForcarRelogin();

    void ForcarRelogin()
    {
        _conferenciaSessao?.Stop();
        _encerramentoExplicito = true;
        Servicos.Sessao.Encerrar();
        RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
        _bandeja.Dispose();
        var login = new LoginWindow();
        bool ok = login.ShowDialog() == true && Servicos.Sessao.Valida;
        if (ok)
        {
            RecuperacaoFalhaStore.Padrao.MarcarSessaoAtiva(Servicos.Sessao.Atual?.Usuario.Email);
            new MainWindow().Show();
            Close();
        }
        else
        {
            Close();
            Application.Current.Shutdown();
        }
    }

    void Menu_Click(object sender, RoutedEventArgs e)
    {
        _expandido = !_expandido;
        double alvo = _expandido ? 216 : 69;
        foreach (var l in _rotulos) l.Visibility = _expandido ? Visibility.Visible : Visibility.Collapsed;
        foreach (ListBoxItem it in Nav.Items) ToolTipService.SetIsEnabled(it, !_expandido);
        if (SystemParameters.ClientAreaAnimation)
            Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(Sidebar.ActualWidth, alvo, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { Sidebar.BeginAnimation(WidthProperty, null); Sidebar.Width = alvo; }
    }

    static readonly Dictionary<string, string> IconeFigma = new()
    {
        ["inicio"] = "ic_inicio",
        ["ajustes"] = "ic_ajustes",
        ["gpu"] = "ic_gpu",
        ["instaladores"] = "ic_instaladores",
        ["jogos"] = "ic_jogos",
        ["bios"] = "ic_bios",
        ["windows"] = "ic_windows",
        ["config"] = "ic_config",
        ["inventario"] = "ic_inventario",
        ["limpeza"] = "ic_limpeza",
        ["admin"] = "ic_admin",
        ["protecao"] = "ic_protecao",
    };

    Grid ConteudoNav(string tag, string glifo, string rotulo)
    {
        var g = new Grid { Height = 40 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var host = new Grid { Width = 48, Height = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(IconeNav(tag, glifo));
        Grid.SetColumn(host, 0);
        g.Children.Add(host);

        var lbl = new TextBlock
        {
            Text = rotulo,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Visibility = Visibility.Collapsed,
        };
        _rotulos.Add(lbl);
        Grid.SetColumn(lbl, 1);
        g.Children.Add(lbl);
        return g;
    }

    static UIElement IconeNav(string tag, string glifo)
    {
        if (IconeFigma.TryGetValue(tag, out var key) && Application.Current.Resources[key] is Geometry g)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = g,
                StrokeThickness = 1.65,
                Fill = null,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                SnapsToDevicePixels = true,
            };
            p.SetBinding(Shape.StrokeProperty, new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) },
            });
            return new Viewbox
            {
                Width = 18,
                Height = 18,
                Child = p,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UseLayoutRounding = true,
            };
        }
        return new TextBlock { Text = glifo, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Seleciona uma aba pelo tag (suporta também redirecionamento das abas internas de Configurações).</summary>
    public void SelecionarAba(string tag)
    {
        var t = (tag ?? "").Trim().ToLowerInvariant();
        if (t is "meu-pc" or "meupc") t = "inventario";
        if (t is "perfil" or "notificacoes" or "telemetria" or "benchmark" or "preferencias")
        {
            _subAbaConfigPendente = t;
            foreach (ListBoxItem it in Nav.Items)
            {
                if ((string)it.Tag == "config")
                {
                    if (ReferenceEquals(Nav.SelectedItem, it) && Host.Content is ConfiguracoesView cv)
                    {
                        cv.SelecionarSubAba(t);
                        _subAbaConfigPendente = null;
                    }
                    else Nav.SelectedItem = it;
                    return;
                }
            }
        }

        foreach (ListBoxItem it in Nav.Items)
            if ((string)it.Tag == t) { Nav.SelectedItem = it; return; }
    }

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Servicos.Sessao.Atual is not null && Servicos.Sessao.Renovar() is null) { ForcarRelogin(); return; }

        var tag = (Nav.SelectedItem as ListBoxItem)?.Tag as string;
        var subConfig = _subAbaConfigPendente;
        _subAbaConfigPendente = null;

        Host.Content = tag switch
        {
            "inicio" => new InicioView(),
            "ajustes" => new AjustesView(),
            "gpu" => new GpuView(),
            "instaladores" => new InstaladoresView(),
            "limpeza" => new LimpezaView(),
            "jogos" => new JogosView(),
            "bios" => new BiosView(),
            "windows" => new WindowsView(),
            "protecao" => new ProtecaoView(),
            "config" => new ConfiguracoesView(subConfig),
            "inventario" => new InventarioView(),
            _ => new InicioView(),
        };
        int indice = Nav.SelectedIndex;
        Movimento.Entrar(Host, indice < _indiceAnterior ? -10 : 10); // a tela chega do lado para onde a navegação andou
        ViajarSelecao(_indiceAnterior, indice);
        _indiceAnterior = indice;
    }

    int _indiceAnterior;

    // A pílula e o marcador do item escolhido partem da posição do item anterior (um objeto só, não dois em fade).
    void ViajarSelecao(int de, int para)
    {
        if (de == para || Nav.ItemContainerGenerator.ContainerFromIndex(de) is not ListBoxItem a
            || Nav.ItemContainerGenerator.ContainerFromIndex(para) is not ListBoxItem b || !a.IsLoaded || !b.IsLoaded) return;
        double dy = a.TranslatePoint(new Point(), b).Y;
        foreach (var nome in new[] { "selBg", "accent" })
            if (b.Template.FindName(nome, b) is UIElement el) Movimento.Viajar(el, 0, dy);
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Max_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        if (PreferenciasUiStore.Padrao.Carregar().FecharParaBandeja)
        {
            _bandeja.Exibir();
            Hide();
            return;
        }
        EncerrarCompletamente();
    }
}
