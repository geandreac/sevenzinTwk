using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sevenzin.Wpf;

/// <summary>
/// Ícone nativo na Área de Notificação (System Tray) do Windows via Win32 `Shell_NotifyIconW` + `HwndSource`.
/// Zero dependência de Windows Forms; suporta restaurar ao clicar e menu de contexto nativo (Abrir / Encerrar).
/// </summary>
public sealed class BandejaSistema : IDisposable
{
    const uint NIM_ADD = 0x00000000;
    const uint NIM_MODIFY = 0x00000001;
    const uint NIM_DELETE = 0x00000002;
    const uint NIF_MESSAGE = 0x00000001;
    const uint NIF_ICON = 0x00000002;
    const uint NIF_TIP = 0x00000004;

    const int WM_USER = 0x0400;
    const int WM_TRAYICON = WM_USER + 0x77;
    const int WM_LBUTTONUP = 0x0202;
    const int WM_LBUTTONDBLCLK = 0x0203;
    const int WM_RBUTTONUP = 0x0205;
    const int WM_CONTEXTMENU = 0x007B;

    const uint MF_STRING = 0x00000000;
    const uint MF_SEPARATOR = 0x00000800;
    const uint TPM_RIGHTBUTTON = 0x0002;
    const uint TPM_RETURNCMD = 0x0100;
    const uint TPM_BOTTOMALIGN = 0x0020;

    const int CMD_ABRIR = 1001;
    const int CMD_CONFIG = 1002;
    const int CMD_SAIR = 1003;

    readonly Action _aoRestaurar;
    readonly Action _aoAbrirConfig;
    readonly Action _aoSair;
    HwndSource? _hwndSource;
    IntPtr _hIcon;
    bool _adicionado;
    bool _disposed;

    public BandejaSistema(Action aoRestaurar, Action aoAbrirConfig, Action aoSair)
    {
        _aoRestaurar = aoRestaurar;
        _aoAbrirConfig = aoAbrirConfig;
        _aoSair = aoSair;
    }

    public void Exibir()
    {
        if (_disposed || App.ModoRender) return;
        GarantirJanelaMensagem();
        if (_hwndSource is null) return;

        if (_hIcon == IntPtr.Zero)
            _hIcon = CarregarIconeExecutavel();

        var data = CriarEstruturaDados();
        if (!_adicionado)
            _adicionado = Shell_NotifyIconW(NIM_ADD, ref data);
        else
            Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void Ocultar()
    {
        if (!_adicionado || _hwndSource is null) return;
        var data = CriarEstruturaDados();
        Shell_NotifyIconW(NIM_DELETE, ref data);
        _adicionado = false;
    }

    NOTIFYICONDATAW CriarEstruturaDados() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwndSource?.Handle ?? IntPtr.Zero,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = WM_TRAYICON,
        hIcon = _hIcon,
        szTip = "SevenzinTwks · Clique para abrir",
    };

    void GarantirJanelaMensagem()
    {
        if (_hwndSource is not null) return;
        var p = new HwndSourceParameters("SevenzinTrayHost")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _hwndSource = new HwndSource(p);
        _hwndSource.AddHook(WndProc);
    }

    static IntPtr CarregarIconeExecutavel()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                ushort idx = 0;
                var h = ExtractAssociatedIconW(IntPtr.Zero, exe, ref idx);
                if (h != IntPtr.Zero) return h;
            }
        }
        catch { }
        return LoadIconW(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION fallback
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int evento = unchecked((short)(lParam.ToInt64() & 0xFFFF));
            if (evento is WM_LBUTTONUP or WM_LBUTTONDBLCLK)
            {
                handled = true;
                Application.Current?.Dispatcher.BeginInvoke(_aoRestaurar);
            }
            else if (evento is WM_RBUTTONUP or WM_CONTEXTMENU)
            {
                handled = true;
                MostrarMenuContexto(hwnd);
            }
        }
        return IntPtr.Zero;
    }

    void MostrarMenuContexto(IntPtr hwnd)
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;
        try
        {
            AppendMenuW(hMenu, MF_STRING, new UIntPtr(CMD_ABRIR), "Abrir SevenzinTwks");
            AppendMenuW(hMenu, MF_STRING, new UIntPtr(CMD_CONFIG), "Configurações");
            AppendMenuW(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(hMenu, MF_STRING, new UIntPtr(CMD_SAIR), "Encerrar completamente");

            GetCursorPos(out var pt);
            SetForegroundWindow(hwnd);
            int cmd = TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, pt.X, pt.Y, hwnd, IntPtr.Zero);
            if (cmd == CMD_ABRIR)
                Application.Current?.Dispatcher.BeginInvoke(_aoRestaurar);
            else if (cmd == CMD_CONFIG)
                Application.Current?.Dispatcher.BeginInvoke(_aoAbrirConfig);
            else if (cmd == CMD_SAIR)
                Application.Current?.Dispatcher.BeginInvoke(_aoSair);
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Ocultar();
        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, string lpIconPath, ref ushort lpiIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    static extern int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
}
