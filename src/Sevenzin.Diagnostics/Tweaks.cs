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
        if (t.Tipo == TipoValor.String) _reg.EscreverString(t.Colmeia, t.Caminho, t.NomeValor, t.ValorTexto);
        else _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, t.ValorOtimizado);
        // Registra o backup SÓ DEPOIS da gravação ter sucesso: um tweak cuja escrita falhou
        // (ex.: chave protegida) não deve gerar um "Desfazer" fantasma.
        _backup[t.Id] = anterior;
        SalvarBackup();
        Auditar("aplicar", t, anterior);
        return new(t.Id, anterior.Dword, t.ValorOtimizado);
    }

    /// <summary>Desfaz usando o estado salvo. False se não há backup (nunca aplicado por aqui).</summary>
    public bool Desfazer(Tweak t)
    {
        if (!_backup.TryGetValue(t.Id, out var anterior)) return false;
        AplicarValorAnterior(t, anterior);
        Auditar("desfazer", t, anterior);
        _backup.Remove(t.Id);
        SalvarBackup();
        return true;
    }

    /// <summary>
    /// Desativa sempre um tweak: usa o backup anterior quando ele de fato desliga o ajuste;
    /// caso o ajuste já viesse ativo no Windows antes do Sevenzin (sem backup), restaura o padrão de fábrica do Windows.
    /// </summary>
    public bool Desativar(Tweak t)
    {
        if (_backup.TryGetValue(t.Id, out var salvo))
        {
            bool backupDiferenteDoOtimizado = salvo.EraString
                ? salvo.Texto != t.ValorTexto
                : salvo.Dword != t.ValorOtimizado;
            if (backupDiferenteDoOtimizado)
                return Desfazer(t);
        }

        var padrao = PadraoDesativadoWindows(t);
        AplicarValorAnterior(t, padrao);

        // Defesa extra: se ApagarValor manteve valor herdado igual ao otimizado, força o inverso explícito
        if (Aplicado(t))
        {
            if (t.Tipo == TipoValor.String)
                _reg.EscreverString(t.Colmeia, t.Caminho, t.NomeValor, t.ValorTexto == "0" ? "1" : "0");
            else
                _reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, t.ValorOtimizado == 0 ? 1 : 0);
        }

        Auditar("desativar-padrao", t, padrao);
        if (_backup.Remove(t.Id))
            SalvarBackup();
        return !Aplicado(t);
    }

    void AplicarValorAnterior(Tweak t, ValorAnterior anterior)
    {
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
    }

    /// <summary>Padrão nativo do Windows para desativar cada ajuste quando não há backup anterior salvo.</summary>
    public static ValorAnterior PadraoDesativadoWindows(Tweak t)
    {
        if (t.Tipo == TipoValor.String)
        {
            string? txt = t.Id switch
            {
                "mouse-aceleracao-off" => "1",
                "mouse-limiar1-off" => "6",
                "mouse-limiar2-off" => "10",
                "mouse-hover-off" => "400",
                "arrastar-janela-off" => "1",
                "menu-delay-0" => "400",
                _ => null,
            };
            return new ValorAnterior(true, null, txt);
        }

        if (t.Caminho.StartsWith(@"SOFTWARE\Policies\", StringComparison.OrdinalIgnoreCase)
            || t.Caminho.StartsWith(@"Software\Policies\", StringComparison.OrdinalIgnoreCase))
            return new ValorAnterior(false, null, null);

        int? dword = t.Id switch
        {
            "gamedvr-off" or "gamedvr-captura-off" => 1,
            "gamedvr-fse-off" => 0,
            "advertising-id-off" or "sugestoes-off" or "sugestoes-config-off"
                or "apps-promovidos-off" or "dicas-windows-off" or "experiencias-sob-medida-off"
                or "destaques-busca-off" or "bing-busca-off" or "notificacoes-off" => 1,
            "taskbar-animations-off" or "widgets-barra-off" or "transparencia-off"
                or "taskbar-transparencia-off" or "aero-peek-off" or "tema-escuro"
                or "botao-taskview-off" or "botao-cortana-off" or "min-animate-off" => 1,
            "som-inicializacao-off" or "dados-adicionais-off" or "wer-off"
                or "paginacao-off" or "large-cache-on" => 0,
            "mmcss-responsiveness" => 20,
            "priority-separation" or "priority-separation-26" => 2,
            "fast-startup-off" => 1,
            "hags" => 1,
            "uac-off" => 1,
            "uac-consent-admin" => 5,
            "uac-consent-user" => 3,
            "uac-filter-token" => 1,
            "uac-secure-desktop" => 1,
            "uac-validate-signatures" => 1,
            "uac-uiadesktop" => 1,
            "uac-secureuia" => 1,
            "hvci-off" => 1,
            "localizacao-servico-off" => 3,
            "mmcss-games-gpu" => 2,
            "lmhosts-off" => 1,
            var id when id.StartsWith("rede-netbios-", StringComparison.OrdinalIgnoreCase) => 0,
            _ => null,
        };
        return new ValorAnterior(false, dword, null);
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
        new Tweak("mitigacoes-off", "Desligar mitigações Spectre/Meltdown", "Desativa as mitigações de hardware contra Spectre/Meltdown (FeatureSettingsOverride=33554435, valor literal do original). Requer reinício.", "Segurança",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 33554435, "ALTO — expõe a CPU às falhas Spectre/Meltdown; requer reinício", true, true),
        new Tweak("wu-pausar", "Pausar atualizações automáticas", "Política que impede o download/instalação automática de atualizações do Windows (NoAutoUpdate=1).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1, "ALTO — deixa o sistema sem correções de segurança novas", true, true),

        // ── UAC completo (valores literais do original: Policies\System) ──
        new Tweak("uac-consent-admin", "UAC: consentimento de admin", "ConsentPromptBehaviorAdmin=0 (valor do original) — elevações de admin não pedem confirmação.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0, "ALTO — remove o consentimento; requer reinício", true, true),
        new Tweak("uac-consent-user", "UAC: consentimento de usuário", "ConsentPromptBehaviorUser=0 (valor do original) — usuários elevam sem pedir senha.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorUser", 0, "ALTO — remove o consentimento; requer reinício", true, true),
        new Tweak("uac-default-consent", "UAC: consentimento padrão", "DefaultConsent=1 (valor do original) — consentimento implícito para o usuário padrão.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DefaultConsent", 1, "ALTO — reduz o consentimento; requer reinício", true, true),
        new Tweak("uac-filter-token", "UAC: filtro de token", "FilterAdministratorToken=0 (valor do original) — remove o token filtrado do admin.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "FilterAdministratorToken", 0, "ALTO — requer reinício", true, true),
        new Tweak("uac-secure-desktop", "UAC: prompt fora da área segura", "PromptOnSecureDesktop=0 (valor do original) — o prompt do UAC não usa a área de trabalho segura.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", 0, "ALTO — o prompt pode ser interceptado", true, true),
        new Tweak("uac-validate-signatures", "UAC: validar assinaturas", "ValidateAdminCodeSignatures=0 (valor do original) — não exige assinatura para elevar.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ValidateAdminCodeSignatures", 0, "ALTO", true, true),
        new Tweak("uac-uiadesktop", "UAC: toggle de acessibilidade", "EnableUIADesktopToggle=0 (valor do original).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableUIADesktopToggle", 0, "ALTO", true, true),
        new Tweak("uac-secureuia", "UAC: caminhos seguros da UI", "EnableSecureUIAPaths=0 (valor do original).", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSecureUIAPaths", 0, "ALTO", true, true),

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

        // ── GPU AMD — power gating (valores literais do original; caminho do driver de vídeo 0000) ──
        new Tweak("amd-gmm-powergate-off", "AMD: power gating GMM off", "DisableGMMPowerGating=1 (valor do original) — desliga o power gating de memória gráfica.", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DisableGMMPowerGating", 1, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-samu-powergate-off", "AMD: power gating SAMU off", "DisableSAMUPowerGating=1 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DisableSAMUPowerGating", 1, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-vcn-powergate-off", "AMD: power gating VCN off", "DisableVCNPowerGating=1 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DisableVCNPowerGating", 1, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-drmdma-powergate-off", "AMD: power gating DRMDMA off", "DisableDrmdmaPowerGating=1 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DisableDrmdmaPowerGating", 1, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-ulps-off", "AMD: ULPS off", "EnableUlps=0 (valor do original) — desliga a economia de energia ultra-baixa da GPU.", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "EnableUlps", 0, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-asic-lowpower-off", "AMD: ASIC low power off", "AsicOnLowPower=0 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "AsicOnLowPower", 0, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-gpu-powerdown-off", "AMD: GPU PowerDown off", "PP_GPUPowerDownEnabled=0 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "PP_GPUPowerDownEnabled", 0, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-sclk-deepsleep-off", "AMD: SCLK deep sleep off", "PP_SclkDeepSleepDisable=1 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "PP_SclkDeepSleepDisable", 1, "Médio — só AMD; calor/consumo; meça A/B", true),
        new Tweak("amd-power-containment-off", "AMD: power containment off", "PP_DisablePowerContainment=1 (valor do original).", "GPU",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "PP_DisablePowerContainment", 1, "Médio — só AMD; calor/consumo; meça A/B", true),

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

        // ── Visual extra (valores literais do original) ──
        new Tweak("tema-escuro", "Tema escuro do Windows", "AppsUseLightTheme=0 (valor do original) — tema escuro para os aplicativos.", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0, "Baixo (cosmético)", false),
        new Tweak("botao-taskview-off", "Esconder botão Visão de Tarefas", "ShowTaskViewButton=0 (valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0, "Baixo (cosmético)", false),
        new Tweak("botao-cortana-off", "Esconder botão Cortana", "ShowCortanaButton=0 (valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCortanaButton", 0, "Baixo (cosmético)", false),
        new Tweak("som-inicializacao-off", "Desligar som de inicialização", "DisableStartupSound=1 (valor do original).", "Visual",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation", "DisableStartupSound", 1, "Baixo (cosmético)", false),
        new Tweak("min-animate-off", "Desligar animações de minimizar", "MinAnimate=0 (valor do original) — minimizar/maximizar sem animação.", "Visual",
            Colmeia.UsuarioAtual, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", 0, "Baixo (cosmético)", false),
        new Tweak("bing-busca-off", "Desligar busca web no Menu Iniciar", "BingSearchEnabled=0 (valor do original) — remove resultados da web na busca.", "Privacidade",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, "Baixo — privacidade", false),
        new Tweak("mouse-hover-off", "Hover do mouse instantâneo", "MouseHoverTime=0 (valor do original, REG_SZ) — balões e dicas aparecem sem atraso.", "Mouse",
            Colmeia.UsuarioAtual, @"Control Panel\Mouse", "MouseHoverTime", 0, "Baixo", false,
            Tipo: TipoValor.String, ValorTexto: "0"),

        // ── Privacidade/rede extras (valores literais do original) ──
        new Tweak("dados-adicionais-off", "Não enviar dados adicionais", "DontSendAdditionalData=1 (valor do original) — reduz dados enviados ao Windows.", "Telemetria",
            Colmeia.UsuarioAtual, @"Software\Microsoft\Windows\CurrentVersion\DeviceAccess\Global", "DontSendAdditionalData", 1, "Baixo — privacidade", false),
        new Tweak("lmhosts-off", "Desligar LMHOSTS", "EnableLMHOSTS=0 (valor do original) — desliga a resolução legada por arquivo LMHOSTS.", "Rede",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters", "EnableLMHOSTS", 0, "Baixo — legado de rede", true),

        // ── Timer global de alta resolução (valor literal do original) ──
        new Tweak("timer-global-1ms", "Timer global de alta resolução", "GlobalTimerResolutionRequests=1 (valor do original) — o sistema atende pedidos de resolução de 1 ms. ATENÇÃO: aumenta o consumo de energia (o SevenzinTwks prefere o timer opt-in por jogo, que é temporário).", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1, "ALTO — global e permanente até desfazer; aumenta consumo de energia", true, true),

        // ── Fechamento de paridade de registro (valores literais confirmados na engenharia reversa) ──
        new Tweak("priority-separation-26", "Prioridade de foreground (quantum curto 0x1A)", "Preset alternativo confirmado no original (Win32PrioritySeparation=26 / 0x1A: quantum curto fixo e boost máximo ao foreground). Use este OU o valor 38.", "Desempenho",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 26, "Médio — quantum curto fixo (0x1A); meça A/B", true),
        new Tweak("gamedvr-fse-off", "Desativar otimizações de tela cheia (FSE global)", "Define GameDVR_FSEBehaviorMode=2 em HKCU (valor literal do original) para priorizar modo exclusivo legado.", "Jogos",
            Colmeia.UsuarioAtual, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, "Baixo — pode ser revertido a qualquer momento", false),
        new Tweak("gamedvr-policy-off", "Bloquear Game DVR por política de máquina", "Política AllowGameDVR=0 em HKLM (valor literal do original) — impede gravação de fundo da Xbox Game Bar.", "Jogos",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, "Baixo — desativa captura Xbox Game Bar", true),
        new Tweak("onedrive-sync-off", "Desativar sincronização em segundo plano do OneDrive", "Política DisableFileSyncNGSC=1 em HKLM (valor literal do original) — pausa a sincronização sem apagar arquivos.", "Privacidade",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1, "Baixo — pausa o cliente OneDrive; reversível", true),
        new Tweak("mitigacoes-mask", "Máscara de mitigações Spectre/Meltdown (Mask)", "Define FeatureSettingsOverrideMask=33554435 (0x2000003, par obrigatório de FeatureSettingsOverride no original). Requer reinício.", "Segurança",
            Colmeia.MaquinaLocal, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 33554435, "ALTO — ativa a máscara de desligamento Spectre/Meltdown; requer reinício", true, true),
        new Tweak("wu-drivers-off", "Impedir atualização automática de drivers pelo Windows Update", "Define ExcludeWUDriversInQualityUpdate=1 (valor literal do original) para evitar sobrescrever drivers de GPU/rede.", "Segurança",
            Colmeia.MaquinaLocal, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1, "ALTO — drivers passam a exigir atualização manual", true, true),
    };
}
