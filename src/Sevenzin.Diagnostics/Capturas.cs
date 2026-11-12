using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Sevenzin.Diagnostics;

/// <summary>Captura de tela SEGURA (§21.2): salva localmente por padrão. Os modos (região/janela/monitor/tela
/// inteira) vieram da engenharia reversa (06_fluxos F11: capture-region/window/monitor). Envio só por ação
/// explícita do usuário com webhook configurado — nunca automático (ver <see cref="EnvioCaptura"/>).</summary>
public static class CapturaTela
{
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int indice);
    const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;
    const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect nativa);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder texto, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Esquerda, Topo, Direita, Baixo; }

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect limites, IntPtr param);

    /// <summary>Uma janela visível do desktop, candidata a captura.</summary>
    public readonly record struct JanelaCaptura(IntPtr Hwnd, string Titulo);

    public static string PastaCapturas =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "capturas");

    public static string NomeArquivo(DateTime agora) => $"captura-{agora:yyyyMMdd-HHmmss-fff}.png";

    /// <summary>Área virtual total (todos os monitores), para o modo "tela inteira".</summary>
    public static Rectangle AreaTelaInteira()
    {
        var r = new Rectangle(GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));
        return r.Width > 0 && r.Height > 0 ? r : new Rectangle(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }

    /// <summary>Limites de cada monitor, na ordem do Windows (o primário costuma ser o primeiro).</summary>
    public static List<Rectangle> Monitores()
    {
        var lista = new List<Rectangle>();
        bool Callback(IntPtr monitor, IntPtr hdc, ref Rect limites, IntPtr param)
        {
            lista.Add(new Rectangle(limites.Esquerda, limites.Topo, limites.Direita - limites.Esquerda, limites.Baixo - limites.Topo));
            return true;
        }
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        if (lista.Count == 0) lista.Add(AreaTelaInteira());
        return lista;
    }

    /// <summary>Janelas visíveis com título e tamanho mínimo útil (candidatas a "capturar janela").</summary>
    public static List<JanelaCaptura> JanelasVisiveis()
    {
        var janelas = new List<JanelaCaptura>();
        EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                if (!GetWindowRect(hwnd, out var r) || r.Direita - r.Esquerda < 50 || r.Baixo - r.Topo < 50) return true;
                int len = GetWindowTextLength(hwnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                if (sb.Length > 0) janelas.Add(new JanelaCaptura(hwnd, sb.ToString()));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return janelas;
    }

    /// <summary>Captura uma área do desktop (modo região ou monitor). Devolve o bitmap em memória.</summary>
    public static Bitmap CapturarArea(Rectangle area)
    {
        var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(area.Left, area.Top, 0, 0, bmp.Size);
        return bmp;
    }

    /// <summary>Captura uma janela pelos seus limites (modo janela). Devolve null se a janela sumiu.</summary>
    public static Bitmap? CapturarJanela(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var r)) return null;
        int w = r.Direita - r.Esquerda, h = r.Baixo - r.Topo;
        if (w <= 0 || h <= 0) return null;
        return CapturarArea(new Rectangle(r.Esquerda, r.Topo, w, h));
    }

    /// <summary>Salva um bitmap como PNG na pasta local e devolve o caminho.</summary>
    public static string Salvar(Bitmap imagem, string? pasta = null)
    {
        var dir = pasta ?? PastaCapturas;
        Directory.CreateDirectory(dir);
        var caminho = Path.Combine(dir, NomeArquivo(DateTime.Now));
        imagem.Save(caminho, ImageFormat.Png);
        return caminho;
    }

    /// <summary>Captura a tela principal (compatibilidade com o fluxo antigo) e salva localmente.</summary>
    public static string CapturarTelaPrincipal(string? pasta = null)
    {
        using var bmp = CapturarArea(new Rectangle(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN)));
        return Salvar(bmp, pasta);
    }
}
