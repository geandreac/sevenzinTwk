using QRCoder;

namespace Sevenzin.Diagnostics;

/// <summary>Gera o PNG (bytes) de um QR Code. Usa PngByteQRCode do QRCoder — sem System.Drawing, então
/// funciona sem dependência de GDI. Usado para o otpauth:// do 2FA; não registra o conteúdo em lugar nenhum.</summary>
public static class GeradorQr
{
    public static byte[] Png(string texto, int pixelsPorModulo = 6)
    {
        using var gen = new QRCodeGenerator();
        var dados = gen.CreateQrCode(texto, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(dados).GetGraphic(pixelsPorModulo);
    }
}
