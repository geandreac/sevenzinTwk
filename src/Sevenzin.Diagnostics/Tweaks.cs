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

        // ── MMCSS "Games" (HKLM; valores literais do original: GPU Priority=8, Priority=2, SFIO=Normal, Scheduling=Medium) ──
        new Tweak("mmcss-games-gpu", "MMCSS Games — prioridade de GPU", "Perfil MMCSS 'Games' do Windows: GPU Priority=8 (valor literal do original).", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8, "Baixo — perfil padrão do Windows, reversível", true),
        new Tweak("mmcss-games-prio", "MMCSS Games — prioridade", "Perfil MMCSS 'Games': Priority=2 (valor literal do original).", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 2, "Baixo — perfil padrão do Windows, reversível", true),
        new Tweak("mmcss-games-sfio", "MMCSS Games — prioridade de I/O", "Perfil MMCSS 'Games': SFIO Priority=Normal (valor literal do original, REG_SZ).", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", 0, "Baixo — perfil padrão do Windows, reversível", true,
            Tipo: TipoValor.String, ValorTexto: "Normal"),
        new Tweak("mmcss-games-sched", "MMCSS Games — categoria de agendamento", "Perfil MMCSS 'Games': Scheduling Category=Medium (valor literal do original, REG_SZ).", "Desempenho",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", 0, "Baixo — perfil padrão do Windows, reversível", true,
            Tipo: TipoValor.String, ValorTexto: "Medium"),

        // ── GPU (HKLM, admin; valores do original) ──
        new Tweak("tdr-delay", "TDR Delay (driver de vídeo)", "Aumenta o tempo de recuperação de GPU travada (TdrDelay=8, valor do original) — mascara travas de driver em jogos.", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 8, "Médio — mascara travas de driver; meça A/B", true),
        new Tweak("amd-pstate-off", "Desligar Power gating (AMD)", "Desativa o power gating dinâmico da GPU AMD (DisableDynamicPstate=1, valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DisableDynamicPstate", 1, "Médio — pode aumentar calor/consumo; só AMD; meça A/B", true),
        new Tweak("amd-chill-off", "Desligar Radeon Chill (AMD)", "Desativa o Radeon Chill (limita FPS para economizar energia) — ChillEnabled=0.", "GPU",
            Colmeia.UsuarioAtual, @"Software\AMD\DVR", "ChillEnabled", 0, "Baixo — Chill limita FPS; desligar devolve o FPS livre", false),

        // ── Rede / multimídia (HKLM, admin; valores do original) ──
        new Tweak("rm-throttle-off", "Desligar atraso de throttle (RM)", "Zera o RMPriorityThrottleDelay (valor do original) — reduz o atraso do throttling de multimídia.", "Rede",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "RMPriorityThrottleDelay", 0, "Médio — afeta o escalonamento de multimídia; meça A/B", true),
        new Tweak("rede-throttle-off", "Desligar limite de rede (MMCSS)", "NetworkThrottlingIndex=10 (valor do original) — remove o limite de 10 pacotes/s imposto a tarefas de multimídia.", "Rede",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10, "Médio — pode aumentar latência em multitarefa; meça A/B", true),

        // ── Memória (HKLM, admin; debatível — meça A/B) ──
        new Tweak("paginacao-off", "Kernel residente (DisablePagingExecutive)", "Mantém o núcleo do sistema na RAM (DisablePagingExecutive=1, valor do original).", "Memória",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, "Médio — mais RAM usada; meça A/B", true),
        new Tweak("large-cache-on", "Cache grande de sistema (LargeSystemCache)", "Amplia o cache de arquivos do sistema (LargeSystemCache=1, valor do original).", "Memória",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 1, "Médio — pode piorar em pouca RAM; meça A/B", true),

        // ── Visual (HKCU, cosmético; valores do original) ──
        new Tweak("transparencia-off", "Desligar transparência", "Desativa a transparência das janelas (EnableTransparency=0, valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, "Baixo (cosmético)", false),
        new Tweak("taskbar-transparencia-off", "Desligar transparência da barra", "Desativa a transparência da barra de tarefas (UseOLEDTaskbarTransparency=0, valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "UseOLEDTaskbarTransparency", 0, "Baixo (cosmético)", false),
        new Tweak("aero-peek-off", "Desligar Aero Peek", "Desativa a prévia de janelas ao passar o mouse (EnableAeroPeek=0, valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, "Baixo (cosmético)", false),
        new Tweak("arrastar-janela-off", "Arrastar janela sem conteúdo", "Desliga o arrasto com conteúdo da janela (DragFullWindows=0, valor do original, REG_SZ).", "Visual",
            Colmeia.UsuarioAtual, @"Control Panel\Desktop", "DragFullWindows", 0, "Baixo (cosmético)", false,
            Tipo: TipoValor.String, ValorTexto: "0"),
        new Tweak("menu-delay-0", "Menus instantâneos", "Zera o atraso de abertura dos menus (MenuShowDelay=0, valor do original, REG_SZ).", "Visual",
            Colmeia.UsuarioAtual, @"Control Panel\Desktop", "MenuShowDelay", 0, "Baixo (cosmético)", false,
            Tipo: TipoValor.String, ValorTexto: "0"),
        new Tweak("notificacoes-off", "Desligar notificações do Windows", "Desativa os toasts de notificação (ToastEnabled=0, valor do original).", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0, "Baixo — você deixa de ver notificações de apps", false),

        // ── Privacidade extra (valores do catálogo §8 do original) ──
        new Tweak("wer-off", "Desligar relatório de erros (WER)", "Desativa o envio do Windows Error Reporting (Disabled=1).", "Telemetria",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, "Baixo — menos diagnóstico em caso de falha", true),
        new Tweak("copilot-off", "Desligar Copilot do Windows", "Desativa o Copilot por política de usuário (TurnOffWindowsCopilot=1).", "Telemetria",
            Colmeia.UsuarioAtual, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, "Baixo — remove o botão e o painel do Copilot", false),
    };
}
