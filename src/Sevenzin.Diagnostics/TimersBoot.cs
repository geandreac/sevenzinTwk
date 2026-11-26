using System.Diagnostics;
using System.Management;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Um ajuste de timer no boot (BCD) diferente do padrão do Windows.</summary>
public sealed record AjusteTimer(string Chave, string Valor, string Explicacao);

public sealed record ResultadoTimers(bool Ok, string Mensagem);

/// <summary>Diagnóstico dos timers de boot que scripts de "otimização" costumam alterar (useplatformclock,
/// useplatformtick, disabledynamictick, tscsyncpolicy). O padrão do Windows é NÃO ter esses valores: ele escolhe
/// sozinho o melhor relógio para o hardware. Voltar ao padrão = apagar os valores, guardando os anteriores para
/// desfazer. Exige administrador; vale após reiniciar.</summary>
public static class TimersBoot
{
    public static readonly IReadOnlyDictionary<string, string> Explicacoes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["useplatformclock"] = "Força o HPET como relógio do sistema. O padrão deixa o Windows escolher (normalmente o TSC, mais rápido de ler); forçar o HPET costuma aumentar a latência.",
        ["useplatformtick"] = "Força o tick do relógio da plataforma em vez do tick sintético. Sem ganho comprovado; pode piorar a precisão em CPUs modernas.",
        ["disabledynamictick"] = "Desliga o tick dinâmico, que deixa a CPU dormir quando ociosa. Aumenta consumo e calor; efeito em jogos não comprovado.",
        ["tscsyncpolicy"] = "Muda como o Windows sincroniza o contador de tempo (TSC) entre os núcleos. O padrão é escolhido pelo Windows conforme o hardware.",
    };

    public static string ArquivoBackup =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "bcd-timers-backup.json");

    /// <summary>Lê "nome   valor" da saída do <c>bcdedit /enum {current}</c> (nomes de elementos vêm em inglês). Pura.</summary>
    public static Dictionary<string, string> Parse(string? saida)
    {
        var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in (saida ?? "").Split('\n'))
        {
            var l = linha.TrimEnd('\r');
            int i = l.IndexOf(' ');
            if (i <= 0 || l.StartsWith(' ')) continue;
            var nome = l[..i].Trim();
            if (Explicacoes.ContainsKey(nome)) r[nome] = l[i..].Trim();
        }
        return r;
    }

    /// <summary>Os timers presentes (= fora do padrão). Pura.</summary>
    public static List<AjusteTimer> Alterados(IReadOnlyDictionary<string, string> bcd) =>
        Explicacoes.Keys.Where(bcd.ContainsKey).Select(k => new AjusteTimer(k, bcd[k], Explicacoes[k])).ToList();

    static (int Codigo, string Saida) Bcdedit(params string[] args)
    {
        var psi = new ProcessStartInfo("bcdedit") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var s = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(15_000);
        return (p.ExitCode, s);
    }

    /// <summary>Estado atual (somente leitura). Null = não deu para ler (bcdedit exige administrador).</summary>
    public static List<AjusteTimer>? Ler()
    {
        try
        {
            var (c, s) = Bcdedit("/enum", "{current}");
            return c == 0 ? Alterados(Parse(s)) : null;
        }
        catch { return null; }
    }

    public static bool TemBackup => File.Exists(ArquivoBackup);

    /// <summary>Apaga os valores (volta ao padrão) depois de salvar os atuais para desfazer; confirma relendo.</summary>
    public static ResultadoTimers RestaurarPadrao()
    {
        var atuais = Ler();
        if (atuais is null) return new(false, "Não foi possível ler a configuração de boot (exige administrador).");
        if (atuais.Count == 0) return new(true, "Os timers de boot já estão no padrão do Windows.");
        Directory.CreateDirectory(Path.GetDirectoryName(ArquivoBackup)!);
        File.WriteAllText(ArquivoBackup, JsonSerializer.Serialize(atuais.ToDictionary(a => a.Chave, a => a.Valor)));
        var falhas = atuais.Where(a => Bcdedit("/deletevalue", "{current}", a.Chave).Codigo != 0).Select(a => a.Chave).ToList();
        var depois = Ler() ?? new();
        falhas.AddRange(depois.Select(a => a.Chave).Except(falhas));
        return falhas.Count == 0
            ? new(true, $"{atuais.Count} valor(es) voltaram ao padrão: {string.Join(", ", atuais.Select(a => a.Chave))}. Reinicie o PC para valer.")
            : new(false, $"Não voltaram ao padrão: {string.Join(", ", falhas)}. Os valores anteriores estão salvos para desfazer.");
    }

    /// <summary>Regrava os valores salvos e confirma relendo.</summary>
    public static ResultadoTimers Desfazer()
    {
        if (!TemBackup) return new(false, "Não há valores anteriores salvos.");
        var salvos = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ArquivoBackup)) ?? new();
        var falhas = salvos.Where(kv => Bcdedit("/set", "{current}", kv.Key, kv.Value).Codigo != 0).Select(kv => kv.Key).ToList();
        var depois = (Ler() ?? new()).ToDictionary(a => a.Chave, a => a.Valor, StringComparer.OrdinalIgnoreCase);
        falhas.AddRange(salvos.Keys.Where(k => !depois.ContainsKey(k)).Except(falhas));
        if (falhas.Count > 0) return new(false, $"Não foi possível regravar: {string.Join(", ", falhas)}.");
        File.Delete(ArquivoBackup);
        return new(true, $"Valores anteriores regravados: {string.Join(", ", salvos.Select(kv => $"{kv.Key}={kv.Value}"))}. Reinicie o PC para valer.");
    }

    /// <summary>true = BitLocker protegendo o disco do Windows (mudar o BCD pode pedir a chave de recuperação no
    /// próximo boot). Null = não deu para ler.</summary>
    public static bool? BitLockerAtivo()
    {
        try
        {
            var unidade = Path.GetPathRoot(Environment.SystemDirectory)!.TrimEnd('\\');
            using var s = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption",
                $"SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter = '{unidade}'");
            foreach (ManagementObject o in s.Get()) using (o) return Convert.ToUInt32(o["ProtectionStatus"]) == 1;
            return false;
        }
        catch { return null; }
    }
}
