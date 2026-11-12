using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Sevenzin.Wpf;

/// <summary>Cena de benchmark reproduzível (igual à do WinForms, via System.Drawing): desenha um trabalho
/// fixo por quadro num bitmap fora da tela. Sem afinidade de thread de UI, roda em segundo plano.</summary>
public sealed class CenaBenchmark : IDisposable
{
    readonly Bitmap _bmp = new(1280, 720, PixelFormat.Format32bppPArgb);
    readonly Graphics _g;
    readonly Pen _pen = new(Color.FromArgb(120, 100, 180, 255));
    readonly SolidBrush _brush = new(Color.White);
    int _f;

    public CenaBenchmark()
    {
        _g = Graphics.FromImage(_bmp);
        _g.SmoothingMode = SmoothingMode.AntiAlias;
    }

    public void Quadro()
    {
        int f = ++_f;
        _g.Clear(Color.FromArgb(0x14, 0x16, 0x1C));
        for (int i = 0; i < 400; i++)
        {
            _brush.Color = Color.FromArgb(90, (i * 7) % 255, (i * 13) % 255, (i * 17) % 255);
            _g.FillEllipse(_brush, (i * 37 + f) % 1280, (i * 53 + f) % 720, 40, 40);
        }
        for (int i = 0; i < 200; i++)
            _g.DrawLine(_pen, (i * 11 + f) % 1280, 0, (i * 23) % 1280, 720);
    }

    public void Dispose() { _g.Dispose(); _bmp.Dispose(); _pen.Dispose(); _brush.Dispose(); }
}
