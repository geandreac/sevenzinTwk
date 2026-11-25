using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Janela sem moldura (WindowChrome) maximizada do jeito certo: ocupa exatamente a área de trabalho do
/// monitor em que está — sem passar da borda (o Windows soma a moldura invisível e cortava ~8 px de cada lado)
/// e sem cobrir a barra de tarefas, em qualquer lado e em qualquer monitor. Com a barra em "ocultar
/// automaticamente", deixa 2 px livres na borda dela para ela continuar aparecendo. Também reduz o tamanho
/// mínimo quando o monitor é menor que o mínimo de projeto (notebook 1366×768 com escala).</summary>
public static class JanelaMaximizada
{
    public static void Aplicar(Window w)
    {
        double minW = w.MinWidth, minH = w.MinHeight;
        void Ligar()
        {
            var fonte = HwndSource.FromHwnd(new WindowInteropHelper(w).Handle);
            fonte?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado) =>
            {
                if (msg == WM_GETMINMAXINFO && Ajustar(hwnd, lParam, w, minW, minH)) tratado = true;
                return IntPtr.Zero;
            });
        }
        if (new WindowInteropHelper(w).Handle != IntPtr.Zero) Ligar();
        else w.SourceInitialized += (_, _) => Ligar();
    }

    static bool Ajustar(IntPtr hwnd, IntPtr lParam, Window w, double minW, double minH)
    {
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;

        var a = AreaMaximizada.Calcular(R(info.rcMonitor), R(info.rcWork), BordasAutoOcultas(info.rcMonitor));
        var trabalho = new RECT { Left = a.Esq, Top = a.Topo, Right = a.Dir, Bottom = a.Baixo };
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        mmi.ptMaxPosition = new POINT { X = trabalho.Left - info.rcMonitor.Left, Y = trabalho.Top - info.rcMonitor.Top };
        mmi.ptMaxSize = new POINT { X = trabalho.Right - trabalho.Left, Y = trabalho.Bottom - trabalho.Top };
        mmi.ptMaxTrackSize = mmi.ptMaxSize;

        // Mínimo de projeto (980×620 DIP) nunca maior que a área útil deste monitor.
        var escala = PresentationSource.FromVisual(w)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        double maxWDip = mmi.ptMaxSize.X / escala.M11, maxHDip = mmi.ptMaxSize.Y / escala.M22;
        w.MinWidth = Math.Min(minW, maxWDip);
        w.MinHeight = Math.Min(minH, maxHDip);
        mmi.ptMinTrackSize = new POINT { X = (int)Math.Ceiling(w.MinWidth * escala.M11), Y = (int)Math.Ceiling(w.MinHeight * escala.M22) };

        Marshal.StructureToPtr(mmi, lParam, true);
        return true;
    }

    static Retangulo R(RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);

    static (bool, bool, bool, bool) BordasAutoOcultas(RECT monitor)
    {
        bool Tem(uint borda)
        {
            var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), uEdge = borda, rc = monitor };
            try { return SHAppBarMessage(ABM_GETAUTOHIDEBAREX, ref d) != IntPtr.Zero; } catch { return false; }
        }
        return (Tem(ABE_LEFT), Tem(ABE_TOP), Tem(ABE_RIGHT), Tem(ABE_BOTTOM));
    }

    const int WM_GETMINMAXINFO = 0x0024;
    const uint MONITOR_DEFAULTTONEAREST = 2;
    const uint ABM_GETAUTOHIDEBAREX = 0x0B, ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
}
