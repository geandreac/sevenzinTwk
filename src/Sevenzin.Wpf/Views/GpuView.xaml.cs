using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "GPU" (§21.5): diagnóstico de GPU/driver e link OFICIAL do driver. Somente leitura —
/// nenhuma alteração, download ou limpeza de driver.</summary>
public partial class GpuView : UserControl
{
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style SecButton => (Style)Application.Current.Resources["SecondaryButton"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];

    public GpuView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var gpus = await Task.Run(DiagnosticoGpu.Coletar);
            Lista.Children.Clear();
            if (gpus.Count == 0)
            {
                Lista.Children.Add(new TextBlock { Text = "Nenhum adaptador de vídeo detectado.", Foreground = Muted, Margin = new Thickness(2, 8, 0, 0) });
                return;
            }
            foreach (var g in gpus) Lista.Children.Add(CartaoGpu(g));
            Lista.Children.Add(CartaoRecomendacoes());
        };
    }

    static string NomeFab(FabricanteGpu f) => f switch
    {
        FabricanteGpu.Nvidia => "NVIDIA",
        FabricanteGpu.Amd => "AMD",
        FabricanteGpu.Intel => "Intel",
        _ => "não identificado",
    };

    Border CartaoGpu(InfoGpu g)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = g.Adaptador, FontSize = 16, FontWeight = FontWeights.SemiBold });

        var campos = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        campos.Children.Add(Campo("Fabricante", NomeFab(g.Fabricante)));
        campos.Children.Add(Campo("Driver", g.VersaoDriver is null ? "—" : $"{g.VersaoDriver}{(g.DataDriver is { } d ? $"  ·  {d:yyyy-MM-dd}" : "")}"));
        campos.Children.Add(Campo("VRAM reportada", g.Vram ?? "— (WMI satura acima de 4 GB)"));
        campos.Children.Add(Campo("Modo atual", g.ModoAtual ?? "—"));
        sp.Children.Add(campos);

        if (g.DriverPossivelmenteAntigo)
            sp.Children.Add(new TextBlock { Text = "⚠ Driver possivelmente desatualizado — considere atualizar pela página oficial.", Foreground = Warning, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });

        if (g.UrlDriverOficial is { } url)
        {
            var b = new Button { Style = SecButton, Content = $"Abrir página oficial do driver ({NomeFab(g.Fabricante)})", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (_, _) => AbrirUrl(url);
            sp.Children.Add(b);
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    static UIElement Campo(string rotulo, string valor)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var r = new TextBlock { Text = rotulo, Foreground = Muted, FontSize = 13 };
        var v = new TextBlock { Text = valor, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        g.Children.Add(r); g.Children.Add(v);
        return g;
    }

    Border CartaoRecomendacoes()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "RECOMENDAÇÕES SEGURAS (sugestões — nada é aplicado aqui)", Style = SectionStyle });
        foreach (var t in new[]
        {
            "Mantenha o driver atualizado pela página oficial do fabricante (link acima).",
            "Ligue o HAGS em Ajustes e meça antes/depois (A/B) — depende de GPU e driver.",
            "Game DVR desligado reduz o overhead de gravação em jogos.",
            "Diferencie limite de GPU, de CPU, VRAM e frame cap antes de mexer nas configurações gráficas.",
        })
            sp.Children.Add(new TextBlock { Text = "•  " + t, Foreground = Secondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        return new Border { Style = CardStyle, Margin = new Thickness(0, 6, 0, 0), Child = sp };
    }

    static void AbrirUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro ao abrir", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
