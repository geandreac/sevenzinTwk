using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sevenzin.Wpf;

/// <summary>Render headless da janela para PNG (`--render saida.png`): mostra a MainWindow fora da tela,
/// espera a UI assentar e salva um PNG. Serve para inspeção visual durante o desenvolvimento.</summary>
static class Renderizador
{
    public static void Rodar(Application app, string saida, string? tag = null)
    {
        if (tag == "login")
        {
            var lw = new LoginWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -4000, Top = -4000, ShowInTaskbar = false };
            lw.Show();
            RenderizarEm(app, lw, saida);
            return;
        }

        var win = new MainWindow
        {
            Width = 1280,
            Height = 760,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000,
            ShowInTaskbar = false,
        };
        win.Show();
        if (tag is not null) win.SelecionarAba(tag);
        RenderizarEm(app, win, saida);
    }

    static void RenderizarEm(Application app, Window win, string saida)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                win.UpdateLayout();
                int w = (int)Math.Ceiling(win.ActualWidth), h = (int)Math.Ceiling(win.ActualHeight);
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(win);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(saida);
                enc.Save(fs);
            }
            catch { }
            app.Shutdown(0);
        };
        timer.Start();
    }
}
