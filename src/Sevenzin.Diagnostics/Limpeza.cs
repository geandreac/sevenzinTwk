using System.IO;
using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

public enum CategoriaLimpeza { TempUsuario, CacheMiniaturas, Lixeira, TempSistema, CacheWindowsUpdate, Prefetch }

/// <summary>Uma categoria de limpeza. <see cref="Permanente"/> só é verdadeiro para a Lixeira (esvaziar é
/// irreversível); as demais vão PARA a Lixeira e podem ser restauradas. <see cref="ExigeAdmin"/> marca os
/// alvos de sistema (HKLM-equivalente em disco): pastas de <c>%SystemRoot%</c> que só limpam como admin.</summary>
/// <see cref="MarcadoPorPadrao"/> = falso para o que limpar costuma piorar o desempenho (o usuário marca se quiser).
public sealed record AlvoLimpeza(CategoriaLimpeza Id, string Nome, string Descricao, bool Permanente, bool ExigeAdmin = false, bool MarcadoPorPadrao = true);

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
            "Conteúdo de %TEMP% (cache de instaladores e apps). Na Limpeza Total, é deletado direto do PC; arquivos em uso são pulados.", false),
        new AlvoLimpeza(CategoriaLimpeza.CacheMiniaturas, "Cache de miniaturas e ícones",
            "thumbcache_*.db / iconcache_*.db do Explorer. O Windows regenera sozinho quando precisar.", false),
        new AlvoLimpeza(CategoriaLimpeza.Lixeira, "Lixeira (Reciclagem)",
            "Remove definitivamente tudo o que já está na Lixeira do Windows.", true),
        new AlvoLimpeza(CategoriaLimpeza.TempSistema, "Arquivos temporários do sistema",
            "Conteúdo de %SystemRoot%\\Temp. Na Limpeza Total, é deletado direto do PC; arquivos em uso são pulados.", false, ExigeAdmin: true),
        new AlvoLimpeza(CategoriaLimpeza.CacheWindowsUpdate, "Cache do Windows Update",
            "Downloads antigos em SoftwareDistribution\\Download. O Windows Update rebaixa o que precisar.", false, ExigeAdmin: true),
        new AlvoLimpeza(CategoriaLimpeza.Prefetch, "Prefetch",
            "Arquivos *.pf de %SystemRoot%\\Prefetch. Não recomendado: o Windows usa esses arquivos para abrir programas e jogos mais rápido; depois de apagados, as próximas aberturas ficam mais lentas até ele refazê-los. Por isso vem desmarcado.", false, ExigeAdmin: true, MarcadoPorPadrao: false),
    };

    // ---- Análise (somente leitura) ----

    public static ResultadoAnalise Analisar(CategoriaLimpeza cat) => cat switch
    {
        CategoriaLimpeza.Lixeira => LixeiraInfo(),
        CategoriaLimpeza.CacheMiniaturas => AnalisarArquivos(ArquivosCache()),
        _ => AnalisarPasta(PastaDe(cat)),
    };

    /// <summary>Pasta resolvida de uma categoria baseada em diretório (todas menos Lixeira e miniaturas).</summary>
    static string PastaDe(CategoriaLimpeza cat) => cat switch
    {
        CategoriaLimpeza.TempSistema => Path.Combine(WindowsDir(), "Temp"),
        CategoriaLimpeza.CacheWindowsUpdate => Path.Combine(WindowsDir(), "SoftwareDistribution", "Download"),
        CategoriaLimpeza.Prefetch => Path.Combine(WindowsDir(), "Prefetch"),
        _ => Path.GetTempPath(),
    };

    static string WindowsDir() => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

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
        _ => MoverParaLixeira(EntradasTopo(PastaDe(cat))),
    };

    /// <summary>
    /// Limpeza Total: exclui os arquivos e pastas diretamente do computador (pulando a Lixeira),
    /// respeitando rigorosamente a guarda <see cref="DentroDe"/> e pulando arquivos em uso ou reparse points.
    /// </summary>
    public static ResultadoLimpeza LimparPermanente(CategoriaLimpeza cat) => cat switch
    {
        CategoriaLimpeza.Lixeira => EsvaziarLixeira(),
        CategoriaLimpeza.CacheMiniaturas => ExcluirPermanentemente(ArquivosCache().ToList()),
        _ => ExcluirPermanentemente(EntradasTopo(PastaDe(cat))),
    };

    /// <summary>Exclui permanentemente cada caminho dentro das raízes seguras permitidas, sem passar pela Lixeira.</summary>
    public static ResultadoLimpeza ExcluirPermanentemente(IEnumerable<string> alvos, IEnumerable<string>? raizesPermitidas = null)
    {
        var raizes = (raizesPermitidas ?? RaizesPermitidas()).ToArray();
        int itens = 0, erros = 0;
        long bytes = 0;

        foreach (var p in alvos)
        {
            if (!DentroDe(p, raizes))
            {
                erros++;
                continue;
            }
            ExcluirEntradaDireta(p, raizes, ref itens, ref bytes, ref erros);
        }

        var msg = erros > 0
            ? $"{itens} item(ns) deletado(s) permanentemente; {erros} em uso/ignorado(s)."
            : $"{itens} item(ns) deletado(s) permanentemente do computador.";
        return new(itens, bytes, erros, msg);
    }

    static void ExcluirEntradaDireta(string caminho, string[] raizes, ref int itens, ref long bytes, ref int erros)
    {
        try
        {
            if (File.Exists(caminho))
            {
                var fi = new FileInfo(caminho);
                if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;
                long tam = Math.Max(0, SafeLen(caminho));
                if (fi.Attributes.HasFlag(FileAttributes.ReadOnly))
                    fi.Attributes = FileAttributes.Normal;
                File.Delete(caminho);
                itens++;
                bytes += tam;
                return;
            }

            if (Directory.Exists(caminho))
            {
                var di = new DirectoryInfo(caminho);
                if (di.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;

                int errosSub = 0;
                long bytesSub = 0;
                int arquivosApagados = 0;
                LimparConteudoDiretorioRecursivo(di, raizes, ref arquivosApagados, ref bytesSub, ref errosSub);

                try
                {
                    if (di.Attributes.HasFlag(FileAttributes.ReadOnly))
                        di.Attributes = FileAttributes.Directory;
                    Directory.Delete(caminho, recursive: false);
                    itens++;
                    bytes += bytesSub;
                }
                catch
                {
                    // Se sobrou algum arquivo travado dentro da subpasta, contabiliza o que já foi liberado lá dentro
                    if (arquivosApagados > 0)
                    {
                        itens += arquivosApagados;
                        bytes += bytesSub;
                    }
                    erros += Math.Max(1, errosSub);
                }
            }
        }
        catch
        {
            erros++;
        }
    }

    static void LimparConteudoDiretorioRecursivo(DirectoryInfo dir, string[] raizes, ref int arquivosApagados, ref long bytes, ref int erros)
    {
        FileInfo[] arquivos;
        try { arquivos = dir.GetFiles(); }
        catch { erros++; return; }

        foreach (var f in arquivos)
        {
            try
            {
                if (!DentroDe(f.FullName, raizes) || f.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                long tam = Math.Max(0, f.Length);
                if (f.Attributes.HasFlag(FileAttributes.ReadOnly))
                    f.Attributes = FileAttributes.Normal;
                f.Delete();
                arquivosApagados++;
                bytes += tam;
            }
            catch { erros++; }
        }

        DirectoryInfo[] subpastas;
        try { subpastas = dir.GetDirectories(); }
        catch { erros++; return; }

        foreach (var sub in subpastas)
        {
            try
            {
                if (!DentroDe(sub.FullName, raizes) || sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                LimparConteudoDiretorioRecursivo(sub, raizes, ref arquivosApagados, ref bytes, ref erros);
                if (sub.Attributes.HasFlag(FileAttributes.ReadOnly))
                    sub.Attributes = FileAttributes.Directory;
                sub.Delete(recursive: false);
            }
            catch { erros++; }
        }
    }

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
        if (antes.Itens == 0)
            return new(0, 0, 0, "Lixeira já estava vazia.");
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
    static string[] RaizesPermitidas() => new[]
    {
        Path.GetTempPath(), ExplorerDir(),
        Path.Combine(WindowsDir(), "Temp"),
        Path.Combine(WindowsDir(), "SoftwareDistribution", "Download"),
        Path.Combine(WindowsDir(), "Prefetch"),
    };

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
