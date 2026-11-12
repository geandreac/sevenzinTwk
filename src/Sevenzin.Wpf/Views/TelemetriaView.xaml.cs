using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

public partial class TelemetriaView : UserControl
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    SessaoTelemetria? _sessao;
    CancellationTokenSource? _cts;
    ResumoTelemetria? _ultimo, _baseline;
    DateTime _inicio;

    public TelemetriaView()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => CapturaLbl.Text = $"●  Capturando…  {(int)(DateTime.UtcNow - _inicio).TotalSeconds}s";
    }

    void Medir_Click(object sender, RoutedEventArgs e)
    {
        if (_sessao is null) Iniciar();
        else _cts?.Cancel();
    }

    void Iniciar()
    {
        int dur = Clamp(DurBox.Text, 1, 120, 10), intervalo = Clamp(IntBox.Text, 50, 5000, 1000);
        _sessao = new SessaoTelemetria(intervalo);
        _cts = new CancellationTokenSource();
        _inicio = DateTime.UtcNow;
        DurBox.IsEnabled = IntBox.IsEnabled = false;
        BtnMedir.Content = "Parar";
        CapturaLbl.Visibility = Visibility.Visible;
        CapturaLbl.Text = "●  Capturando…  0s";
        StatusLbl.Text = "Sessão de telemetria em andamento.";
        _timer.Start();

        var ct = _cts.Token;
        Task.Run(async () =>
        {
            _sessao.Iniciar();                                   // warm-up do WMI fora da thread da UI
            try { await Task.Delay(dur * 1000, ct); } catch (TaskCanceledException) { }
            var r = _sessao.Parar();
            Dispatcher.Invoke(() => Finalizar(r));
        });
    }

    void Finalizar(ResumoTelemetria r)
    {
        _timer.Stop();
        _sessao = null; _cts = null;
        CapturaLbl.Visibility = Visibility.Collapsed;
        BtnMedir.Content = "Iniciar medição";
        DurBox.IsEnabled = IntBox.IsEnabled = true;
        _ultimo = r;
        SumGrid.ItemsSource = r.Metricas.Select(m => new LinhaResumo(m)).ToList();
        BtnBase.IsEnabled = r.Amostras > 0;
        StatusLbl.Text = r.Amostras == 0
            ? "Nenhuma amostra capturada — tente um intervalo maior ou duração mais longa."
            : $"Amostras: {r.Amostras} · overhead medido da coleta: {r.OverheadPct:0.0}%";
        if (_baseline is { } b && r.Amostras > 0) MostrarAB(b, r);
    }

    void Base_Click(object sender, RoutedEventArgs e)
    {
        if (_ultimo is not { Amostras: > 0 } b) return;
        _baseline = b;
        AbGrid.ItemsSource = null;
        AbLbl.Text = $"COMPARAÇÃO A/B — base: {b.Amostras} amostras, {b.Inicio.ToLocalTime():HH:mm}. Aplique uma mudança e meça de novo.";
    }

    void MostrarAB(ResumoTelemetria baseR, ResumoTelemetria novo)
    {
        AbGrid.ItemsSource = ComparacaoAB.Comparar(baseR, novo).Select(c => new LinhaAB(c)).ToList();
        AbLbl.Text = $"COMPARAÇÃO A/B — base ({baseR.Amostras}) × atual ({novo.Amostras}) · \"Inconclusivo\" = |Δ| dentro do ruído";
    }

    static int Clamp(string texto, int min, int max, int padrao) =>
        int.TryParse(texto, out var v) ? Math.Clamp(v, min, max) : padrao;
}

public sealed class LinhaResumo
{
    public string Metrica { get; } public string Media { get; } public string P95 { get; }
    public string P99 { get; } public string Desvio { get; } public string Faixa { get; } public string Unidade { get; }
    public LinhaResumo(ResumoMetrica m)
    {
        Metrica = m.Nome; Media = $"{m.Media:0.0}"; P95 = $"{m.P95:0.0}"; P99 = $"{m.P99:0.0}";
        Desvio = $"{m.Desvio:0.0}"; Faixa = $"{m.Min:0.0}–{m.Max:0.0}"; Unidade = m.Unidade;
    }
}

public sealed class LinhaAB
{
    public string Metrica { get; } public string Base { get; } public string Atual { get; }
    public string Delta { get; } public string Ruido { get; } public string Veredito { get; } public Brush Cor { get; }
    public LinhaAB(ResultadoAB c)
    {
        Metrica = c.Metrica; Base = $"{c.Base:0.0}"; Atual = $"{c.Novo:0.0}";
        Delta = $"{(c.Delta >= 0 ? "+" : "")}{c.Delta:0.0}"; Ruido = $"±{c.Ruido:0.0}";
        bool dif = c.Veredito == VereditoAB.DiferencaDetectada;
        Veredito = dif ? "Diferença detectada" : "Inconclusivo";
        Cor = (Brush)Application.Current.Resources[dif ? "WarningBrush" : "TextMutedBrush"];
    }
}
