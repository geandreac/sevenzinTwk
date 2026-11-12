using System.Windows;
using System.Windows.Controls;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

public partial class BenchmarkView : UserControl
{
    public BenchmarkView() => InitializeComponent();

    void Rodar_Click(object sender, RoutedEventArgs e)
    {
        int quadros = int.TryParse(QuadBox.Text, out var q) ? Math.Clamp(q, 100, 50000) : 2000;
        BtnRodar.IsEnabled = QuadBox.IsEnabled = false;
        BtnRodar.Content = "Rodando…";
        StatusLbl.Text = $"Medindo {quadros} quadros…";
        Task.Run(() =>
        {
            ResumoFrametime r;
            using (var cena = new CenaBenchmark()) r = BenchmarkFrametime.Rodar(quadros, cena.Quadro);
            Dispatcher.Invoke(() =>
            {
                Mostrar(r);
                BtnRodar.IsEnabled = QuadBox.IsEnabled = true;
                BtnRodar.Content = "Rodar benchmark";
                StatusLbl.Text = $"Concluído · {r.Quadros} quadros · {r.FpsMedio:0.0} FPS médio.";
            });
        });
    }

    void Mostrar(ResumoFrametime r) => ResGrid.ItemsSource = new[]
    {
        new LinhaKV("FPS médio", $"{r.FpsMedio:0.0}"),
        new LinhaKV("Frametime médio", $"{r.FrametimeMedioMs:0.00} ms"),
        new LinhaKV("Frametime p95", $"{r.P95Ms:0.00} ms"),
        new LinhaKV("Frametime p99", $"{r.P99Ms:0.00} ms"),
        new LinhaKV("1% low", $"{r.Fps1PorCentoLow:0.0} FPS"),
        new LinhaKV("Desvio do frametime", $"{r.DesvioMs:0.00} ms"),
        new LinhaKV($"Stutters (> {r.StutterLimiarMs:0.0} ms)", r.Stutters.ToString()),
        new LinhaKV("Quadros medidos", r.Quadros.ToString()),
    };
}

public sealed record LinhaKV(string Metrica, string Valor);
