using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Topologia real da CPU via Win32 GetLogicalProcessorInformationEx (só leitura):
/// divisão P/E pela EfficiencyClass (hardware híbrido), máscaras por núcleo físico (Core 0 / SMT)
/// e domínios de cache L3 (≈ CCD em processadores AMD multi-CCD).</summary>
public static class TopologiaCpu
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref uint returnedLength);

    const int RelationProcessorCore = 0, RelationCache = 2;

    /// <param name="ECores">núcleos de eficiência; <param name="PCores">de performance (só quando híbrido).
    /// MascaraPCores/MascaraECores: máscara de afinidade (bits dos processadores lógicos) de cada classe.
    /// MascaraSemCore0 / MascaraSemSmt / MascaraPrimeiroCcd: máscaras calculadas a partir da topologia física real.</param>
    public readonly record struct Resultado(
        bool Hibrido,
        int ECores,
        int PCores,
        int NucleosFisicos,
        int DominiosL3,
        long MascaraPCores,
        long MascaraECores,
        long MascaraSemCore0 = 0,
        long MascaraSemSmt = 0,
        long MascaraPrimeiroCcd = 0);

    /// <summary>Remove todos os threads lógicos pertencentes ao primeiro núcleo físico (Core 0). Puro/testável.</summary>
    public static long CalcularMascaraSemCore0(IReadOnlyList<long> mascarasPorNucleoFisico, long mascaraTotal)
    {
        if (mascarasPorNucleoFisico.Count <= 1) return mascaraTotal;
        long sem0 = mascaraTotal & ~mascarasPorNucleoFisico[0];
        return sem0 != 0 ? sem0 : mascaraTotal;
    }

    /// <summary>Mantém apenas o 1º thread lógico de cada núcleo físico (desativa SMT/Hyper-Threading por afinidade). Puro/testável.</summary>
    public static long CalcularMascaraSemSmt(IReadOnlyList<long> mascarasPorNucleoFisico, long mascaraFallback = 0)
    {
        if (mascarasPorNucleoFisico.Count == 0) return mascaraFallback;
        long acumulada = 0;
        foreach (var m in mascarasPorNucleoFisico)
        {
            if (m == 0) continue;
            long primeiroBit = m & -m; // isola o bit menos significativo do núcleo físico
            acumulada |= primeiroBit;
        }
        return acumulada != 0 ? acumulada : mascaraFallback;
    }

    /// <summary>Seleciona a máscara do 1º domínio de cache L3 (1º CCD em CPUs multi-CCD). Puro/testável.</summary>
    public static long CalcularMascaraPrimeiroCcd(IReadOnlyList<long> mascarasCachesL3, long mascaraTotal)
    {
        if (mascarasCachesL3.Count <= 1) return mascaraTotal;
        long ccd0 = mascarasCachesL3[0] & mascaraTotal;
        return ccd0 != 0 ? ccd0 : mascaraTotal;
    }

    /// <summary>Compõe a máscara final de afinidade do jogo combinando base + filtros de topologia, sem nunca zerar os núcleos.</summary>
    public static long CombinarMascaraJogo(
        long mascaraBase,
        Resultado? topo,
        bool excluirCore0,
        bool desativarSmt,
        bool apenasUmCcd,
        long? mascaraTodosOverride = null)
    {
        long todos = mascaraTodosOverride ?? AplicadorPerfil.MascaraTodosNucleos;
        long atual = mascaraBase == 0 ? todos : (mascaraBase & todos);
        if (atual == 0) atual = todos;
        if (topo is not { } t) return atual;

        if (apenasUmCcd && t.MascaraPrimeiroCcd != 0)
        {
            long tentativa = atual & t.MascaraPrimeiroCcd;
            if (tentativa != 0) atual = tentativa;
        }
        if (desativarSmt && t.MascaraSemSmt != 0)
        {
            long tentativa = atual & t.MascaraSemSmt;
            if (tentativa != 0) atual = tentativa;
        }
        if (excluirCore0 && t.MascaraSemCore0 != 0)
        {
            long tentativa = atual & t.MascaraSemCore0;
            if (tentativa != 0) atual = tentativa;
        }
        return atual != 0 ? atual : todos;
    }

    public static Resultado? Ler()
    {
        var cores = Consultar(RelationProcessorCore);
        if (cores is null) return null;

        var porClasse = new Dictionary<byte, int>();
        var mascaraClasse = new Dictionary<byte, long>();
        var mascarasFisicas = new List<long>();
        int fisicos = 0;

        foreach (var off in Entradas(cores))
        {
            byte ec = cores[off + 9];
            porClasse[ec] = porClasse.GetValueOrDefault(ec) + 1;
            long maskCore = BitConverter.ToInt64(cores, off + 32);
            mascaraClasse[ec] = mascaraClasse.GetValueOrDefault(ec) | maskCore;
            mascarasFisicas.Add(maskCore);
            fisicos++;
        }

        long todos = AplicadorPerfil.MascaraTodosNucleos;
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
        else { pmask = todos; emask = 0; }

        int dominiosL3 = 0;
        var mascarasL3 = new List<long>();
        if (Consultar(RelationCache) is { } cache)
        {
            foreach (var off in Entradas(cache))
            {
                int size = BitConverter.ToInt32(cache, off + 4);
                if (cache[off + 8] == 3) // Level == 3
                {
                    dominiosL3++;
                    if (size >= 48)
                    {
                        long maskL3 = BitConverter.ToInt64(cache, off + 40);
                        if (maskL3 != 0) mascarasL3.Add(maskL3);
                    }
                }
            }
        }

        long semCore0 = CalcularMascaraSemCore0(mascarasFisicas, todos);
        long semSmt = CalcularMascaraSemSmt(mascarasFisicas, todos);
        long ccd0 = CalcularMascaraPrimeiroCcd(mascarasL3, todos);

        return new Resultado(hibrido, e, p, fisicos, dominiosL3, pmask, emask, semCore0, semSmt, ccd0);
    }

    static byte[]? Consultar(int relationship)
    {
        uint len = 0;
        GetLogicalProcessorInformationEx(relationship, IntPtr.Zero, ref len);
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
