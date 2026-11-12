using System.Security.Cryptography;
using System.Text;

namespace Sevenzin.Diagnostics;

/// <summary>TOTP (RFC 6238, HMAC-SHA1, 30 s, 6 dígitos) — verificação em dois fatores LOCAL, substituindo o
/// esquema de licença/2FA por servidor do original (KeyAuth_Enable2FA/GetTwoFactorSecret, 04_funcionalidades §1)
/// por algo 100% offline e verificável. O segredo é guardado na conta local (nunca em texto fora do arquivo).</summary>
public static class Totp
{
    const int Passo = 30;       // segundos por janela (RFC 6238)
    const int Digitos = 6;      // comprimento do código

    /// <summary>Gera um segredo novo (20 bytes aleatórios em Base32, sem '=' de preenchimento).</summary>
    public static string GerarSegredo()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32(bytes);
    }

    /// <summary>Base32 RFC 4648 (alfabeto A-Z2-7, sem padding). Função pura (testável).</summary>
    public static string Base32(byte[] dados)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(alfabeto[(buffer >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(alfabeto[(buffer << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    static byte[] DecodificarBase32(string segredo)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var s = (segredo ?? "").ToUpperInvariant().Replace("=", "").Replace(" ", "");
        var saida = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in s)
        {
            int v = alfabeto.IndexOf(c);
            if (v < 0) continue;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                saida.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return saida.ToArray();
    }

    /// <summary>Calcula o código TOTP do instante. Função pura (testável com vetores RFC 6238).</summary>
    public static string Calcular(string segredo, DateTime utc)
    {
        long unix = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        long contador = unix / Passo; // Unix time: o RFC 6238 conta a partir de 1970-01-01
        var msg = new byte[8];
        for (int i = 7; i >= 0; i--) { msg[i] = (byte)(contador & 0xFF); contador >>= 8; }

        using var hmac = new HMACSHA1(DecodificarBase32(segredo));
        var hash = hmac.ComputeHash(msg);
        int offset = hash[^1] & 0x0F;
        int binario = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binario % 1_000_000).ToString("D6");
    }

    /// <summary>Confere o código com tolerância de ±1 janela (relógios ligeiramente dessincronizados).</summary>
    public static bool Confere(string segredo, string? codigo, DateTime utc)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        var esperado = Calcular(segredo, utc);
        if (ComparacaoConstante(esperado, codigo.Trim())) return true;
        return ComparacaoConstante(Calcular(segredo, utc.AddSeconds(-Passo)), codigo.Trim())
            || ComparacaoConstante(Calcular(segredo, utc.AddSeconds(Passo)), codigo.Trim());
    }

    /// <summary>Comparação em tempo constante (evita timing attack no código de 6 dígitos).</summary>
    static bool ComparacaoConstante(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
