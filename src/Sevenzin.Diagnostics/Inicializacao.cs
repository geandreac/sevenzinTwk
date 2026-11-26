using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

public enum OrigemInicializacao { RunUsuario, RunMaquina, RunMaquina32, PastaUsuario, PastaComum }

/// <summary>Um programa que abre sozinho ao entrar no Windows.</summary>
public sealed record ItemInicializacao(string Nome, string Comando, OrigemInicializacao Origem, bool Habilitado)
{
    public bool ExigeAdmin => Origem is OrigemInicializacao.RunMaquina or OrigemInicializacao.RunMaquina32 or OrigemInicializacao.PastaComum;
    public string Escopo => ExigeAdmin ? "Todos os usuários" : "Este usuário";
}

/// <summary>Programas de inicialização (chaves Run e pastas Inicializar), ligados e desligados pelo MESMO mecanismo do
/// Gerenciador de Tarefas: o valor binário em Explorer\StartupApproved. Nada é apagado — o programa continua
/// instalado e a entrada original fica intacta; reativar desfaz. Tarefas agendadas e serviços não entram aqui.</summary>
public static class Inicializacao
{
    const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    const string Aprovado = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    /// <summary>Sem valor = ligado. 1º byte par (0x02, 0x06) = ligado; ímpar (0x03, 0x07) = desligado. Pura.</summary>
    public static bool EstaHabilitado(byte[]? valor) => valor is not { Length: > 0 } || (valor[0] & 1) == 0;

    /// <summary>12 bytes: 0x03 + 3 zeros + FILETIME de quando foi desligado (o que o Gerenciador de Tarefas grava). Pura.</summary>
    public static byte[] ValorDesabilitado(DateTime utc)
    {
        var v = new byte[12];
        v[0] = 0x03;
        BitConverter.GetBytes(utc.ToFileTimeUtc()).CopyTo(v, 4);
        return v;
    }

    public static byte[] ValorHabilitado() { var v = new byte[12]; v[0] = 0x02; return v; }

    static (RegistryKey Raiz, string Sub) ChaveAprovacao(OrigemInicializacao o) => o switch
    {
        OrigemInicializacao.RunUsuario => (Registry.CurrentUser, Aprovado + "Run"),
        OrigemInicializacao.RunMaquina => (Registry.LocalMachine, Aprovado + "Run"),
        OrigemInicializacao.RunMaquina32 => (Registry.LocalMachine, Aprovado + "Run32"),
        OrigemInicializacao.PastaUsuario => (Registry.CurrentUser, Aprovado + "StartupFolder"),
        _ => (Registry.LocalMachine, Aprovado + "StartupFolder"),
    };

    public static List<ItemInicializacao> Listar()
    {
        var r = new List<ItemInicializacao>();
        void DoRegistro(RegistryKey raiz, string sub, OrigemInicializacao o)
        {
            try
            {
                using var k = raiz.OpenSubKey(sub);
                if (k is null) return;
                foreach (var nome in k.GetValueNames().Where(n => n.Length > 0))
                    r.Add(new(nome, k.GetValue(nome)?.ToString() ?? "", o, LerHabilitado(o, nome)));
            }
            catch { }
        }
        void DaPasta(Environment.SpecialFolder pasta, OrigemInicializacao o)
        {
            try
            {
                var dir = Environment.GetFolderPath(pasta);
                if (!Directory.Exists(dir)) return;
                foreach (var a in Directory.EnumerateFiles(dir).Where(a => !Path.GetFileName(a).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                    r.Add(new(Path.GetFileName(a), a, o, LerHabilitado(o, Path.GetFileName(a))));
            }
            catch { }
        }
        DoRegistro(Registry.CurrentUser, Run, OrigemInicializacao.RunUsuario);
        DoRegistro(Registry.LocalMachine, Run, OrigemInicializacao.RunMaquina);
        DoRegistro(Registry.LocalMachine, Run32, OrigemInicializacao.RunMaquina32);
        DaPasta(Environment.SpecialFolder.Startup, OrigemInicializacao.PastaUsuario);
        DaPasta(Environment.SpecialFolder.CommonStartup, OrigemInicializacao.PastaComum);
        return r.OrderBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static bool LerHabilitado(OrigemInicializacao o, string nome)
    {
        try
        {
            var (raiz, sub) = ChaveAprovacao(o);
            using var k = raiz.OpenSubKey(sub);
            return EstaHabilitado(k?.GetValue(nome) as byte[]);
        }
        catch { return true; }
    }

    /// <summary>Liga ou desliga e relê para confirmar. Null = ok; senão, o motivo. Itens de "todos os usuários" exigem admin.</summary>
    public static string? Definir(ItemInicializacao item, bool habilitar)
    {
        try
        {
            var (raiz, sub) = ChaveAprovacao(item.Origem);
            using (var k = raiz.CreateSubKey(sub, writable: true))
                k.SetValue(item.Nome, habilitar ? ValorHabilitado() : ValorDesabilitado(DateTime.UtcNow), RegistryValueKind.Binary);
            return LerHabilitado(item.Origem, item.Nome) == habilitar ? null : "O Windows não confirmou a mudança na releitura.";
        }
        catch (UnauthorizedAccessException) { return "Sem permissão: itens de todos os usuários exigem o SevenzinTwks como administrador."; }
        catch (Exception e) { return e.Message; }
    }

    /// <summary>Executável de um comando de inicialização, para mostrar (ex.: "C:\...\app.exe" --min → app.exe). Pura.</summary>
    public static string Executavel(string comando)
    {
        var c = comando.Trim();
        if (c.StartsWith('"')) { int f = c.IndexOf('"', 1); return Path.GetFileName(f > 0 ? c[1..f] : c.Trim('"')); }
        int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        // sem .exe é um atalho/arquivo da pasta Inicializar: o caminho inteiro (pode ter espaços, ex.: "Start Menu")
        return Path.GetFileName(exe > 0 ? c[..(exe + 4)] : c);
    }
}

/// <summary>Tempo de boot medido pelo próprio Windows (evento 100 de Diagnostics-Performance), em ms.</summary>
public sealed record MedicaoBoot(DateTime Utc, int TotalMs, int NucleoMs, int PosLoginMs);

/// <summary>Lê o tempo de boot do log do Windows (somente leitura). Para medir antes/depois de mudar a inicialização.
/// Depende do serviço Diagnostic Policy (DPS): desativado, o Windows não grava esses eventos.</summary>
public static class TempoBoot
{
    static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    /// <summary>Converte a saída XML do wevtutil (eventos soltos) em medições, mais recente primeiro. Pura.</summary>
    public static List<MedicaoBoot> Parse(string? xml)
    {
        var r = new List<MedicaoBoot>();
        if (string.IsNullOrWhiteSpace(xml)) return r;
        foreach (var ev in XDocument.Parse("<r>" + xml.TrimStart('\uFEFF') + "</r>").Root!.Elements(Ns + "Event"))
        {
            if (ev.Element(Ns + "System")?.Element(Ns + "EventID")?.Value != "100") continue;
            var dados = ev.Element(Ns + "EventData")?.Elements(Ns + "Data").ToDictionary(d => (string?)d.Attribute("Name") ?? "", d => d.Value) ?? new();
            int N(string k) => dados.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0;
            if (!DateTime.TryParse(ev.Element(Ns + "System")?.Element(Ns + "TimeCreated")?.Attribute("SystemTime")?.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) continue;
            if (N("BootTime") > 0) r.Add(new(utc, N("BootTime"), N("MainPathBootTime"), N("BootPostBootTime")));
        }
        return r.OrderByDescending(m => m.Utc).ToList();
    }

    public static List<MedicaoBoot> Ler(int quantos = 10)
    {
        try
        {
            var psi = new ProcessStartInfo("wevtutil") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.Unicode };
            foreach (var a in new[] { "qe", "Microsoft-Windows-Diagnostics-Performance/Operational", "/q:*[System[(EventID=100)]]", "/f:xml", $"/c:{quantos}", "/rd:true", "/uni:true" })
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10_000);
            return Parse(saida);
        }
        catch { return new(); }
    }

    /// <summary>true = serviço DPS desativado (sem ele não há medição de boot).</summary>
    public static bool DpsDesativado()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\DPS");
            return k?.GetValue("Start") is int s && s == 4;
        }
        catch { return false; }
    }
}
