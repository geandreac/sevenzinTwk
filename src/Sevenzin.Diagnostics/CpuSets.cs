using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Um conjunto de CPU (CPU Set) do sistema: Id (codifica grupo+índice), grupo, índice lógico
/// e classe de eficiência (maior = P-core em CPU híbrida).</summary>
public readonly record struct ConjuntoCpu(uint Id, ushort Grupo, byte IndiceLogico, byte EfficiencyClass);

/// <summary>CPU Sets (Win32 Get/SetProcessDefaultCpuSets) — paridade com o original (04_funcionalidades §8:
/// CPU Sets para fixar threads nos P-cores). Só leitura + aplicação TEMPORÁRIA por processo (revertida ao
/// fechar o jogo), nunca permanente.</summary>
public static class CpuSets
{
    [DllImport("kernel32.dll")]
    static extern bool GetSystemCpuSetInformation(IntPtr informacao, uint tamanho, ref uint tamanhoDevolvido, IntPtr processo, uint flags);

    [DllImport("kernel32.dll")]
    static extern bool SetProcessDefaultCpuSets(IntPtr processo, uint[]? ids, uint contagem);

    [DllImport("kernel32.dll")]
    static extern bool GetProcessDefaultCpuSets(IntPtr processo, IntPtr buffer, uint tamanho, out uint tamanhoNecessario);

    /// <summary>Parse do buffer do GetSystemCpuSetInformation. Offsets estáveis do winnt.h até
    /// EfficiencyClass: Size(0), Type(4), Id(8), Group(12), LogicalProcessorIndex(14), EfficiencyClass(18).
    /// Função pura (testável).</summary>
    public static List<ConjuntoCpu> Analisar(byte[] dados)
    {
        var lista = new List<ConjuntoCpu>();
        int off = 0;
        while (off + 20 <= dados.Length)
        {
            uint size = BitConverter.ToUInt32(dados, off);
            if (size < 20 || off + size > dados.Length) break;
            lista.Add(new ConjuntoCpu(
                BitConverter.ToUInt32(dados, off + 8),        // Id
                BitConverter.ToUInt16(dados, off + 12),       // Group
                dados[off + 14],                              // LogicalProcessorIndex
                dados[off + 18]));                            // EfficiencyClass
            off += (int)size;
        }
        return lista;
    }

    /// <summary>IDs dos conjuntos de PERFORMANCE (maior EfficiencyClass quando há divisão P/E).
    /// null quando a CPU é homogênea (não há restrição a aplicar). Função pura (testável).</summary>
    public static uint[]? IdsDePerformance(IReadOnlyList<ConjuntoCpu> conjuntos)
    {
        var classes = conjuntos.Select(c => (int)c.EfficiencyClass).Distinct().ToList();
        if (classes.Count <= 1) return null;
        int maior = classes.Max();
        return conjuntos.Where(c => c.EfficiencyClass == maior).Select(c => c.Id).ToArray();
    }

    /// <summary>Conjuntos atuais do sistema. Lista vazia quando a API não responde.</summary>
    public static List<ConjuntoCpu> Listar()
    {
        try
        {
            uint len = 0;
            GetSystemCpuSetInformation(IntPtr.Zero, 0, ref len, IntPtr.Zero, 0);
            if (len == 0) return new List<ConjuntoCpu>();
            var buf = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!GetSystemCpuSetInformation(buf, len, ref len, IntPtr.Zero, 0)) return new List<ConjuntoCpu>();
                var bytes = new byte[len];
                Marshal.Copy(buf, bytes, 0, (int)len);
                return Analisar(bytes);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return new List<ConjuntoCpu>(); }
    }

    /// <summary>Aplica os conjuntos como padrão do processo (todos os threads novos usam esses núcleos).
    /// Contagem 0 = restaura o padrão do sistema.</summary>
    public static bool AplicarAoProcesso(Process p, uint[]? ids) =>
        SetProcessDefaultCpuSets(p.Handle, ids, ids is null ? 0 : (uint)ids.Length);

    /// <summary>Conjuntos padrão atuais do processo (para restaurar depois). null quando a API falha.</summary>
    public static uint[]? Atual(Process p)
    {
        try
        {
            GetProcessDefaultCpuSets(p.Handle, IntPtr.Zero, 0, out uint necessario);
            if (necessario == 0) return Array.Empty<uint>();
            var buf = Marshal.AllocHGlobal((int)necessario);
            try
            {
                if (!GetProcessDefaultCpuSets(p.Handle, buf, necessario, out uint lido) || lido == 0) return Array.Empty<uint>();
                var ids = new uint[lido / sizeof(uint)];
                for (int i = 0; i < ids.Length; i++) ids[i] = unchecked((uint)Marshal.ReadInt32(buf, i * sizeof(uint)));
                return ids;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return null; }
    }
}
