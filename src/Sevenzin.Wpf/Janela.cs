using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sevenzin.Wpf;

/// <summary>Cantos arredondados nativos via DWM (Windows 11). Não usa transparência: mantém o WindowChrome,
/// a aceleração de hardware e o arrasto/controles. Em Windows 10 o atributo é ignorado (no-op seguro).</summary>
public static class JanelaArredondada
{
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2; // cantos arredondados padrão do Windows 11

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
