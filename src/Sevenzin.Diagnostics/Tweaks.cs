using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Colmeia do registro usada por um tweak. HKCU não exige administrador; HKLM exige.</summary>
public enum Colmeia { UsuarioAtual, MaquinaLocal }

/// <summary>Acesso ao registro atrás de interface, para o motor ser testável sem tocar no registro real.</summary>
public interface IRegistro
{
    int? LerDword(Colmeia h, string caminho, string nome);
    void EscreverDword(Colmeia h, string caminho, string nome, int valor);
    void ApagarValor(Colmeia h, string caminho, string nome);
}

/// <summary>Um ajuste reversível: um valor DWORD de registro com o valor "otimizado" e metadados de risco.</summary>
public sealed record Tweak(
    string Id, string Nome, string Descricao, string Categoria,
    Colmeia Colmeia, string Caminho, string NomeValor, int ValorOtimizado,
    string Risco, bool ExigeAdmin);

public sealed record ResultadoTweak(string Id, int? ValorAnterior, int ValorNovo);

/// <summary>Trava de segurança (§23): recusa qualquer tweak que toque em mecanismos de segurança do SO.
/// É verificada antes de QUALQUER escrita; não há como aplicar um desses, mesmo que entre no catálogo.</summary>
public static class GuardaSeguranca
{
    static readonly string[] Proibidos =
    {
        "Windows Defender", "Microsoft Antimalware", "DisableAntiSpyware", "Real-Time Protection", "Spynet", "DefenderApiLogger", "DefenderAuditLogger",
        @"Policies\System",                 // UAC (ConsentPromptBehavior*)
        "ConsentPromptBehavior", "EnableLUA",
        "DeviceGuard", "HypervisorEnforcedCodeIntegrity", "EnableVirtualizationBasedSecurity", // HVCI/VBS
        "FeatureSettingsOverride",          // mitigações Spectre/Meltdown
        "MitigationOptions", "Image File Execution Options",                                   // Exploit Protection
        "SmartScreen",
        "WindowsUpdate", "WaUService",      // pausar updates
        "CacheIsPowerProtected",            // write cache forçado (risco de perda de dados)
    };

    /// <summary>True se o tweak mexe em segurança e deve ser recusado.</summary>
    public static bool Proibido(Tweak t)
    {
        var alvo = $"{t.Caminho}\\{t.NomeValor}";
        return Proibidos.Any(p => alvo.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Motor da Fase 4 (§21.7): aplica um tweak guardando o estado anterior, desfaz restaurando-o,
/// e registra auditoria. Nada de segurança é alterado — a GuardaSeguranca barra antes de escrever.</summary>
public sealed class MotorTweaks
{
    readonly IRegistro _reg;
    readonly string _arqBackup;
    readonly string _arqAuditoria;
    Dictionary<string, int?> _backup;

    public MotorTweaks(IRegistro reg, string? pastaEstado = null)
    {
        _reg = reg;
        var pasta = pastaEstado ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");
        Directory.CreateDirectory(pasta);
        _arqBackup = Path.Combine(pasta, "tweaks-backup.json");
        _arqAuditoria = Path.Combine(pasta, "tweaks-auditoria.log");
        _backup = CarregarBackup();
    }

    /// <summary>Estado atual: aplicado (valor igual ao otimizado) ou não.</summary>
    public bool Aplicado(Tweak t) => _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor) == t.ValorOtimizado;

    /// <summary>Prévia textual do que a aplicação muda (§21.7 "o que será alterado").</summary>
    public string Previa(Tweak t)
    {
        var atual = _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor);
        var hive = t.Colmeia == Colmeia.UsuarioAtual ? "HKCU" : "HKLM";
        return $"{t.Nome}\n\n{hive}\\{t.Caminho}\\{t.NomeValor}\n  de: {(atual?.ToString() ?? "(não definido)")}\n  para: {t.ValorOtimizado}\n\nRisco: {t.Risco}. Reversível (o estado anterior é salvo).";
    }

    public ResultadoTweak Aplicar(Tweak t)
    {
        if (GuardaSeguranca.Proibido(t)) throw new InvalidOperationException($"Tweak recusado pela trava de segurança: {t.Id}");
        int? anterior = _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor);
        _backup[t.Id] = anterior;                         // null = o valor não existia antes
        SalvarBackup();
        _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, t.ValorOtimizado);
        Auditar("aplicar", t, anterior, t.ValorOtimizado);
        return new(t.Id, anterior, t.ValorOtimizado);
    }

    /// <summary>Desfaz usando o estado salvo. False se não há backup (nunca aplicado por aqui).</summary>
    public bool Desfazer(Tweak t)
    {
        if (!_backup.TryGetValue(t.Id, out int? anterior)) return false;
        if (anterior is null) _reg.ApagarValor(t.Colmeia, t.Caminho, t.NomeValor); // não existia: remover
        else _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, anterior.Value);
        Auditar("desfazer", t, t.ValorOtimizado, anterior);
        _backup.Remove(t.Id);
        SalvarBackup();
        return true;
    }

    public bool TemBackup(string id) => _backup.ContainsKey(id);

    Dictionary<string, int?> CarregarBackup()
    {
        try { return File.Exists(_arqBackup) ? JsonSerializer.Deserialize<Dictionary<string, int?>>(File.ReadAllText(_arqBackup)) ?? new() : new(); }
        catch { return new(); }
    }
    void SalvarBackup() => File.WriteAllText(_arqBackup, JsonSerializer.Serialize(_backup));
    void Auditar(string acao, Tweak t, int? de, int? para) =>
        File.AppendAllText(_arqAuditoria, $"{DateTime.UtcNow:o}\t{acao}\t{t.Id}\t{de?.ToString() ?? "-"}->{para?.ToString() ?? "-"}\n");
}

/// <summary>Catálogo inicial: só tweaks HKCU reversíveis, sem administrador e de baixo risco,
/// derivados das categorias do original (GameDVR, ID de publicidade, visual, sugestões).</summary>
public static class CatalogoTweaks
{
    public static readonly IReadOnlyList<Tweak> Todos = new[]
    {
        new Tweak("gamedvr-off", "Desligar Game DVR", "Desativa a gravação em segundo plano da Game Bar (reduz overhead em jogos).", "Jogos",
            Colmeia.UsuarioAtual, @"System\GameConfigStore", "GameDVR_Enabled", 0, "Baixo", false),
        new Tweak("advertising-id-off", "Desligar ID de publicidade", "Impede apps de usarem o ID de publicidade para rastrear você.", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, "Baixo", false),
        new Tweak("taskbar-animations-off", "Desligar animações da barra de tarefas", "Remove animações da barra de tarefas (interface mais responsiva).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, "Baixo (cosmético)", false),
        new Tweak("sugestoes-off", "Desligar sugestões de conteúdo", "Desativa dicas e sugestões de apps no menu Iniciar.", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, "Baixo", false),

        // Máquina (HKLM): exigem administrador; reversíveis; 🟡 → medir A/B antes de confiar no ganho.
        new Tweak("mmcss-responsiveness", "Resposta do sistema para jogos", "Reduz a reserva de CPU para tarefas de fundo (SystemResponsiveness), dando mais à aplicação em foco.", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10, "Médio — depende do uso; meça A/B", true),
        new Tweak("priority-separation", "Prioridade de foreground", "Favorece a aplicação em primeiro plano no compartilhamento de CPU (Win32PrioritySeparation).", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, "Médio — pode afetar o fundo; meça A/B", true),
        new Tweak("fast-startup-off", "Desligar inicialização rápida", "Desliga o Fast Startup (HiberbootEnabled): inicialização mais previsível, drivers recarregados.", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, "Baixo — geralmente recomendado", true),
        new Tweak("hags", "Agendamento de GPU por hardware (HAGS)", "Liga o HwSchMode. Depende de GPU e driver; meça antes/depois.", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, "Médio — depende de GPU/driver; meça A/B", true),
    };
}
