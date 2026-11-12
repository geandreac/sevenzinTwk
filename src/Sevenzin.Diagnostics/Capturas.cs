using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Captura de tela SEGURA (§21.2): salva localmente, só para você. Nunca envia a lugar nenhum
/// (sem rede, sem webhook) — ao contrário do recurso homônimo do binário original.</summary>
public static class CapturaTela
{
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int indice);
    const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;

    public static string PastaCapturas =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "capturas");

    public static string NomeArquivo(DateTime agora) => $"captura-{agora:yyyyMMdd-HHmmss-fff}.png";

    /// <summary>Salva um bitmap como PNG na pasta local e devolve o caminho.</summary>
    public static string Salvar(Bitmap imagem, string? pasta = null)
    {
        var dir = pasta ?? PastaCapturas;
        Directory.CreateDirectory(dir);
        var caminho = Path.Combine(dir, NomeArquivo(DateTime.Now));
        imagem.Save(caminho, ImageFormat.Png);
        return caminho;
    }

    /// <summary>Captura a tela principal e salva localmente. Devolve o caminho do PNG.</summary>
    public static string CapturarTelaPrincipal(string? pasta = null)
    {
        int w = GetSystemMetrics(SM_CXSCREEN), h = GetSystemMetrics(SM_CYSCREEN);
        if (w <= 0 || h <= 0) { w = 1920; h = 1080; } // fallback sem display
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
        return Salvar(bmp, pasta);
    }
}
