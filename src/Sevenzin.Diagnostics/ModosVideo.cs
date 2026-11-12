using System.Runtime.InteropServices;
using System.Text;

namespace Sevenzin.Diagnostics;

/// <summary>Um modo de vídeo suportado (resolução × taxa de atualização × profundidade de cor).
/// Obtido por <see cref="LeitorModosVideo"/>. Só leitura.</summary>
public readonly record struct ModoVideo(uint Largura, uint Altura, uint Hz, uint BitsPerPel, uint Flags)
{
    public override string ToString() => $"{Largura}×{Altura} @ {Hz} Hz · {BitsPerPel} bpp";
}

/// <summary>Dispositivo de exibição ativo (adaptador/monitor). Só leitura.</summary>
public readonly record struct DispositivoVideo(string Nome, string Descricao);

/// <summary>Enumeração dos modos de vídeo suportados via Win32 <c>EnumDisplaySettingsW</c> (DEVMODE) —
/// paridade com o original (05_metricas §2: dmPelsWidth, dmPelsHeight, dmBitsPerPel, dmDisplayFrequency).
/// Somente leitura: nada aqui altera o modo atual. Devolve lista vazia quando a API falha.</summary>
public static class LeitorModosVideo
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettingsW(string? deviceName, int modeNum, IntPtr devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevicesW(string? device, int index, IntPtr displayDevice, uint flags);

    // ENUM_CURRENT_SETTINGS (-1) pede o modo em uso; EDD_GET_DEVICE_INTERFACE_NAME não é necessário aqui.
    const int EnumCurrentSettings = -1;
    const uint AttachedToDesktop = 0x00000001;

    /// <summary>sizeof(DEVMODE) mínimo aceito pela API (layout clássico do winuser.h até dmDisplayFrequency).</summary>
    const int TamanhoDevmode = 220;
    const int TamanhoDisplayDevice = 840;

    // Offsets do DEVMODE (layout clássico, pouco alinhado):
    const int OffTamanho = 68;            // dmSize (WORD)
    const int OffBitsPorPixel = 168;      // dmBitsPerPel (DWORD)
    const int OffLargura = 172;           // dmPelsWidth (DWORD)
    const int OffAltura = 176;            // dmPelsHeight (DWORD)
    const int OffFlags = 180;             // dmDisplayFlags (DWORD)
    const int OffFrequencia = 184;        // dmDisplayFrequency (DWORD)

    /// <summary>Converte o buffer cru do DEVMODE em <see cref="ModoVideo"/>. Função pura (testável).
    /// Devolve null quando o buffer é pequeno demais, dmSize inconsistente ou o modo é inválido (0×0 @ 0 Hz).</summary>
    public static ModoVideo? AnalisarDevmode(byte[] buffer)
    {
        if (buffer.Length < OffFrequencia + 4) return null;
        ushort tamanho = BitConverter.ToUInt16(buffer, OffTamanho);
        if (tamanho < OffFrequencia + 4) return null; // DEVMODE truncado demais para os campos que lemos
        uint largura = BitConverter.ToUInt32(buffer, OffLargura);
        uint altura = BitConverter.ToUInt32(buffer, OffAltura);
        uint hz = BitConverter.ToUInt32(buffer, OffFrequencia);
        uint bpp = BitConverter.ToUInt32(buffer, OffBitsPorPixel);
        if (largura == 0 || altura == 0 || hz == 0 || bpp == 0) return null;
        return new ModoVideo(largura, altura, hz, bpp, BitConverter.ToUInt32(buffer, OffFlags));
    }

    /// <summary>Dispositivos de exibição ativos (ligados ao desktop), ex.: "\\.\DISPLAY1".</summary>
    public static IReadOnlyList<DispositivoVideo> Dispositivos()
    {
        var lista = new List<DispositivoVideo>();
        try
        {
            var buf = new byte[TamanhoDisplayDevice];
            for (int i = 0; ; i++)
            {
                Array.Clear(buf);
                BitConverter.GetBytes((uint)TamanhoDisplayDevice).CopyTo(buf, 0); // cb
                IntPtr p = Marshal.AllocHGlobal(buf.Length);
                try
                {
                    Marshal.Copy(buf, 0, p, buf.Length);
                    if (!EnumDisplayDevicesW(null, i, p, 0)) break;
                    Marshal.Copy(p, buf, 0, buf.Length);
                }
                finally { Marshal.FreeHGlobal(p); }
                uint state = BitConverter.ToUInt32(buf, 324);
                if ((state & AttachedToDesktop) == 0) continue;
                lista.Add(new DispositivoVideo(LerTexto(buf, 4, 32), LerTexto(buf, 68, 128)));
            }
        }
        catch { /* sem dispositivos enumeráveis */ }
        return lista;
    }

    /// <summary>Modos suportados pelo dispositivo (null = exibição primária), ordenados por resolução e Hz.
    /// Lista vazia quando a API não responde — nunca valores inventados.</summary>
    public static IReadOnlyList<ModoVideo> Listar(string? deviceName = null)
    {
        var modos = new List<ModoVideo>();
        try
        {
            for (int i = 0; ; i++)
            {
                var buf = new byte[TamanhoDevmode];
                BitConverter.GetBytes((ushort)TamanhoDevmode).CopyTo(buf, OffTamanho); // dmSize
                IntPtr p = Marshal.AllocHGlobal(buf.Length);
                bool ok;
                try
                {
                    Marshal.Copy(buf, 0, p, buf.Length);
                    ok = EnumDisplaySettingsW(deviceName, i, p);
                    if (ok) Marshal.Copy(p, buf, 0, buf.Length);
                }
                finally { Marshal.FreeHGlobal(p); }
                if (!ok) break;
                if (AnalisarDevmode(buf) is { } m) modos.Add(m);
            }
        }
        catch { /* enumeração indisponível */ }
        return Consolidar(modos);
    }

    /// <summary>Modo em uso agora (ENUM_CURRENT_SETTINGS), ou null quando a API não responde.</summary>
    public static ModoVideo? Atual(string? deviceName = null)
    {
        try
        {
            var buf = new byte[TamanhoDevmode];
            BitConverter.GetBytes((ushort)TamanhoDevmode).CopyTo(buf, OffTamanho);
            IntPtr p = Marshal.AllocHGlobal(buf.Length);
            try
            {
                Marshal.Copy(buf, 0, p, buf.Length);
                if (!EnumDisplaySettingsW(deviceName, EnumCurrentSettings, p)) return null;
                Marshal.Copy(p, buf, 0, buf.Length);
            }
            finally { Marshal.FreeHGlobal(p); }
            return AnalisarDevmode(buf);
        }
        catch { return null; }
    }

    /// <summary>Remove duplicatas e ordena: resolução decrescente, depois Hz decrescente. Função pura (testável).</summary>
    public static List<ModoVideo> Consolidar(IEnumerable<ModoVideo> modos) =>
        modos.Distinct()
            .OrderByDescending(m => m.Largura).ThenByDescending(m => m.Altura).ThenByDescending(m => m.Hz)
            .ToList();

    static string LerTexto(byte[] b, int offset, int chars)
    {
        var s = Encoding.Unicode.GetString(b, offset, chars * 2);
        int fim = s.IndexOf('\0');
        return (fim >= 0 ? s[..fim] : s).Trim();
    }
}
