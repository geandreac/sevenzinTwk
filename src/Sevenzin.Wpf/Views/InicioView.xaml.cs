using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public TextBlock Sub = null!;
        public Polyline Spark = null!;
        public Border? BadgeBox;
        public readonly List<double> Serie = new();
        public double Max;
    }

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    CardMonitor _cpu = null!, _gpu = null!, _mem = null!, _lat = null!;
    double _totalGB;

    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Accent => (Brush)Application.Current.Resources["AccentCyanBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style EyebrowStyle => (Style)Application.Current.Resources["Eyebrow"];
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
        Raiz.Children.Add(new TextBlock { Text = "OTIMIZAÇÃO", Style = EyebrowStyle });
        Raiz.Children.Add(new TextBlock { Text = "Início", FontFamily = Heading, FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
        Raiz.Children.Add(new TextBlock { Text = "Windows · Monitorando em tempo real", Foreground = Secondary, FontSize = 13, Margin = new Thickness(0, 0, 0, 22) });

        Raiz.Children.Add(new TextBlock { Text = "MONITORES AO VIVO", Style = SectionStyle });
        var grade = new UniformGrid { Rows = 1, Columns = 4, Margin = new Thickness(0, 0, 0, 24) };
        _cpu = Card("CPU", 100, badge: null);
        _gpu = Card("GPU", 100, badge: null);
        _mem = Card("Memória", 100, badge: null);
        _lat = Card("Latência", 150, badge: "Estável");
        AddCard(grade, _cpu, 0, 4);
        AddCard(grade, _gpu, 1, 4);
        AddCard(grade, _mem, 2, 4);
        AddCard(grade, _lat, 3, 4);
        _gpu.Numero.Text = "—";
        Raiz.Children.Add(grade);
    }

    void AddCard(Panel host, CardMonitor c, int indice, int total)
    {
        c.Card.Margin = new Thickness(indice == 0 ? 0 : 6, 0, indice == total - 1 ? 0 : 6, 0);
        host.Children.Add(c.Card);
    }

    CardMonitor Card(string titulo, double max, string? badge)
    {
        var c = new CardMonitor { Max = max };
        var sp = new StackPanel();

        var topo = new Grid();
        topo.Children.Add(new TextBlock { Text = titulo, Foreground = Muted, FontSize = 12 });
        if (badge is not null)
        {
            var b = new Border { Background = SuccessBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            b.Child = new TextBlock { Text = badge, Foreground = Success, FontSize = 10, FontWeight = FontWeights.SemiBold };
            c.BadgeBox = b;
            topo.Children.Add(b);
        }
        sp.Children.Add(topo);

        c.Numero = new TextBlock { Text = "…", FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(c.Numero);
        c.Sub = new TextBlock { Text = "", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 2, 0, 10), TextTrimming = TextTrimming.CharacterEllipsis };
        sp.Children.Add(c.Sub);

        c.Spark = new Polyline { Stroke = Accent, StrokeThickness = 1.6, Stretch = Stretch.Fill, StrokeLineJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Bottom };
        sp.Children.Add(new Border { Height = 30, Child = c.Spark });

        c.Card = new Border { Style = CardStyle, Child = sp };
        return c;
    }

    async System.Threading.Tasks.Task IniciarAsync()
    {
        var itens = await System.Threading.Tasks.Task.Run(Relatorio.Coletar);
        string Val(string nome) => itens.FirstOrDefault(i => i.Nome == nome)?.Valor ?? "—";

        _cpu.Sub.Text = Val("Modelo");
        _gpu.Sub.Text = Val("Adaptador");
        var total = Val("Capacidade total");
        _mem.Sub.Text = total == "—" ? "memória do sistema" : $"de {total}";
        _lat.Sub.Text = "Google DNS";
        _totalGB = ExtrairGB(total);
        if (_totalGB > 0) _mem.Max = _totalGB;

        // SISTEMA (faixa compacta com divisores verticais — Figma 3:1039)
        Raiz.Children.Add(new TextBlock { Text = "SISTEMA", Style = SectionStyle });
        var freq = itens.FirstOrDefault(i => i.Nome == "Frequência configurada")?.Valor;
        var campos = new[]
        {
            ("CPU", Val("Modelo")), ("GPU", Val("Adaptador")),
            ("RAM", freq is null ? total : $"{total} · {freq}"), ("SO", Val("Windows")),
        };
        var sis = new Grid();
        for (int i = 0; i < campos.Length; i++)
        {
            sis.ColumnDefinitions.Add(new ColumnDefinition());
            var cell = CampoSistema(campos[i].Item1, campos[i].Item2, i == 0 ? 0 : 21);
            Grid.SetColumn(cell, i);
            sis.Children.Add(cell);
            if (i > 0)
            {
                var d = new System.Windows.Shapes.Rectangle { Width = 1, Height = 38, Fill = Borda, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(d, i);
                sis.Children.Add(d);
            }
        }
        Raiz.Children.Add(new Border { Style = CardStyle, Padding = new Thickness(21, 11, 21, 11), Margin = new Thickness(0, 0, 0, 22), Child = sis });

        // RECURSOS
        Raiz.Children.Add(new TextBlock { Text = "RECURSOS", Style = SectionStyle });
        var rec = new UniformGrid { Rows = 1, Columns = 2 };
        rec.Children.Add(CardRecurso("Ajustes de driver", "Perfis de driver para NVIDIA e AMD.", 0));
        rec.Children.Add(CardRecurso("Perfil por jogo", "Afinidade e prioridade por título.", 1));
        Raiz.Children.Add(rec);

        _timer.Start();
    }

    async System.Threading.Tasks.Task TickAsync()
    {
        var a = await System.Threading.Tasks.Task.Run(LeituraAtual.Agora);
        if (a is { } s)
        {
            Empurrar(_cpu, s.CpuPct);
            _cpu.Numero.Text = $"{s.CpuPct:0}%";
            double usadaGB = _totalGB > 0 ? Math.Max(0, _totalGB - s.RamDisponivelMB / 1024.0) : s.RamCommittedPct;
            Empurrar(_mem, usadaGB);
            _mem.Numero.Text = _totalGB > 0 ? $"{usadaGB:0.0} GB" : $"{s.RamCommittedPct:0}%";
        }

        // Uso de GPU (%) pelos contadores "GPU Engine"; "—" quando indisponível (nunca 0 forjado).
        var gpu = await System.Threading.Tasks.Task.Run(LeitorGpuUso.LerUtilizacao);
        if (gpu is { } g)
        {
            Empurrar(_gpu, g);
            _gpu.Numero.Text = $"{g:0}%";
            _gpu.Card.ToolTip = null;
        }
        else
        {
            _gpu.Numero.Text = "—";
            _gpu.Card.ToolTip = "Uso de GPU indisponível neste equipamento (sem o contador 'GPU Engine' do Windows).";
        }

        var ping = await System.Threading.Tasks.Task.Run(() =>
        {
            try { return new Ping().Send("8.8.8.8", 1000); } catch { return null; }
        });
        if (ping is { Status: IPStatus.Success })
        {
            Empurrar(_lat, ping.RoundtripTime);
            _lat.Numero.Text = $"{ping.RoundtripTime} ms";
            _lat.Sub.Text = "Google DNS · sem perda";
            if (_lat.BadgeBox is { } bb) bb.Visibility = Visibility.Visible; // "Estável" só com rede real
        }
        else
        {
            _lat.Numero.Text = "—";
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

    static FrameworkElement CampoSistema(string rotulo, string valor, double margemEsq)
    {
        var sp = new StackPanel { Margin = new Thickness(margemEsq, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = rotulo, Foreground = Muted, FontSize = 11 });
        // nome completo preservado no tooltip; mostra compacto com reticências se não couber
        sp.Children.Add(new TextBlock { Text = valor, FontSize = 13, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = valor });
        return sp;
    }

    UIElement CardRecurso(string titulo, string sub, int indice)
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        sp.Children.Add(new TextBlock { Text = titulo, FontSize = 15, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock { Text = sub, Foreground = Secondary, FontSize = 13, Margin = new Thickness(0, 4, 0, 0) });
        // Degradê diagonal amostrado do Figma 3:1052 (topo-esq claro → rodapé/direita preto)
        Color C(string k) => (Color)Application.Current.Resources[k];
        var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        grad.GradientStops.Add(new GradientStop(C("ResGradTop"), 0));
        grad.GradientStops.Add(new GradientStop(C("ResGradMid"), 0.5));
        grad.GradientStops.Add(new GradientStop(C("ResGradBot"), 1));
        return new Border
        {
            Background = grad,
            BorderBrush = Borda,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Height = 160,
            Margin = new Thickness(indice == 0 ? 0 : 7, 0, indice == 0 ? 7 : 0, 0),
            Child = sp,
        };
    }

    static double ExtrairGB(string texto)
    {
        var m = System.Text.RegularExpressions.Regex.Match(texto, @"([0-9]+[.,]?[0-9]*)");
        return m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
