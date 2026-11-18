using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sevenzin.Wpf;

/// <summary>Cantos arredondados nativos via DWM (Windows 11). Não usa transparência: mantém o WindowChrome,
/// a aceleração de hardware e o arrasto/controles. Em Windows 10 o atributo é ignorado (no-op seguro).</summary>
public static class JanelaArredondada
{
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWCP_DONOTROUND = 1;
    const int DWMWCP_ROUND = 2; // cantos arredondados padrão do Windows 11
    const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    /// <summary>Para janela transparente que desenha a própria forma (o cartão do login): sem cantos e sem borda do
    /// DWM. Senão o Windows 11 desenha uma moldura cinza de 1px no retângulo inteiro, inclusive na margem transparente.</summary>
    public static void SemMolduraNativa(Window w)
    {
        static void Aplicar(Window jan)
        {
            var h = new WindowInteropHelper(jan).Handle;
            if (h == IntPtr.Zero) return;
            int canto = DWMWCP_DONOTROUND, borda = DWMWA_COLOR_NONE;
            try
            {
                DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref canto, sizeof(int));
                DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref borda, sizeof(int));
            }
            catch { }
        }
        if (new WindowInteropHelper(w).Handle != IntPtr.Zero) Aplicar(w);
        else w.SourceInitialized += (_, _) => Aplicar(w);
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Arredondar(Window w)
    {
        static void Aplicar(Window jan)
        {
            var h = new WindowInteropHelper(jan).Handle;
            if (h == IntPtr.Zero) return;
            int pref = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); } catch { }
        }
        if (new WindowInteropHelper(w).Handle != IntPtr.Zero) Aplicar(w);
        else w.SourceInitialized += (_, _) => Aplicar(w);
    }
}
