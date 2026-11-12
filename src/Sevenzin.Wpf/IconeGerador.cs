using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sevenzin.Wpf;

/// <summary>Gera o ícone do app (.ico multi-tamanho) a partir do asset oficial "S em raio" (geometria ic_logo),
/// preservando proporção e cor (#2F8CFF). Usado via `--makeicon saida.ico`. Frames em PNG (suportado desde o Vista).</summary>
static class IconeGerador
{
    public static void Gerar(string saida, int[] tamanhos)
    {
        var frames = tamanhos.Select(RenderPng).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saida))!);
        using var fs = File.Create(saida);
        using var bw = new BinaryWriter(fs);
        bw.Write((short)0); bw.Write((short)1); bw.Write((short)frames.Length); // ICONDIR: reservado, tipo=1, nº
        int offset = 6 + 16 * frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            int s = tamanhos[i];
            bw.Write((byte)(s >= 256 ? 0 : s)); // largura (0 = 256)
            bw.Write((byte)(s >= 256 ? 0 : s)); // altura
            bw.Write((byte)0);  // paleta
            bw.Write((byte)0);  // reservado
            bw.Write((short)1); // planos
            bw.Write((short)32);// bits por pixel
            bw.Write(frames[i].Length);
            bw.Write(offset);
            offset += frames[i].Length;
        }
        foreach (var f in frames) bw.Write(f);
    }

    static byte[] RenderPng(int s)
    {
        var geo = (Geometry)Application.Current.Resources["ic_logo"];
        var brush = (Brush)Application.Current.Resources["Brand500Brush"];
        var path = new System.Windows.Shapes.Path { Data = geo, Fill = brush, Stretch = Stretch.Uniform };
        var host = new Grid { Width = s, Height = s, Background = Brushes.Transparent };
        host.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(s * 0.18), Child = path });
        host.Measure(new Size(s, s));
        host.Arrange(new Rect(0, 0, s, s));
        host.UpdateLayout();
        var rtb = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(host);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
