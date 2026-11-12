using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Colmeia do registro usada por um tweak. HKCU não exige administrador; HKLM exige.</summary>
public enum Colmeia { UsuarioAtual, MaquinaLocal }

/// <summary>Tipo do valor escrito no registro. Dword é o padrão; String existe porque o original
/// gravava REG_SZ em chaves como `Control Panel\Mouse` (fidelidade ao que foi confirmado).</summary>
public enum TipoValor { Dword, String }

/// <summary>Acesso ao registro atrás de interface, para o motor ser testável sem tocar no registro real.</summary>
public interface IRegistro
{
    int? LerDword(Colmeia h, string caminho, string nome);
    void EscreverDword(Colmeia h, string caminho, string nome, int valor);
    string? LerString(Colmeia h, string caminho, string nome);
    void EscreverString(Colmeia h, string caminho, string nome, string valor);
    void ApagarValor(Colmeia h, string caminho, string nome);
}

/// <summary>Um ajuste reversível: um valor de registro com o valor "otimizado" e metadados de risco.
/// <paramref name="ExigeConfirmacao"/> = item de risco alto: a UI pede confirmação explícita antes de aplicar.
/// <paramref name="Tipo"/>/<paramref name="ValorTexto"/> definem o valor escrito (DWORD ou string).</summary>
public sealed record Tweak(
    string Id, string Nome, string Descricao, string Categoria,
    Colmeia Colmeia, string Caminho, string NomeValor, int ValorOtimizado,
    string Risco, bool ExigeAdmin, bool ExigeConfirmacao = false,
    TipoValor Tipo = TipoValor.Dword, string ValorTexto = "");

public sealed record ResultadoTweak(string Id, int? ValorAnterior, int ValorNovo);

/// <summary>Motor da Fase 4 (§21.7): aplica um tweak guardando o estado anterior, desfaz restaurando-o,
/// e registra auditoria. A antiga trava de segurança (GuardaSeguranca) foi removida por decisão do dono
/// em 12/11/2026 — ver docs/revisao_ajustes.md. Toda alteração permanece reversível e auditada.</summary>
public sealed class MotorTweaks
{
    /// <summary>Valor anterior de um tweak (DWORD ou string), para desfazer restaurar exatamente.</summary>
    public sealed record ValorAnterior(bool EraString, int? Dword, string? Texto);

    readonly IRegistro _reg;
    readonly string _arqBackup;
    readonly string _arqAuditoria;
    Dictionary<string, ValorAnterior> _backup;

    public MotorTweaks(IRegistro reg, string? pastaEstado = null)
    {
        _reg = reg;
        var pasta = pastaEstado ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");
        Directory.CreateDirectory(pasta);
        _arqBackup = Path.Combine(pasta, "tweaks-backup.json");
        _arqAuditoria = Path.Combine(pasta, "tweaks-auditoria.log");
        _backup = CarregarBackup();
    }

    /// <summary>Valor atual do tweak, como texto (para prévia/auditoria), ou null quando a chave não existe.</summary>
    string? ValorAtual(Tweak t) =>
        t.Tipo == TipoValor.String ? _reg.LerString(t.Colmeia, t.Caminho, t.NomeValor) : _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor)?.ToString();

    /// <summary>Valor "otimizado" do tweak, como texto.</summary>
    static string ValorAlvo(Tweak t) => t.Tipo == TipoValor.String ? t.ValorTexto : t.ValorOtimizado.ToString();

    /// <summary>Estado atual: aplicado (valor igual ao otimizado) ou não.</summary>
    public bool Aplicado(Tweak t) =>
        t.Tipo == TipoValor.String
            ? _reg.LerString(t.Colmeia, t.Caminho, t.NomeValor) == t.ValorTexto
            : _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor) == t.ValorOtimizado;

    /// <summary>Prévia textual do que a aplicação muda (§21.7 "o que será alterado").</summary>
    public string Previa(Tweak t)
    {
        var atual = ValorAtual(t);
        var hive = t.Colmeia == Colmeia.UsuarioAtual ? "HKCU" : "HKLM";
        return $"{t.Nome}\n\n{hive}\\{t.Caminho}\\{t.NomeValor}\n  de: {(atual ?? "(não definido)")}\n  para: {ValorAlvo(t)}\n\nRisco: {t.Risco}. Reversível (o estado anterior é salvo).";
    }

    public ResultadoTweak Aplicar(Tweak t)
    {
        var anterior = t.Tipo == TipoValor.String
            ? new ValorAnterior(true, null, _reg.LerString(t.Colmeia, t.Caminho, t.NomeValor))
            : new ValorAnterior(false, _reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor), null);
        _backup[t.Id] = anterior;
        SalvarBackup();
        if (t.Tipo == TipoValor.String) _reg.EscreverString(t.Colmeia, t.Caminho, t.NomeValor, t.ValorTexto);
        else _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, t.ValorOtimizado);
        Auditar("aplicar", t, anterior);
        return new(t.Id, anterior.Dword, t.ValorOtimizado);
    }

    /// <summary>Desfaz usando o estado salvo. False se não há backup (nunca aplicado por aqui).</summary>
    public bool Desfazer(Tweak t)
    {
        if (!_backup.TryGetValue(t.Id, out var anterior)) return false;
        if (anterior.EraString)
        {
            if (anterior.Texto is null) _reg.ApagarValor(t.Colmeia, t.Caminho, t.NomeValor);
            else _reg.EscreverString(t.Colmeia, t.Caminho, t.NomeValor, anterior.Texto);
        }
        else
        {
            if (anterior.Dword is null) _reg.ApagarValor(t.Colmeia, t.Caminho, t.NomeValor);
            else _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, anterior.Dword.Value);
        }
        Auditar("desfazer", t, anterior);
        _backup.Remove(t.Id);
        SalvarBackup();
        return true;
    }

    public bool TemBackup(string id) => _backup.ContainsKey(id);

    Dictionary<string, ValorAnterior> CarregarBackup()
    {
        try { return File.Exists(_arqBackup) ? JsonSerializer.Deserialize<Dictionary<string, ValorAnterior>>(File.ReadAllText(_arqBackup)) ?? new() : new(); }
        catch { return new(); }
    }
    void SalvarBackup() => File.WriteAllText(_arqBackup, JsonSerializer.Serialize(_backup));
    void Auditar(string acao, Tweak t, ValorAnterior anterior) =>
        File.AppendAllText(_arqAuditoria, $"{DateTime.UtcNow:o}\t{acao}\t{t.Id}\t{ValorAntigoTexto(anterior)}->{ValorAlvo(t)}\n");

    static string ValorAntigoTexto(ValorAnterior a) => a.EraString ? a.Texto ?? "-" : a.Dword?.ToString() ?? "-";
}

/// <summary>Catálogo de tweaks reversíveis, derivado do original (04_funcionalidades.md §3 + catalogo-otimizacoes.md).
/// Cada item é um valor DWORD único, reversível (prévia/backup/desfazer/auditoria). O grupo "Segurança" (UAC, Defender,
/// SmartScreen, HVCI, mitigações, Windows Update) entrou em 12/11/2026 por decisão do dono: risco ALTO/CRÍTICO,
/// confirmação explícita na UI e desfazer pelo motor — sem comportamento silencioso.</summary>
public static class CatalogoTweaks
{
    public static readonly IReadOnlyList<Tweak> Todos = new[]
    {
        // ── Jogos (HKCU, sem admin) ──
        new Tweak("gamedvr-off", "Desligar Game DVR", "Desativa a gravação em segundo plano da Game Bar (reduz overhead em jogos).", "Jogos",
            Colmeia.UsuarioAtual, @"System\GameConfigStore", "GameDVR_Enabled", 0, "Baixo", false),
        new Tweak("gamedvr-captura-off", "Desligar captura de jogos em segundo plano", "Desativa a captura automática da Game Bar (AppCaptureEnabled) — menos trabalho de fundo durante o jogo.", "Jogos",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, "Baixo", false),

        // ── Privacidade / sugestões (HKCU, sem admin) ──
        new Tweak("advertising-id-off", "Desligar ID de publicidade", "Impede apps de usarem o ID de publicidade para rastrear você.", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, "Baixo", false),
        new Tweak("sugestoes-off", "Sugestões no menu Iniciar", "Desativa apps e conteúdo sugeridos no menu Iniciar (SubscribedContent-338388).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, "Baixo", false),
        new Tweak("sugestoes-config-off", "Sugestões no app Configurações", "Desativa o conteúdo sugerido dentro do app Configurações (SystemPaneSuggestionsEnabled).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, "Baixo", false),
        new Tweak("apps-promovidos-off", "Instalação automática de apps promovidos", "Impede o Windows de instalar sozinho apps promovidos (SilentInstalledAppsEnabled).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0, "Baixo", false),
        new Tweak("dicas-windows-off", "Dicas e truques do Windows", "Desativa as dicas e novidades mostradas pelo Windows (SoftLandingEnabled).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, "Baixo", false),
        new Tweak("experiencias-sob-medida-off", "Experiências sob medida", "Impede o uso de dados de diagnóstico para personalizar dicas e anúncios (TailoredExperiences).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, "Baixo", false),
        new Tweak("destaques-busca-off", "Destaques da pesquisa", "Desativa o conteúdo dinâmico (ícones/novidades) na caixa de pesquisa (IsDynamicSearchBoxEnabled).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, "Baixo", false),

        // ── Visual (HKCU, sem admin; cosmético) ──
        new Tweak("taskbar-animations-off", "Desligar animações da barra de tarefas", "Remove animações da barra de tarefas (interface mais responsiva).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, "Baixo (cosmético)", false),
        new Tweak("widgets-barra-off", "Botão de Widgets na barra de tarefas", "Esconde o botão de Widgets/Notícias da barra de tarefas (TaskbarDa).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0, "Baixo (cosmético)", false),

        // ── Telemetria / atividade (HKLM, exigem administrador) ──
        new Tweak("telemetria-minima", "Telemetria no mínimo permitido", "Fixa a coleta de diagnóstico no nível mínimo (Básico/Obrigatório). Em Home/Pro o Windows não permite zero — por isso o valor é 1, como no original.", "Telemetria",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1, "Baixo — reduz, não remove", true),
        new Tweak("historico-atividades-off", "Linha do tempo / histórico de atividades", "Desliga a coleta e o envio da linha do tempo de atividades (EnableActivityFeed).", "Telemetria",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, "Baixo", true),

        // ── Desempenho (HKLM, exigem administrador; 🟡 meça A/B antes de confiar no ganho) ──
        new Tweak("mmcss-responsiveness", "Resposta do sistema para jogos", "Reduz a reserva de CPU para tarefas de fundo (SystemResponsiveness), dando mais à aplicação em foco.", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10, "Médio — depende do uso; meça A/B", true),
        new Tweak("priority-separation", "Prioridade de foreground", "Favorece a aplicação em primeiro plano no compartilhamento de CPU (Win32PrioritySeparation).", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, "Médio — pode afetar o fundo; meça A/B", true),
        new Tweak("fast-startup-off", "Desligar inicialização rápida", "Desliga o Fast Startup (HiberbootEnabled): inicialização mais previsível, drivers recarregados.", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, "Baixo — geralmente recomendado", true),
        new Tweak("hags", "Agendamento de GPU por hardware (HAGS)", "Liga o HwSchMode. Depende de GPU e driver; meça antes/depois.", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, "Médio — depende de GPU/driver; meça A/B", true),

        // ── Segurança (HKLM; risco ALTO/CRÍTICO — confirmação explícita obrigatória, desfazer pelo motor) ──
        new Tweak("uac-off", "Desligar UAC (controle de conta de usuário)", "Remove o consentimento de elevação (EnableLUA=0): programas elevam sem perguntar. Requer reinício para valer.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0, "ALTO — remove o consentimento de elevação; requer reinício", true, true),
        new Tweak("defender-rt-off", "Desligar proteção em tempo real (Defender)", "Desliga o monitoramento em tempo real do Microsoft Defender (DisableRealtimeMonitoring=1).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, "ALTO — o Windows pode ignorar com Tamper Protection ativa", true, true),
        new Tweak("defender-antispyware-off", "Desligar Defender (anti-spyware)", "Desativa o Microsoft Defender por registro (DisableAntiSpyware=1).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows Defender", "DisableAntiSpyware", 1, "ALTO — sem efeito no Windows 10/11 com Tamper Protection ativa", true, true),
        new Tweak("smartscreen-off", "Desligar SmartScreen", "Política que desativa a verificação SmartScreen em apps e navegação (EnableSmartScreen=0).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0, "ALTO — remove avisos de apps/arquivos não reconhecidos", true, true),
        new Tweak("hvci-off", "Desligar Isolamento de Núcleo (HVCI)", "Desativa a integridade de memória por virtualização (HypervisorEnforcedCodeIntegrity=0). Requer reinício.", "Segurança",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0, "CRÍTICO — reduz a proteção do kernel; requer reinício", true, true),
        new Tweak("mitigacoes-off", "Desligar mitigações Spectre/Meltdown", "Desativa as mitigações de hardware contra Spectre/Meltdown (FeatureSettingsOverride=1). Requer reinício.", "Segurança",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 1, "ALTO — expõe a CPU às falhas Spectre/Meltdown; requer reinício", true, true),
        new Tweak("wu-pausar", "Pausar atualizações automáticas", "Política que impede o download/instalação automática de atualizações do Windows (NoAutoUpdate=1).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1, "ALTO — deixa o sistema sem correções de segurança novas", true, true),

        // ── Mouse (HKCU, REG_SZ — o original gravava strings em Control Panel\Mouse) ──
        new Tweak("mouse-aceleracao-off", "Desligar aceleração do mouse", "Remove a aceleração do ponteiro (MouseSpeed=0) — movimento 1:1, previsível para jogos.", "Mouse",
            Colmeia.UsuarioAtual, @"Control Panel\Mouse", "MouseSpeed", 0, "Baixo", false,
            Tipo: TipoValor.String, ValorTexto: "0"),
        new Tweak("mouse-limiar1-off", "Limiar de aceleração 1", "Zera o 1º limiar de aceleração do mouse (MouseThreshold1=0).", "Mouse",
            Colmeia.UsuarioAtual, @"Control Panel\Mouse", "MouseThreshold1", 0, "Baixo", false,
            Tipo: TipoValor.String, ValorTexto: "0"),
        new Tweak("mouse-limiar2-off", "Limiar de aceleração 2", "Zera o 2º limiar de aceleração do mouse (MouseThreshold2=0).", "Mouse",
            Colmeia.UsuarioAtual, @"Control Panel\Mouse", "MouseThreshold2", 0, "Baixo", false,
            Tipo: TipoValor.String, ValorTexto: "0"),

        // ── Localização (HKLM, admin; confirmado no original: HideSCALocation + serviço lfsvc) ──
        new Tweak("localizacao-ocultar", "Ocultar o ícone de localização", "Política que esconde o indicador de localização na bandeja (HideSCALocation=1).", "Localização",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "HideSCALocation", 1, "Baixo — cosmético (política de máquina)", true),
        new Tweak("localizacao-servico-off", "Desativar o serviço de localização", "Desliga o serviço lfsvc (Start=4). AVISO: alguns jogos usam localização e podem não abrir sem ele.", "Localização",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Services\lfsvc", "Start", 4, "Médio — alguns jogos dependem do serviço; requer reinício para valer", true),
    };
}
