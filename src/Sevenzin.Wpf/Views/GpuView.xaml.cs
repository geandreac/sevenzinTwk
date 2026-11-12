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
            Lista.Children.Add(CartaoModosVideo());
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

    /// <summary>Modos de vídeo suportados (EnumDisplaySettingsW/DEVMODE — paridade 05 §2). Somente leitura:
    /// lista resolução × Hz por monitor; nada altera o modo atual. "—" quando a API não responde.</summary>
    Border CartaoModosVideo()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Modos de vídeo suportados (somente leitura)", FontSize = 16, FontWeight = FontWeights.SemiBold });

        var dispositivos = LeitorModosVideo.Dispositivos();
        var alvos = dispositivos.Count > 0 ? dispositivos : new List<DispositivoVideo> { new("", "Exibição primária") };
        foreach (var d in alvos)
        {
            var nomeAlvo = d.Nome.Length > 0 ? d.Nome : null; // null = primário
            var modos = LeitorModosVideo.Listar(nomeAlvo);
            var atual = LeitorModosVideo.Atual(nomeAlvo);

            var titulo = $"{d.Descricao}{(d.Nome.Length > 0 ? $" ({d.Nome})" : "")}";
            sp.Children.Add(new TextBlock
            {
                Text = atual is { } a ? $"{titulo} — em uso: {a.Largura}×{a.Altura} @ {a.Hz} Hz" : titulo,
                FontSize = 13,
                Foreground = Secondary,
                Margin = new Thickness(0, 10, 0, 4),
            });

            if (modos.Count == 0)
            {
                sp.Children.Add(new TextBlock { Text = "Indisponível neste equipamento.", Foreground = Muted, FontSize = 12 });
                continue;
            }

            // Agrupa por resolução e mostra as taxas: "1920×1080 — 60 · 120 · 144 Hz"
            foreach (var grupo in modos.GroupBy(m => (m.Largura, m.Altura)).Take(12))
            {
                var hz = string.Join(" · ", grupo.Select(m => m.Hz).Distinct().OrderByDescending(h => h).Select(h => $"{h} Hz"));
                var bpp = grupo.First().BitsPerPel != 32 ? $" · {grupo.First().BitsPerPel} bpp" : "";
                sp.Children.Add(new TextBlock { Text = $"•  {grupo.Key.Largura}×{grupo.Key.Altura} — {hz}{bpp}", FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
            }
            var grupos = modos.GroupBy(m => (m.Largura, m.Altura)).Count();
            if (grupos > 12)
                sp.Children.Add(new TextBlock { Text = $"… e mais {grupos - 12} resoluções.", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        }

        sp.Children.Add(new TextBlock
        {
            Text = "O Sevenzin lista os modos, mas não altera a taxa de atualização por você — troque nas configurações de vídeo do Windows ou do driver.",
            Foreground = Muted,
            FontSize = 11,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        return new Border { Style = CardStyle, Margin = new Thickness(0, 6, 0, 0), Child = sp };
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
