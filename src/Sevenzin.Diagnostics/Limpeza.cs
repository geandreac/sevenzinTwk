using System.IO;
using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

public enum CategoriaLimpeza { TempUsuario, CacheMiniaturas, Lixeira }

/// <summary>Uma categoria de limpeza. <see cref="Permanente"/> só é verdadeiro para a Lixeira (esvaziar é
/// irreversível); as demais vão PARA a Lixeira e podem ser restauradas.</summary>
public sealed record AlvoLimpeza(CategoriaLimpeza Id, string Nome, string Descricao, bool Permanente);

public sealed record ResultadoAnalise(long Bytes, int Itens);
public sealed record ResultadoLimpeza(int Itens, long Bytes, int Erros, string Mensagem);

/// <summary>Limpeza segura (reconstrução): só locais temporários/cache conhecidos, NENHUM arquivo pessoal.
/// Limpar = enviar para a Lixeira (SHFileOperation com FOF_ALLOWUNDO) — reversível; a ÚNICA ação permanente
/// é esvaziar a Lixeira (SHEmptyRecycleBin), que a UI confirma à parte. Defesa em profundidade: todo caminho
/// a remover precisa estar dentro de uma raiz permitida (<see cref="DentroDe"/>) e não é um reparse point
/// (junction/symlink), para nunca sair da pasta segura. Sem admin na v1 (só áreas do usuário).</summary>
public static class Limpeza
{
    public static readonly IReadOnlyList<AlvoLimpeza> Alvos = new[]
    {
        new AlvoLimpeza(CategoriaLimpeza.TempUsuario, "Arquivos temporários do usuário",
            "Conteúdo de %TEMP% (cache de instaladores e apps). Vai para a Lixeira; arquivos em uso são pulados.", false),
        new AlvoLimpeza(CategoriaLimpeza.CacheMiniaturas, "Cache de miniaturas e ícones",
            "thumbcache_*.db / iconcache_*.db do Explorer. O Windows regenera sozinho quando precisar.", false),
        new AlvoLimpeza(CategoriaLimpeza.Lixeira, "Lixeira (Reciclagem)",
            "Esvaziar remove de vez o que está na Lixeira. PERMANENTE — não dá para desfazer.", true),
    };

    // ---- Análise (somente leitura) ----

    public static ResultadoAnalise Analisar(CategoriaLimpeza cat) => cat switch
    {
        CategoriaLimpeza.Lixeira => LixeiraInfo(),
        CategoriaLimpeza.CacheMiniaturas => AnalisarArquivos(ArquivosCache()),
        _ => AnalisarPasta(Path.GetTempPath()),
    };

    static ResultadoAnalise AnalisarPasta(string dir)
    {
        var topo = EntradasTopo(dir);
        long b = 0;
        foreach (var p in topo) b += TamanhoDe(p);
        return new(b, topo.Count);
    }

    static ResultadoAnalise AnalisarArquivos(IEnumerable<string> arqs)
    {
        long b = 0; int n = 0;
        foreach (var a in arqs) { var l = SafeLen(a); if (l >= 0) { b += l; n++; } }
        return new(b, n);
    }

    // ---- Limpeza ----

    public static ResultadoLimpeza Limpar(CategoriaLimpeza cat) => cat switch
    {
        CategoriaLimpeza.Lixeira => EsvaziarLixeira(),
        CategoriaLimpeza.CacheMiniaturas => MoverParaLixeira(ArquivosCache().ToList()),
        _ => MoverParaLixeira(EntradasTopo(Path.GetTempPath())),
    };

    static ResultadoLimpeza MoverParaLixeira(List<string> alvos)
    {
        int itens = 0, erros = 0; long bytes = 0;
        foreach (var p in alvos)
        {
            if (!DentroDeRaizPermitida(p)) { erros++; continue; } // guarda: nunca fora da raiz segura
            long tam = TamanhoDe(p);
            if (EnviarParaLixeira(p)) { itens++; bytes += tam; } else erros++;
        }
        var msg = erros > 0
            ? $"{itens} item(ns) enviado(s) à Lixeira; {erros} em uso/ignorado(s)."
            : $"{itens} item(ns) enviado(s) à Lixeira.";
        return new(itens, bytes, erros, msg);
    }

    static ResultadoLimpeza EsvaziarLixeira()
    {
        var antes = LixeiraInfo();
        try
        {
            int r = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            return r == 0
                ? new(antes.Itens, antes.Bytes, 0, $"Lixeira esvaziada ({antes.Itens} item(ns)).")
                : new(0, 0, 1, "Não foi possível esvaziar a Lixeira.");
        }
        catch (Exception e) { return new(0, 0, 1, e.Message); }
    }

    // ---- Guarda de segurança (pura, testável) ----

    /// <summary>True se <paramref name="caminho"/> (resolvido, com ".." colapsado) é uma das raízes ou está
    /// dentro dela. Base para recusar qualquer alvo fora das áreas seguras.</summary>
    public static bool DentroDe(string caminho, IEnumerable<string> raizes)
    {
        var full = NormPath(caminho);
        if (full.Length == 0) return false;
        foreach (var r0 in raizes)
        {
            var r = NormPath(r0);
            if (r.Length == 0) continue;
            if (full.Equals(r, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static bool DentroDeRaizPermitida(string caminho) => DentroDe(caminho, RaizesPermitidas());
    static string[] RaizesPermitidas() => new[] { Path.GetTempPath(), ExplorerDir() };

    static string NormPath(string p)
    {
        try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return ""; }
    }

    // ---- Enumeração segura (pura/testável onde dá) ----

    /// <summary>Tamanho recursivo de uma pasta, pulando reparse points (junction/symlink) e erros por item.</summary>
    public static long TamanhoPasta(string dir)
    {
        long total = 0;
        try
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists || di.Attributes.HasFlag(FileAttributes.ReparsePoint)) return 0;
            foreach (var f in di.EnumerateFiles()) { try { total += f.Length; } catch { } }
            foreach (var sub in di.EnumerateDirectories())
            {
                try { if (!sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) total += TamanhoPasta(sub.FullName); }
                catch { }
            }
        }
        catch { }
        return total;
    }

    static List<string> EntradasTopo(string dir)
    {
        var r = new List<string>();
        try
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists) return r;
            foreach (var e in di.EnumerateFileSystemInfos())
                try { if (!e.Attributes.HasFlag(FileAttributes.ReparsePoint)) r.Add(e.FullName); } catch { }
        }
        catch { }
        return r;
    }

    static long TamanhoDe(string p) => Directory.Exists(p) ? TamanhoPasta(p) : (File.Exists(p) ? Math.Max(0, SafeLen(p)) : 0);
    static long SafeLen(string f) { try { return new FileInfo(f).Length; } catch { return -1; } }

    static string ExplorerDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");

    static IEnumerable<string> ArquivosCache()
    {
        var dir = ExplorerDir();
        if (!Directory.Exists(dir)) yield break;
        foreach (var pat in new[] { "thumbcache_*.db", "iconcache_*.db" })
        {
            string[] achados;
            try { achados = Directory.GetFiles(dir, pat); } catch { achados = Array.Empty<string>(); }
            foreach (var f in achados) yield return f;
        }
    }

    // ---- P/Invoke (Shell) ----

    static bool EnviarParaLixeira(string caminho)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = caminho + "\0\0", // lista terminada por duplo nulo
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT),
        };
        try { return SHFileOperation(ref op) == 0 && op.fAnyOperationsAborted == 0; }
        catch { return false; }
    }

    static ResultadoAnalise LixeiraInfo()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        try { if (SHQueryRecycleBin(null, ref info) == 0) return new(info.i64Size, (int)info.i64NumItems); }
        catch { }
        return new(0, 0);
    }

    const uint FO_DELETE = 0x0003;
    const ushort FOF_SILENT = 0x0004, FOF_NOCONFIRMATION = 0x0010, FOF_ALLOWUNDO = 0x0040, FOF_NOERRORUI = 0x0400;
    const uint SHERB_NOCONFIRMATION = 0x1, SHERB_NOPROGRESSUI = 0x2, SHERB_NOSOUND = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
