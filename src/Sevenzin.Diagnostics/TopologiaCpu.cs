using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Topologia real da CPU via Win32 GetLogicalProcessorInformationEx (só leitura):
/// divisão P/E pela EfficiencyClass (hardware híbrido) e nº de domínios de cache L3 (≈ CCD em AMD).</summary>
public static class TopologiaCpu
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref uint returnedLength);

    const int RelationProcessorCore = 0, RelationCache = 2;

    /// <param name="ECores">núcleos de eficiência; <param name="PCores">de performance (só quando híbrido).
    /// MascaraPCores/MascaraECores: máscara de afinidade (bits dos processadores lógicos) de cada classe.</param>
    public readonly record struct Resultado(bool Hibrido, int ECores, int PCores, int NucleosFisicos, int DominiosL3,
        long MascaraPCores, long MascaraECores);

    public static Resultado? Ler()
    {
        var cores = Consultar(RelationProcessorCore);
        if (cores is null) return null;

        // EfficiencyClass: 0 em CPU homogênea; quando híbrida, maior = P-core, menor = E-core.
        var porClasse = new Dictionary<byte, int>();
        var mascaraClasse = new Dictionary<byte, long>();
        int fisicos = 0;
        foreach (var off in Entradas(cores))
        {
            byte ec = cores[off + 9];
            porClasse[ec] = porClasse.GetValueOrDefault(ec) + 1;
            // máscara do 1º grupo (KAFFINITY em +32). shortcut: ignora grupos >0 (PCs com >64 lógicos), raríssimo em jogos.
            mascaraClasse[ec] = mascaraClasse.GetValueOrDefault(ec) | BitConverter.ToInt64(cores, off + 32);
            fisicos++;
        }

        bool hibrido = porClasse.Count > 1;
        int e = 0, p = 0;
        long pmask, emask;
        if (hibrido)
        {
            byte menor = porClasse.Keys.Min();
            foreach (var kv in porClasse) { if (kv.Key == menor) e += kv.Value; else p += kv.Value; }
            emask = mascaraClasse[menor];
            pmask = porClasse.Keys.Where(k => k != menor).Aggregate(0L, (a, k) => a | mascaraClasse[k]);
        }
        else { pmask = AplicadorPerfil.MascaraTodosNucleos; emask = 0; }

        int dominiosL3 = 0;
        if (Consultar(RelationCache) is { } cache)
            foreach (var off in Entradas(cache)) if (cache[off + 8] == 3) dominiosL3++; // Level == 3

        return new Resultado(hibrido, e, p, fisicos, dominiosL3, pmask, emask);
    }

    static byte[]? Consultar(int relationship)
    {
        uint len = 0;
        GetLogicalProcessorInformationEx(relationship, IntPtr.Zero, ref len); // 1ª chamada: só o tamanho
        if (len == 0) return null;
        var buf = Marshal.AllocHGlobal((int)len);
        try
        {
            if (!GetLogicalProcessorInformationEx(relationship, buf, ref len)) return null;
            var bytes = new byte[len];
            Marshal.Copy(buf, bytes, 0, (int)len);
            return bytes;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    // Cada SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship (4) + Size (4 em +4) + união de tamanho variável.
    static IEnumerable<int> Entradas(byte[] b)
    {
        int off = 0;
        while (off + 8 <= b.Length)
        {
            int size = BitConverter.ToInt32(b, off + 4);
            if (size <= 0 || off + size > b.Length) yield break;
            yield return off;
            off += size;
        }
    }
}
