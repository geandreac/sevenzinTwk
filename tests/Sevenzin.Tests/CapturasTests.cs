using System.Drawing;
using System.Drawing.Imaging;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class CapturasTests
{
    [Fact]
    public void NomeArquivo_tem_prefixo_data_e_extensao_png()
    {
        var nome = CapturaTela.NomeArquivo(new DateTime(2026, 11, 11, 14, 5, 9));
        Assert.StartsWith("captura-20261111-140509", nome);
        Assert.EndsWith(".png", nome);
    }

    [Fact]
    public void PastaCapturas_fica_no_appdata_local_do_sevenzin()
    {
        Assert.EndsWith(Path.Combine("Sevenzin", "capturas"), CapturaTela.PastaCapturas);
    }

    [Fact]
    public void Salvar_grava_um_png_valido_na_pasta_local()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "sevenzin-cap-" + Guid.NewGuid().ToString("N"));
        using var bmp = new Bitmap(8, 6, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.SteelBlue);

        var caminho = CapturaTela.Salvar(bmp, pasta);
        try
        {
            Assert.True(File.Exists(caminho));
            Assert.Equal(pasta, Path.GetDirectoryName(caminho));
            using var lida = Image.FromFile(caminho); // relê: PNG válido
            Assert.Equal(8, lida.Width);
            Assert.Equal(6, lida.Height);
            Assert.Equal(ImageFormat.Png, lida.RawFormat);
        }
        finally { try { Directory.Delete(pasta, true); } catch { } }
    }
}
