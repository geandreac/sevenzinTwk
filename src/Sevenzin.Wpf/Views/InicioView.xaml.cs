using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Dashboard "Início": monitores ao vivo (CPU, memória, latência) com número grande, sublabel e
/// sparkline, cartões de sistema (inventário real) e atalhos de recursos. Atualiza em tempo real.</summary>
public partial class InicioView : UserControl
{
    sealed class CardMonitor
    {
        public Border Card = null!;
        public TextBlock Numero = null!;
        public TextBlock Unidade = null!;
        public TextBlock Sub = null!;
        public Polyline Spark = null!;
        public Border? BadgeBox;
        public readonly List<double> Serie = new();
        public double Max;
    }

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    readonly MedidorCpuUso _cpuUso = new(); // GetSystemTimes (05 §3): delta puro, sem warm-up de WMI
    CardMonitor _cpu = null!, _gpu = null!, _mem = null!, _lat = null!;
    TextBlock _subtitulo = null!, _pctTxt = null!, _prontSub = null!;
    System.Windows.Shapes.Path _anel = null!;
    UniformGrid _barras = null!;
    StackPanel _detalhes = null!;
    Border _prontCard = null!;
    Button _otimizarBtn = null!, _desfazerBtn = null!, _verBtn = null!;
    CheckBox _completo = null!;
    ResultadoProntidao? _ultimo;
    FabricanteGpu _fabricante;
    bool _naBateria;
    double _totalGB;

    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Accent => (Brush)Application.Current.Resources["AccentCyanBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Brush Borda => (Brush)Application.Current.Resources["BorderDefaultBrush"];
    static FontFamily Heading => (FontFamily)Application.Current.Resources["HeadingFont"];

    public InicioView()
    {
        InitializeComponent();
        MontarEsqueleto();
        _timer.Tick += async (_, _) => await TickAsync();
        Unloaded += (_, _) => _timer.Stop();
        Loaded += async (_, _) => await IniciarAsync();
    }

    void MontarEsqueleto()
    {
        Raiz.Children.Add(new TextBlock { Text = "Início", FontFamily = Heading, FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
        _subtitulo = new TextBlock { Text = "Windows · Monitorando em tempo real", Foreground = Secondary, FontSize = 13, Margin = new Thickness(0, 2, 0, 18) };
        Raiz.Children.Add(_subtitulo);
        Raiz.Children.Add(CartaoProntidao()); // preenchido com o estado REAL em IniciarAsync

        // Monitores: uma superfície só, quatro colunas com divisória.
        Raiz.Children.Add(new TextBlock { Text = "Agora", Style = SectionStyle });
        var grade = new Grid();
        _cpu = Monitor("CPU", 100);
        _gpu = Monitor("GPU", 100);
        _mem = Monitor("Memória", 100);
        _lat = Monitor("Latência", 150, badge: "Estável");
        var todos = new[] { _cpu, _gpu, _mem, _lat };
        for (int i = 0; i < todos.Length; i++)
        {
            grade.ColumnDefinitions.Add(new ColumnDefinition());
            todos[i].Card.BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0);
            Grid.SetColumn(todos[i].Card, i);
            grade.Children.Add(todos[i].Card);
        }
        Raiz.Children.Add(new Border { Style = CardStyle, Padding = new Thickness(0, 16, 0, 16), Margin = new Thickness(0, 0, 0, 28), Child = grade });

        Raiz.Children.Add(new TextBlock { Text = "Jogos em destaque", Style = SectionStyle });
        var jogos = new UniformGrid { Rows = 2, Columns = 4 };
        var nomes = new[] { "Counter-Strike 2", "EA SPORTS FC", "FiveM", "Fortnite", "PUBG", "Rust", "VALORANT", "Warzone" };
        for (int i = 0; i < nomes.Length; i++) jogos.Children.Add(AtalhoJogo(nomes[i], i));
        Raiz.Children.Add(jogos);
    }

    CardMonitor Monitor(string titulo, double max, string? badge = null)
    {
        var c = new CardMonitor { Max = max };
        var sp = new StackPanel { Margin = new Thickness(18, 0, 18, 0) };

        var topo = new Grid { Height = 18 };
        topo.Children.Add(new TextBlock { Text = titulo, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        if (badge is not null)
        {
            c.BadgeBox = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = badge, Foreground = Success, FontSize = 12 } };
            topo.Children.Add(c.BadgeBox);
        }
        sp.Children.Add(topo);

        // Valor grande, unidade recuada; algarismos tabulares para o número não "dançar" a cada leitura.
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        c.Numero = new TextBlock { Text = "—", FontSize = 28, FontWeight = FontWeights.SemiBold };
        System.Windows.Documents.Typography.SetNumeralAlignment(c.Numero, FontNumeralAlignment.Tabular);
        c.Unidade = new TextBlock { Foreground = Muted, FontSize = 13, Margin = new Thickness(5, 0, 0, 5), VerticalAlignment = VerticalAlignment.Bottom };
        linha.Children.Add(c.Numero);
        linha.Children.Add(c.Unidade);
        sp.Children.Add(linha);
        c.Sub = new TextBlock { Text = "", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 12), TextTrimming = TextTrimming.CharacterEllipsis };
        sp.Children.Add(c.Sub);

        c.Spark = new Polyline { Stroke = Accent, StrokeThickness = 1.6, Stretch = Stretch.Fill, StrokeLineJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Bottom };
        sp.Children.Add(new Border { Height = 30, Child = c.Spark });

        c.Card = new Border { BorderBrush = Borda, Child = sp, Background = Brushes.Transparent };
        return c;
    }

    async System.Threading.Tasks.Task IniciarAsync()
    {
        var itens = await System.Threading.Tasks.Task.Run(Relatorio.Coletar);
        string Val(string nome) => itens.FirstOrDefault(i => i.Nome == nome)?.Valor ?? "—";

        _cpu.Sub.Text = Val("Modelo");
        _gpu.Sub.Text = Val("Adaptador");
        var total = Val("Capacidade total");
        var freq = itens.FirstOrDefault(i => i.Nome == "Frequência configurada")?.Valor;
        _mem.Sub.Text = total == "—" ? "memória do sistema" : $"de {total}" + (freq is null ? "" : $" · {freq}");
        _lat.Sub.Text = "Google DNS";
        _totalGB = ExtrairGB(total);
        if (_totalGB > 0) _mem.Max = _totalGB;

        // Subtítulo com a edição real do Windows (ex.: "Windows 11 Pro 25H2") e a prontidão para jogos lida do sistema.
        _subtitulo.Text = (Val("Windows") != "—" ? Val("Windows") : "Windows") + " · Monitorando em tempo real";
        _fabricante = DriverGpu.Detectar(Val("Adaptador"));
        _naBateria = SystemParameters.PowerLineStatus == PowerLineStatus.Offline;
        _cpuUso.Ler(); // 1ª amostra do GetSystemTimes: o próximo tick já tem intervalo para calcular o %
        _timer.Start();
        await ReavaliarAsync();
    }

    async System.Threading.Tasks.Task TickAsync()
    {
        // CPU do sistema via GetSystemTimes (delta entre ticks). "…" até haver intervalo — nunca 0 forjado.
        var cpu = await System.Threading.Tasks.Task.Run(_cpuUso.Ler);
        if (cpu is { } c)
        {
            Empurrar(_cpu, c);
            _cpu.Numero.Text = $"{c:0}"; _cpu.Unidade.Text = "%";
            _cpu.Card.ToolTip = null;
        }
        else
        {
            _cpu.Card.ToolTip = "Uso de CPU indisponível (GetSystemTimes não respondeu).";
        }

        // Memória via WMI formatado (a parte de CPU do mesmo coletor não é mais usada aqui).
        var a = await System.Threading.Tasks.Task.Run(LeituraAtual.Agora);
        if (a is { } s)
        {
            double usadaGB = _totalGB > 0 ? Math.Max(0, _totalGB - s.RamDisponivelMB / 1024.0) : s.RamCommittedPct;
            Empurrar(_mem, usadaGB);
            _mem.Numero.Text = _totalGB > 0 ? $"{usadaGB:0.0}" : $"{s.RamCommittedPct:0}"; _mem.Unidade.Text = _totalGB > 0 ? "GB" : "%";
        }

        // Uso de GPU (%) pelos contadores "GPU Engine"; "—" quando indisponível (nunca 0 forjado).
        var gpu = await System.Threading.Tasks.Task.Run(LeitorGpuUso.LerUtilizacao);
        if (gpu is { } g)
        {
            Empurrar(_gpu, g);
            _gpu.Numero.Text = $"{g:0}"; _gpu.Unidade.Text = "%";
            _gpu.Card.ToolTip = null;
        }
        else
        {
            _gpu.Numero.Text = "—"; _gpu.Unidade.Text = "";
            _gpu.Card.ToolTip = "Uso de GPU indisponível neste equipamento (sem o contador 'GPU Engine' do Windows).";
        }

        var ping = await System.Threading.Tasks.Task.Run(() =>
        {
            try { return new Ping().Send("8.8.8.8", 1000); } catch { return null; }
        });
        if (ping is { Status: IPStatus.Success })
        {
            Empurrar(_lat, ping.RoundtripTime);
            _lat.Numero.Text = $"{ping.RoundtripTime}"; _lat.Unidade.Text = "ms";
            _lat.Sub.Text = "Google DNS · sem perda";
            if (_lat.BadgeBox is { } bb) bb.Visibility = Visibility.Visible; // "Estável" só com rede real
        }
        else
        {
            _lat.Numero.Text = "—"; _lat.Unidade.Text = "";
            _lat.Sub.Text = "Google DNS · indisponível";
            if (_lat.BadgeBox is { } bb) bb.Visibility = Visibility.Collapsed;
        }
    }

    void Empurrar(CardMonitor c, double v)
    {
        c.Serie.Add(v);
        if (c.Serie.Count > 24) c.Serie.RemoveAt(0);
        if (c.Serie.Count < 2) { c.Spark.Points = new PointCollection(); return; }
        var pts = new PointCollection();
        for (int i = 0; i < c.Serie.Count; i++)
            pts.Add(new Point(i, c.Max - Math.Clamp(c.Serie[i], 0, c.Max)));
        c.Spark.Points = pts;
    }

    /// <summary>Atalho de um jogo em destaque (os 8 confirmados na engenharia reversa): leva à tela Jogos.</summary>
    Button AtalhoJogo(string nome, int indice)
    {
        var b = new Button { Content = nome, Style = (Style)Application.Current.Resources["Atalho"],
            Margin = new Thickness(indice % 4 == 0 ? 0 : 5, indice >= 4 ? 10 : 0, indice % 4 == 3 ? 0 : 5, 0) };
        b.Click += (_, _) => { if (Window.GetWindow(this) is MainWindow mw) mw.SelecionarAba("jogos"); };
        return b;
    }

    // ── Prontidão para jogos (docs/estudo-prontidao-jogos.md) — fases 1 e 2: só leitura ──
    const double TamAnel = 132, RaioAnel = 59;

    UIElement CartaoProntidao()
    {
        var anelHost = new Grid { Width = TamAnel, Height = TamAnel, VerticalAlignment = VerticalAlignment.Top };
        anelHost.Children.Add(new Ellipse { Width = RaioAnel * 2, Height = RaioAnel * 2, Stroke = (Brush)Application.Current.Resources["Surface3Brush"], StrokeThickness = 8 });
        _anel = new System.Windows.Shapes.Path { StrokeThickness = 8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Stroke = Muted };
        anelHost.Children.Add(_anel);
        _pctTxt = new TextBlock { Text = "…", FontFamily = Heading, FontSize = 34, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Documents.Typography.SetNumeralAlignment(_pctTxt, FontNumeralAlignment.Tabular);
        anelHost.Children.Add(_pctTxt);

        var textos = new StackPanel { Margin = new Thickness(28, 0, 0, 0) };
        textos.Children.Add(new TextBlock { Text = "Prontidão para jogos", FontSize = 18, FontWeight = FontWeights.SemiBold });
        _prontSub = new TextBlock { Text = "Verificando ajustes…", Foreground = Secondary, FontSize = 13, Margin = new Thickness(0, 3, 0, 16), TextWrapping = TextWrapping.Wrap };
        textos.Children.Add(_prontSub);
        _barras = new UniformGrid { Columns = 3 };
        textos.Children.Add(_barras);

        // Fase 3: aplicar o que falta em lote (prévia + 1 confirmação + backup) e desfazer o lote inteiro.
        var acoes = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        _otimizarBtn = new Button { Content = "Otimizar para jogos", Style = (Style)Application.Current.Resources["PrimaryButton"], IsEnabled = false, Margin = new Thickness(0, 0, 12, 0) };
        _otimizarBtn.Click += async (_, _) => await OtimizarAsync();
        _completo = new CheckBox { Content = "Incluir recomendados (risco médio)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0),
            ToolTip = "Também aplica HAGS, prioridade de CPU, TCP e ajustes de driver. O catálogo pede para medir antes e depois." };
        _desfazerBtn = new Button { Content = "Desfazer tudo", Style = (Style)Application.Current.Resources["SecondaryButton"], IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
        _desfazerBtn.Click += async (_, _) => await DesfazerTudoAsync();
        var bench = new Button { Content = "Medir no Benchmark", Style = (Style)Application.Current.Resources["SecondaryButton"],
            ToolTip = "Meça antes e depois de otimizar: o ganho real só aparece medindo." };
        bench.Click += (_, _) => { if (Window.GetWindow(this) is MainWindow mw) mw.SelecionarAba("benchmark"); };
        _completo.Click += (_, _) => AtualizarAcao();
        foreach (var b in new FrameworkElement[] { _otimizarBtn, _completo, _desfazerBtn, bench })
        {
            b.Margin = new Thickness(0, 0, b.Margin.Right, 8); // respiro quando a linha quebra na janela estreita
            acoes.Children.Add(b);
        }
        textos.Children.Add(acoes);

        var ver = new Button { Content = "Ver detalhes", Style = (Style)Application.Current.Resources["SecondaryButton"], VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0), IsEnabled = false };
        _detalhes = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 16, 0, 0) };
        ver.Click += (_, _) =>
        {
            bool abrir = (string)ver.Content == "Ver detalhes";
            ver.Content = abrir ? "Ocultar detalhes" : "Ver detalhes";
            Movimento.Expandir(_detalhes, abrir);
        };
        _verBtn = ver;

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition());
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(textos, 1); Grid.SetColumn(ver, 2);
        Grid.SetRow(_detalhes, 1); Grid.SetColumnSpan(_detalhes, 3);
        g.Children.Add(anelHost); g.Children.Add(textos); g.Children.Add(ver); g.Children.Add(_detalhes);

        _prontCard = new Border { Style = CardStyle, Margin = new Thickness(0, 4, 0, 22), Child = g };
        System.Windows.Automation.AutomationProperties.SetName(_prontCard, "Prontidão para jogos");
        return _prontCard;
    }

    async System.Threading.Tasks.Task ReavaliarAsync()
    {
        try
        {
            var fab = _fabricante; bool bat = _naBateria;
            var r = await System.Threading.Tasks.Task.Run(() => ProntidaoJogos.Avaliar(new MotorTweaks(new RegistroWindows()), fab, bat));
            _ultimo = r;
            PreencherProntidao(r);
            AtualizarAcao();
            _desfazerBtn.IsEnabled = ProntidaoJogos.LerLote().Ids.Count > 0;
            if (!App.ModoRender) AvisarDesvios(r);
        }
        catch { _prontSub.Text = "Não foi possível ler os ajustes agora."; }
    }

    // Fase 4: o lote aplicou e algo (Windows Update, outro programa) voltou ao padrão → avisa uma vez em Notificações.
    static void AvisarDesvios(ResultadoProntidao r)
    {
        var lote = ProntidaoJogos.LerLote();
        var d = ProntidaoJogos.Desvios(r.Itens, lote).OrderBy(i => i.Id).ToList();
        var ids = d.Select(i => i.Id).ToList();
        if (ids.SequenceEqual(lote.DesviosAvisados)) return;
        ProntidaoJogos.SalvarLote(lote with { DesviosAvisados = ids });
        if (d.Count == 0) return;
        NotificacoesStore.Padrao.Adicionar("Prontidão", $"{d.Count} ajuste(s) para jogos voltaram ao padrão",
            string.Join(", ", d.Select(i => i.Nome)) + ". Provavelmente o Windows Update ou outro programa. " +
            "Para reaplicar, use \"Otimizar para jogos\" na tela Início.");
    }

    // O botão diz quantos ajustes faltam; sem nada pendente, vira estado em vez de ação.
    void AtualizarAcao()
    {
        if (_ultimo is null) return;
        int n = ProntidaoJogos.Pendentes(_ultimo.Itens, _completo.IsChecked == true).Count;
        _otimizarBtn.Content = n == 0 ? "Tudo em dia" : $"Otimizar para jogos · {n}";
        _otimizarBtn.Style = (Style)Application.Current.Resources[n == 0 ? "SecondaryButton" : "PrimaryButton"];
        _otimizarBtn.IsEnabled = n > 0;
    }

    async System.Threading.Tasks.Task OtimizarAsync()
    {
        if (_ultimo is null) return;
        var pend = ProntidaoJogos.Pendentes(_ultimo.Itens, _completo.IsChecked == true);
        if (pend.Count == 0)
        {
            Toasts.Mostrar(this, TipoToast.Info, "Nada pendente", _completo.IsChecked == true
                ? "Todos os ajustes recomendados já estão ativos."
                : "Os ajustes necessários já estão ativos. Marque \"Incluir recomendados\" para ver os de risco médio.");
            return;
        }
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Aplicar ajustes de sistema exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }

        var previa = new System.Text.StringBuilder($"Vai aplicar {pend.Count} ajuste(s), com backup de cada valor:\n");
        foreach (var g in pend.GroupBy(i => i.Componente))
        {
            previa.Append($"\n{g.Key.ToUpperInvariant()}\n");
            foreach (var i in g) previa.Append($"  • {i.Nome}{(i.Risco.Length > 0 ? $" — risco {i.Risco}" : "")}\n");
        }
        previa.Append("\n\"Desfazer tudo\" reverte este lote. Alguns ajustes (HAGS, inicialização rápida) só valem depois de reiniciar o PC.\n\nContinuar?");
        if (MessageBox.Show(previa.ToString(), "Otimizar para jogos", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _otimizarBtn.IsEnabled = _desfazerBtn.IsEnabled = false;
        _prontSub.Text = $"Aplicando {pend.Count} ajuste(s)…";
        var fab = _fabricante;
        var r = await System.Threading.Tasks.Task.Run(() =>
        {
            var motor = new MotorTweaks(new RegistroWindows());
            var grupos = ProntidaoJogos.GruposDriver(fab);
            return ProntidaoJogos.AplicarLote(pend, i => ProntidaoJogos.Executar(i.Id, true, motor, grupos));
        });

        var lote = ProntidaoJogos.LerLote();
        ProntidaoJogos.SalvarLote(new LoteProntidao(lote.Ids.Union(r.Aplicados.Select(i => i.Id)).ToList(), new()));
        var detalhe = $"{r.Aplicados.Count} de {pend.Count} ajuste(s) aplicados com backup." +
            (r.Falhas.Count > 0 ? " Falharam: " + string.Join("; ", r.Falhas.Select(f => $"{f.Item.Nome} ({f.Motivo})")) + "." : "") +
            " Reinicie o PC para os que pedem reinício.";
        NotificacoesStore.Padrao.Adicionar("Prontidão", "Otimização para jogos aplicada", detalhe);
        Toasts.Mostrar(this, r.Falhas.Count == 0 ? TipoToast.Sucesso : TipoToast.Aviso,
            r.Falhas.Count == 0 ? "Otimizado para jogos" : "Otimização parcial", detalhe, async () => await DesfazerTudoAsync(confirmar: false));
        await ReavaliarAsync();
    }

    async System.Threading.Tasks.Task DesfazerTudoAsync(bool confirmar = true)
    {
        var lote = ProntidaoJogos.LerLote();
        if (lote.Ids.Count == 0) return;
        if (confirmar && MessageBox.Show($"Desfazer os {lote.Ids.Count} ajuste(s) aplicados por \"Otimizar para jogos\"?\n\n" +
                "Cada valor volta ao que era antes. O plano de energia volta ao Equilibrado e o TCP ao padrão do Windows.",
                "Desfazer tudo", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _otimizarBtn.IsEnabled = _desfazerBtn.IsEnabled = false;
        _prontSub.Text = "Desfazendo…";
        var fab = _fabricante;
        var falhas = await System.Threading.Tasks.Task.Run(() =>
        {
            var motor = new MotorTweaks(new RegistroWindows());
            var grupos = ProntidaoJogos.GruposDriver(fab);
            return lote.Ids.Select(id => (id, motivo: ProntidaoJogos.Executar(id, false, motor, grupos))).Where(x => x.motivo is not null).ToList();
        });
        ProntidaoJogos.SalvarLote(new LoteProntidao(new(), new()));
        var detalhe = $"{lote.Ids.Count - falhas.Count} de {lote.Ids.Count} ajuste(s) revertidos." +
            (falhas.Count > 0 ? " Sem reverter: " + string.Join("; ", falhas.Select(f => $"{f.id} ({f.motivo})")) + "." : "");
        NotificacoesStore.Padrao.Adicionar("Prontidão", "Otimização para jogos desfeita", detalhe);
        Toasts.Mostrar(this, falhas.Count == 0 ? TipoToast.Sucesso : TipoToast.Aviso, "Ajustes desfeitos", detalhe);
        await ReavaliarAsync();
    }

    static Brush CorPct(int pct) => (Brush)Application.Current.Resources[pct >= 80 ? "SuccessBrush" : pct >= 40 ? "WarningBrush" : "DangerBrush"];

    void PreencherProntidao(ResultadoProntidao r)
    {
        var cor = CorPct(r.Percentual);
        _anel.Stroke = cor;
        _prontSub.Text = $"{r.Ativos} de {r.Total} ajustes recomendados ativos. Conta ajustes, não FPS.";
        System.Windows.Automation.AutomationProperties.SetName(_prontCard, $"Prontidão para jogos, {r.Percentual} por cento");
        AnimarAnel(r.Percentual);

        _barras.Children.Clear();
        foreach (var c in r.Componentes) _barras.Children.Add(BarraComponente(c));

        _detalhes.Children.Clear();
        foreach (var comp in r.Componentes)
        {
            _detalhes.Children.Add(new TextBlock { Text = comp.Nome, Style = SectionStyle, Margin = new Thickness(0, 10, 0, 4) });
            foreach (var i in r.Itens.Where(i => i.Componente == comp.Nome))
                _detalhes.Children.Add(LinhaItem(i));
        }
        var naoLidos = r.Itens.Where(i => i.Aplicado is null).ToList();
        if (naoLidos.Count > 0)
            _detalhes.Children.Add(new TextBlock { Text = $"Não foi possível ler {naoLidos.Count} item(ns): " + string.Join(", ", naoLidos.Select(i => i.Nome)) + ". Ficaram fora da conta.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        _detalhes.Children.Add(new TextBlock { Text = "Itens necessários pesam 3, recomendados pesam 1. Segurança (UAC, Defender…), timer global e ajustes de memória não entram.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        _verBtn.IsEnabled = true;
    }

    static UIElement BarraComponente(ComponenteProntidao c)
    {
        var trilho = new Grid { Height = 6, Margin = new Thickness(0, 4, 0, 0) };
        trilho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(c.Percentual, 0.001), GridUnitType.Star) });
        trilho.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - c.Percentual, 0.001), GridUnitType.Star) });
        var fundo = new Border { CornerRadius = new CornerRadius(3), Background = (Brush)Application.Current.Resources["Surface3Brush"] };
        Grid.SetColumnSpan(fundo, 2);
        trilho.Children.Add(fundo);
        var cheio = new Border { CornerRadius = new CornerRadius(3), Background = CorPct(c.Percentual), RenderTransform = new ScaleTransform(1, 1) };
        Movimento.Contar(0, 1, v => ((ScaleTransform)cheio.RenderTransform).ScaleX = v, 600);
        trilho.Children.Add(cheio);

        var topo = new DockPanel();
        var pct = new TextBlock { Text = $"{c.Percentual}%", Foreground = Secondary, FontSize = 11 };
        DockPanel.SetDock(pct, Dock.Right);
        topo.Children.Add(pct);
        topo.Children.Add(new TextBlock { Text = c.Nome, FontSize = 12 });

        var sp = new StackPanel { Margin = new Thickness(0, 0, 18, 10), ToolTip = $"{c.Nome}: {c.Ativos} de {c.Total} ajustes ativos" };
        sp.Children.Add(topo);
        sp.Children.Add(trilho);
        System.Windows.Automation.AutomationProperties.SetName(sp, $"{c.Nome}, {c.Percentual} por cento");
        return sp;
    }

    static UIElement LinhaItem(ItemProntidao i)
    {
        var (icone, cor) = i.Aplicado switch { true => ("✓", Success), false => ("○", Muted), null => ("—", Muted) };
        var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        var nivel = new TextBlock { Text = i.Nivel == NivelProntidao.Necessario ? "Necessário" : "Recomendado", Foreground = Muted, FontSize = 11 };
        DockPanel.SetDock(nivel, Dock.Right);
        d.Children.Add(nivel);
        d.Children.Add(new TextBlock { Text = icone, Foreground = cor, Width = 20, FontWeight = FontWeights.SemiBold });
        d.Children.Add(new TextBlock { Text = i.Nome, FontSize = 12, Foreground = i.Aplicado == true ? Secondary : (Brush)Application.Current.Resources["TextPrimaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
        return d;
    }

    double _anelAtual;
    void AnimarAnel(int alvo)
    {
        // Anel e número saem do valor anterior: a mudança depois de otimizar fica visível.
        Movimento.Contar(_anelAtual, alvo, v => { _anel.Data = Arco(v); _pctTxt.Text = $"{v:0}%"; }, 700);
        _anelAtual = alvo;
    }

    static Geometry Arco(double pct)
    {
        double c = TamAnel / 2;
        if (pct <= 0) return Geometry.Empty;
        if (pct >= 100) return new EllipseGeometry(new Point(c, c), RaioAnel, RaioAnel);
        double ang = pct / 100 * 2 * Math.PI;
        var fig = new PathFigure { StartPoint = new Point(c, c - RaioAnel) };
        fig.Segments.Add(new ArcSegment(new Point(c + RaioAnel * Math.Sin(ang), c - RaioAnel * Math.Cos(ang)), new Size(RaioAnel, RaioAnel), 0, pct > 50, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { fig });
    }

    static double ExtrairGB(string texto)
    {
        var m = System.Text.RegularExpressions.Regex.Match(texto, @"([0-9]+[.,]?[0-9]*)");
        return m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
