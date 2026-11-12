using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class GeradorQrTests
{
    [Fact]
    public void Png_gera_imagem_png_valida_para_otpauth()
    {
        var png = GeradorQr.Png("otpauth://totp/Sevenzin:teste@x.com?secret=JBSWY3DPEHPK3PXP&issuer=Sevenzin");
        Assert.True(png.Length > 100);
        // assinatura PNG: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
    }
}
