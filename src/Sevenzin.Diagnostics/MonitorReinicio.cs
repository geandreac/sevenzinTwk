using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Sevenzin.Diagnostics;

/// <summary>Um evento do log System do Windows (só os campos que importam aqui).</summary>
public sealed record EventoSistema(int Id, DateTime Utc, string Provedor, IReadOnlyList<string> Dados);

/// <summary>Detecta, na abertura do app, se o PC foi desligado/reiniciado desde a última vez e explica o motivo lendo
/// o log System do Windows (somente leitura, via wevtutil):
/// 1001 do WER-SystemErrorReporting = tela azul (BSOD); 41 Kernel-Power / 6008 = desligou sem encerrar o Windows
/// (queda de energia, travamento, botão de força); 1074 = desligamento/reinício pedido por um programa ou pessoa,
/// com o motivo. O resultado vai para Notificações. Estado em %LOCALAPPDATA%\Sevenzin\estado-sistema.json.</summary>
public static class MonitorReinicio
{
    sealed record Estado(DateTime UltimoBootUtc, DateTime VerificadoAteUtc);
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    public static string ArquivoPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "estado-sistema.json");

    /// <summary>Quando o Windows ligou (agora − tempo ligado). Muda a cada reinício de verdade.</summary>
    public static DateTime BootAtualUtc() => DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>Na abertura do app. Na 1ª vez só guarda o estado (não despeja histórico antigo em Notificações).
    /// Nunca lança: falhar aqui não pode atrapalhar a abertura.</summary>
    public static void VerificarENotificar(NotificacoesStore store, string? arquivoEstado = null,
        Func<DateTime, string>? lerEventosDesde = null, Func<DateTime>? boot = null)
    {
        try
        {
            var arq = arquivoEstado ?? ArquivoPadrao;
            var bootAtual = (boot ?? BootAtualUtc)();
            var anterior = Ler(arq);
            Gravar(arq, new Estado(bootAtual, DateTime.UtcNow));
            if (anterior is null) return;

            bool reiniciou = Math.Abs((bootAtual - anterior.UltimoBootUtc).TotalMinutes) > 2;
            var eventos = ParseWevtutil((lerEventosDesde ?? LerLogSistema)(anterior.VerificadoAteUtc))
                .Where(e => e.Utc > anterior.VerificadoAteUtc).ToList();
            if (Interpretar(eventos, reiniciou, bootAtual) is { } n) store.Adicionar("Sistema", n.Titulo, n.Detalhe);
        }
        catch { }
    }

    /// <summary>Escolhe a notificação mais importante: tela azul > desligamento inesperado > pedido de
    /// desligamento/reinício > reinício simples. Null quando não houve nada.</summary>
    public static (string Titulo, string Detalhe)? Interpretar(IReadOnlyList<EventoSistema> eventos, bool reiniciou, DateTime bootUtc)
    {
        static string Quando(DateTime utc) => utc.ToLocalTime().ToString("dd/MM 'às' HH:mm", CultureInfo.InvariantCulture);
        var volta = reiniciou ? $" O Windows voltou {Quando(bootUtc)}." : "";

        var bsod = eventos.Where(e => e.Id == 1001 && e.Provedor.Contains("SystemErrorReporting", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Utc).FirstOrDefault();
        if (bsod is not null)
        {
            var codigo = (bsod.Dados.FirstOrDefault() ?? "").Split(' ', 2)[0];
            return ("Tela azul (BSOD) detectada",
                $"O Windows travou com o erro {(codigo.Length > 0 ? codigo : "desconhecido")} e reiniciou ({Quando(bsod.Utc)}).{volta} " +
                "Se repetir, verifique os drivers (principalmente o de vídeo), a memória e a temperatura.");
        }

        if (eventos.Any(e => e.Id is 41 or 6008))
            return ("Desligamento inesperado do PC",
                $"O PC desligou sem encerrar o Windows corretamente (queda de energia, travamento ou botão de força).{volta}");

        var pedido = eventos.Where(e => e.Id == 1074).OrderByDescending(e => e.Utc).FirstOrDefault();
        if (pedido is not null)
        {
            var programa = Path.GetFileName((pedido.Dados.ElementAtOrDefault(0) ?? "").Split(" (")[0]);
            var motivo = pedido.Dados.ElementAtOrDefault(2);
            var tipo = pedido.Dados.ElementAtOrDefault(4);
            return ("O PC foi desligado ou reiniciado",
                $"{(string.IsNullOrWhiteSpace(tipo) ? "" : $"Tipo: {tipo}. ")}Pedido por {(programa.Length > 0 ? programa : "um programa")} " +
                $"({Quando(pedido.Utc)}){(string.IsNullOrWhiteSpace(motivo) ? "" : $". Motivo: {motivo}")}.{volta}");
        }

        return reiniciou ? ("O PC foi reiniciado", $"O Windows foi iniciado {Quando(bootUtc)}.") : null;
    }

    /// <summary>Converte a saída XML do wevtutil (vários &lt;Event&gt; soltos) em eventos.</summary>
    public static List<EventoSistema> ParseWevtutil(string? xml)
    {
        var r = new List<EventoSistema>();
        if (string.IsNullOrWhiteSpace(xml)) return r;
        foreach (var ev in XDocument.Parse("<r>" + xml.TrimStart('﻿') + "</r>").Root!.Elements(Ns + "Event"))
        {
            var sys = ev.Element(Ns + "System");
            if (sys is null || !int.TryParse(sys.Element(Ns + "EventID")?.Value, out var id)) continue;
            if (!DateTime.TryParse(sys.Element(Ns + "TimeCreated")?.Attribute("SystemTime")?.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) continue;
            var provedor = sys.Element(Ns + "Provider")?.Attribute("Name")?.Value ?? "";
            var dados = ev.Element(Ns + "EventData")?.Elements(Ns + "Data").Select(d => d.Value).ToList() ?? new List<string>();
            r.Add(new EventoSistema(id, utc, provedor, dados));
        }
        return r;
    }

    // /uni:true + leitura em UTF-16: sem isso os acentos do motivo ("não planejada") chegam corrompidos.
    static string LerLogSistema(DateTime desdeUtc)
    {
        var desde = desdeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z";
        var psi = new ProcessStartInfo("wevtutil")
        {
            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.Unicode,
        };
        foreach (var a in new[]
        {
            "qe", "System", $"/q:*[System[(EventID=41 or EventID=6008 or EventID=1074 or EventID=1001) and TimeCreated[@SystemTime>='{desde}']]]",
            "/f:xml", "/c:50", "/rd:true", "/uni:true",
        })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10_000);
        return saida;
    }

    static Estado? Ler(string arq)
    {
        try { return File.Exists(arq) ? JsonSerializer.Deserialize<Estado>(File.ReadAllText(arq), Opts) : null; }
        catch { return null; }
    }

    static void Gravar(string arq, Estado e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arq)!);
        var tmp = arq + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(e, Opts));
        File.Move(tmp, arq, overwrite: true);
    }
}
