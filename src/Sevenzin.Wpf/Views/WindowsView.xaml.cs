using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Windows": informações do SO (só leitura) + recomendações seguras que apontam para Ajustes
/// (reversível, auditado, uma mudança por vez, com confirmação). O construtor de ISO/debloat do original é omitido.</summary>
public partial class WindowsView : UserControl
{
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];

    readonly TextBlock _energiaLbl = new() { TextWrapping = TextWrapping.Wrap };
    readonly StackPanel _inicializacaoHost = new() { Margin = new Thickness(0, 0, 0, 24) };
    readonly StackPanel _timersHost = new() { Margin = new Thickness(0, 0, 0, 24) };
    readonly TextBlock _contagemIni = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 10) };
    int _ligadosIni, _totalIni;
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static readonly FontFamily Mono = new("Cascadia Mono, Consolas");

    // ── Inicialização (StartupApproved, mesmo mecanismo do Gerenciador de Tarefas) + tempo de boot medido ──

    async Task MontarInicializacaoAsync()
    {
        _inicializacaoHost.Children.Clear();
        _contagemIni.Text = "";
        _inicializacaoHost.Children.Add(new TextBlock { Text = "Lendo a inicialização…", Foreground = Muted, FontSize = 12 });
        var (itens, boots, dpsOff) = await Task.Run(() => (Inicializacao.Listar(), TempoBoot.Ler(5), TempoBoot.DpsDesativado()));
        _inicializacaoHost.Children.Clear();

        if (itens.Count == 0)
            _inicializacaoHost.Children.Add(new TextBlock { Text = "Nenhum programa configurado para abrir com o Windows.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
        else
        {
            _ligadosIni = itens.Count(i => i.Habilitado);
            _totalIni = itens.Count;
            AtualizarContagemIni(_ligadosIni);
            var lista = Ui.Grupo(itens.Select(LinhaInicializacao), 10);
            _inicializacaoHost.Children.Add(lista);
            Movimento.Entrar(lista, 6, 220);
            _inicializacaoHost.Children.Add(new TextBlock
            {
                Text = "Desligar não desinstala nada: o programa só deixa de abrir sozinho, e religar desfaz. Tarefas agendadas e serviços não aparecem nesta lista.",
                Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
            });
        }
        _inicializacaoHost.Children.Add(NotaBoot(boots, dpsOff));
    }

    void AtualizarContagemIni(double ligados)
    {
        _contagemIni.Text = $"{Math.Round(ligados):0} de {_totalIni} ligados";
        _contagemIni.Foreground = Secondary;
    }

    // Tempo de boot: a medição de antes e depois vem do próprio Windows. Medição velha ou ausente = barra de aviso.
    static UIElement NotaBoot(IReadOnlyList<MedicaoBoot> boots, bool dpsOff)
    {
        string titulo, texto;
        bool aviso;
        if (boots.Count > 0 && boots[0].Utc >= MonitorReinicio.BootAtualUtc().AddMinutes(-10))
        {
            var u = boots[0];
            titulo = $"Último boot: {u.TotalMs / 1000.0:0.0} s";
            texto = $"{u.NucleoMs / 1000.0:0.0} s até o login + {u.PosLoginMs / 1000.0:0.0} s depois dele, em {u.Utc.ToLocalTime():dd/MM 'às' HH:mm}"
                + (boots.Count > 1 ? $" · média dos últimos {boots.Count}: {boots.Average(b => b.TotalMs) / 1000.0:0.0} s." : ".")
                + " Para comparar, reinicie 3 vezes antes e 3 depois de mudar a lista.";
            aviso = false;
        }
        else if (boots.Count > 0)
        {
            // Medição de antes deste boot: o Windows parou de registrar — o número não vale para hoje.
            var u = boots[0];
            titulo = $"Medição de boot antiga ({u.TotalMs / 1000.0:0.0} s em {u.Utc.ToLocalTime():dd/MM/yyyy})";
            texto = dpsOff
                ? "O serviço Diagnostic Policy (DPS) está desativado e o Windows parou de registrar o tempo de boot. Reative-o em services.msc para medir antes e depois."
                : "O Windows não registrou os boots mais recentes.";
            aviso = true;
        }
        else
        {
            titulo = "Sem medição de boot";
            texto = dpsOff
                ? "O serviço Diagnostic Policy (DPS) está desativado neste PC, e sem ele o Windows não registra o tempo de boot. Reative-o em services.msc se quiser medir."
                : "O Windows ainda não registrou nenhum tempo de boot (ou o log exige administrador).";
            aviso = dpsOff;
        }
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = titulo, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = aviso ? Warning : (Brush)Application.Current.Resources["TextPrimaryBrush"] });
        sp.Children.Add(new TextBlock { Text = texto, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        return new Border
        {
            BorderBrush = aviso ? Warning : (Brush)Application.Current.Resources["BorderDefaultBrush"],
            BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 2, 0, 2), Child = sp,
        };
    }

    UIElement LinhaInicializacao(ItemInicializacao i)
    {
        // Atalhos da pasta Inicializar aparecem sem ".lnk"; o comando completo fica na dica.
        bool daPasta = i.Origem is OrigemInicializacao.PastaUsuario or OrigemInicializacao.PastaComum;
        string nome = daPasta ? System.IO.Path.GetFileNameWithoutExtension(i.Nome) : i.Nome;
        var atual = i;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var esq = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        var titulo = new TextBlock { Text = nome, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        esq.Children.Add(titulo);
        esq.Children.Add(new TextBlock
        {
            Text = (daPasta ? "Atalho na pasta Inicializar" : Inicializacao.Executavel(i.Comando)) + $" · {i.Escopo}",
            Foreground = Muted, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = i.Comando, Margin = new Thickness(0, 2, 0, 0),
        });
        g.Children.Add(esq);
        var sw = new System.Windows.Controls.Primitives.ToggleButton
        {
            Style = (Style)Application.Current.Resources["ToggleSwitch"], IsChecked = i.Habilitado, VerticalAlignment = VerticalAlignment.Center,
        };
        System.Windows.Automation.AutomationProperties.SetName(sw, $"{nome} abre com o Windows");
        // Desligado = nome recua para o tom secundário; a linha não some nem reordena.
        void Pintar() => titulo.Foreground = atual.Habilitado ? (Brush)Application.Current.Resources["TextPrimaryBrush"] : Secondary;
        void Marcar(bool ligado)
        {
            if (atual.Habilitado == ligado) return;
            atual = atual with { Habilitado = ligado };
            sw.IsChecked = ligado;
            Pintar();
            int antes = _ligadosIni;
            _ligadosIni += ligado ? 1 : -1;
            Movimento.Contar(antes, _ligadosIni, AtualizarContagemIni, 260);
        }
        Pintar();
        sw.Click += async (_, _) =>
        {
            bool ligar = sw.IsChecked == true;
            sw.IsChecked = atual.Habilitado; // só muda depois de confirmar e gravar
            if (atual.ExigeAdmin && !Elevacao.EhAdmin())
            {
                if (MessageBox.Show("Este item vale para todos os usuários e exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                        "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    Elevacao.ReabrirComoAdmin();
                return;
            }
            if (MessageBox.Show(ligar
                    ? $"Voltar a abrir \"{nome}\" junto com o Windows?"
                    : $"Parar de abrir \"{nome}\" junto com o Windows?\n\nO programa continua instalado e pode ser aberto normalmente; só não abre sozinho no login. Para desfazer, religue este item.",
                    "Inicialização do Windows", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            sw.IsEnabled = false;
            var alvo = atual;
            var erro = await Task.Run(() => Inicializacao.Definir(alvo, ligar));
            sw.IsEnabled = true;
            if (erro is null)
            {
                Marcar(ligar);
                NotificacoesStore.Padrao.Adicionar("Inicialização", $"'{nome}' {(ligar ? "religado" : "desligado")} na inicialização",
                    $"{i.Comando} ({i.Escopo}). Desfazer: {(ligar ? "desligue" : "religue")} o item na tela Windows.");
                Toasts.Mostrar(this, TipoToast.Sucesso, ligar ? "Religado na inicialização" : "Desligado na inicialização", nome,
                    () => { if (Inicializacao.Definir(atual, !ligar) is null) Marcar(!ligar); });
            }
            else Toasts.Mostrar(this, TipoToast.Erro, "Não foi possível alterar", erro);
        };
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return g;
    }

    // ── Timers de boot (BCD): diagnóstico + voltar ao padrão do Windows, com desfazer ──

    async Task MontarTimersAsync()
    {
        _timersHost.Children.Clear();
        var (alterados, temBackup) = await Task.Run(() => (TimersBoot.Ler(), TimersBoot.TemBackup));
        var sp = new StackPanel();
        if (alterados is null)
            sp.Children.Add(new TextBlock { Text = "Não foi possível ler a configuração de boot: abra o SevenzinTwks como administrador.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        else if (alterados.Count == 0)
            sp.Children.Add(new TextBlock { Text = "Os timers de boot estão no padrão do Windows: ele escolhe sozinho o melhor relógio para este hardware.", Foreground = Success, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        else
        {
            sp.Children.Add(new TextBlock { Text = (alterados.Count == 1 ? "1 valor foi alterado" : $"{alterados.Count} valores foram alterados") + " por algum programa ou script (o padrão do Windows é não ter nenhum deles):", FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            foreach (var a in alterados)
            {
                var item = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                item.Children.Add(new TextBlock { Text = $"{a.Chave} = {a.Valor}", FontFamily = Mono, FontSize = 13 });
                item.Children.Add(new TextBlock { Text = a.Explicacao, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                sp.Children.Add(item);
            }
        }
        var botoes = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        if (alterados is { Count: > 0 }) botoes.Children.Add(Botao(Primario, "Voltar ao padrão do Windows", () => _ = RestaurarTimersAsync(alterados)));
        if (temBackup) botoes.Children.Add(Botao(Secundario, "Desfazer (regravar os valores anteriores)", () => _ = DesfazerTimersAsync()));
        if (botoes.Children.Count > 0) sp.Children.Add(botoes);
        sp.Children.Add(new TextBlock { Text = "Mudanças no boot só valem depois de reiniciar. Meça antes e depois na tela de DPC (latência) e no Benchmark.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        var cartao = new Border { Style = CardStyle, Child = sp };
        _timersHost.Children.Add(cartao);
        Movimento.Entrar(cartao, 6, 220);
    }

    async Task RestaurarTimersAsync(IReadOnlyList<AjusteTimer> alterados)
    {
        if (!Elevacao.EhAdmin()) { MessageBox.Show("Alterar a configuração de boot exige administrador.", "Elevação", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var bitlocker = await Task.Run(TimersBoot.BitLockerAtivo);
        var msg = "Voltar os timers de boot ao padrão do Windows?\n\n"
                  + string.Join("\n", alterados.Select(a => $"• remover {a.Chave} (hoje: {a.Valor})"))
                  + "\n\nOs valores atuais ficam salvos e o botão \"Desfazer\" os regrava. Vale depois de reiniciar."
                  + (bitlocker == true ? "\n\nATENÇÃO: o BitLocker está ativo neste disco. Mudar o boot pode fazer o Windows pedir a CHAVE DE RECUPERAÇÃO no próximo boot — tenha a chave em mãos (conta Microsoft > Dispositivos > Chaves de recuperação) antes de continuar."
                     : bitlocker is null ? "\n\nSe o BitLocker estiver ativo, o Windows pode pedir a chave de recuperação no próximo boot." : "");
        if (MessageBox.Show(msg, "Timers de boot", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (!await PontoAntes.GarantirAsync(this, "voltar os timers de boot ao padrão")) return;
        var r = await Task.Run(TimersBoot.RestaurarPadrao);
        NotificacoesStore.Padrao.Adicionar("Windows", r.Ok ? "Timers de boot no padrão do Windows" : "Falha ao mudar timers de boot", r.Mensagem);
        Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Timers no padrão" : "Falha nos timers", r.Mensagem);
        await MontarTimersAsync();
    }

    async Task DesfazerTimersAsync()
    {
        if (!Elevacao.EhAdmin()) { MessageBox.Show("Alterar a configuração de boot exige administrador.", "Elevação", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (MessageBox.Show("Regravar os valores de timer que existiam antes? Vale depois de reiniciar.", "Desfazer timers de boot",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var r = await Task.Run(TimersBoot.Desfazer);
        NotificacoesStore.Padrao.Adicionar("Windows", r.Ok ? "Timers de boot restaurados" : "Falha ao desfazer timers de boot", r.Mensagem);
        Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Timers restaurados" : "Falha", r.Mensagem);
        await MontarTimersAsync();
    }

    public WindowsView()
    {
        InitializeComponent();
        Montar();
    }

    void Montar()
    {
        var itens = Relatorio.Coletar();
        string Val(string n) => itens.FirstOrDefault(i => i.Nome == n)?.Valor ?? "—";

        Raiz.Children.Add(new TextBlock { Text = "Estado", Style = SectionStyle });
        var linhas = new StackPanel();
        linhas.Children.Add(Linha("Edição", Val("Windows")));
        linhas.Children.Add(Linha("Versão / build", Val("Versão / build")));
        linhas.Children.Add(Linha("Arquitetura", Val("Arquitetura")));
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = linhas });

        var cabIni = new DockPanel();
        DockPanel.SetDock(_contagemIni, Dock.Right);
        cabIni.Children.Add(_contagemIni);
        cabIni.Children.Add(new TextBlock { Text = "Programas que abrem com o Windows", Style = SectionStyle });
        Raiz.Children.Add(cabIni);
        Raiz.Children.Add(_inicializacaoHost);
        _ = MontarInicializacaoAsync();

        Raiz.Children.Add(new TextBlock { Text = "Timers de boot", Style = SectionStyle });
        Raiz.Children.Add(_timersHost);
        _ = MontarTimersAsync();

        Raiz.Children.Add(new TextBlock { Text = "Recomendações seguras", Style = SectionStyle });
        var rec = new StackPanel();
        rec.Children.Add(Item("Telemetria e privacidade", "Reduzir coleta de dados, ID de publicidade e histórico de atividades. · reversível, baixo risco"));
        rec.Children.Add(Item("Efeitos visuais", "Desligar animações/transparências para a interface responder mais rápido. · reversível, cosmético"));
        rec.Children.Add(Item("Game DVR / Game Bar", "Desligar a gravação em segundo plano reduz overhead em jogos. · reversível"));
        rec.Children.Add(Item("Plano de energia", "Preferir 'Alto desempenho' quando fizer sentido. · reversível"));
        rec.Children.Add(new TextBlock { Text = "Cada uma é aplicada em Ajustes, com prévia, uma mudança por vez e opção de desfazer; tudo fica na auditoria do Admin.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = rec });

        // Plano de energia "Sevenzin" (duplica o plano ativo + aplica os índices do original; fábrica intacta). Admin + reversível.
        Raiz.Children.Add(new TextBlock { Text = "Plano de energia SevenzinTwks", Style = SectionStyle });
        var en = new StackPanel();
        _energiaLbl.Foreground = Secondary;
        AtualizarEnergia();
        en.Children.Add(_energiaLbl);
        en.Children.Add(new TextBlock
        {
            Text = "Cria um plano chamado \"SevenzinTwks\" duplicando o seu plano atual — os planos de fábrica do Windows (Equilibrado, Alto desempenho) permanecem intactos. Exige administrador e é totalmente reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        botoes.Children.Add(Botao(Primario, "Criar e ativar \"SevenzinTwks\"", () => ExecutarEnergia(GestorEnergia.AplicarSevenzin,
            "Isto vai DUPLICAR o seu plano de energia ativo, nomear a cópia como \"SevenzinTwks\" e ativá-la. Os planos de fábrica do Windows continuam intactos e você pode voltar a qualquer momento em \"Restaurar padrão\".\n\nContinuar?")));
        botoes.Children.Add(Botao(Secundario, "Restaurar padrão", () => ExecutarEnergia(GestorEnergia.Restaurar,
            "Isto vai ativar o plano \"Equilibrado\" do Windows e remover os planos \"SevenzinTwks\" criados.\n\nContinuar?")));
        en.Children.Add(botoes);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = en });

        // Hibernação (valor literal do original: HibernateEnabled=0).
        Raiz.Children.Add(new TextBlock { Text = "Hibernação", Style = SectionStyle });
        var hib = new StackPanel();
        var hibStatus = new TextBlock { Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        AtualizarHibernacao(hibStatus);
        hib.Children.Add(hibStatus);
        hib.Children.Add(new TextBlock
        {
            Text = "O original desliga a hibernação (HibernateEnabled=0). Desligar libera o hiberfil.sys e evita gravar a RAM em disco; exige administrador e é reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        var botoesHib = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        botoesHib.Children.Add(Botao(Primario, "Desativar hibernação", () => ExecutarHibernacao(false, hibStatus)));
        botoesHib.Children.Add(Botao(Secundario, "Reativar hibernação", () => ExecutarHibernacao(true, hibStatus)));
        hib.Children.Add(botoesHib);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = hib });

        // Desinstalação assistida (Edge/OneDrive): instaladores OFICIAIS, confirmação explícita.
        Raiz.Children.Add(new TextBlock { Text = "Desinstalar Edge e OneDrive", Style = SectionStyle });
        var desinst = new StackPanel();
        desinst.Children.Add(new TextBlock
        {
            Text = "Usa os desinstaladores OFICIAIS da Microsoft (setup.exe do Edge, OneDriveSetup.exe do Windows). Cada ação pede confirmação explícita. O Edge pode ser reinstalado pelo site oficial; o OneDrive também.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        var desinstStatus = new TextBlock { Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var botoesDes = new StackPanel { Orientation = Orientation.Horizontal };
        botoesDes.Children.Add(Botao(Primario, "Desinstalar Microsoft Edge", () => DesinstalarAsync("Microsoft Edge", DesinstaladorApps.DesinstalarEdgeAsync, desinstStatus)));
        botoesDes.Children.Add(Botao(Secundario, "Desinstalar OneDrive", () => DesinstalarAsync("OneDrive", DesinstaladorApps.DesinstalarOneDriveAsync, desinstStatus)));
        desinst.Children.Add(botoesDes);
        desinst.Children.Add(desinstStatus);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = desinst });

        Raiz.Children.Add(new TextBlock { Text = "Ativação do Windows (KMS corporativo)", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 10) });
        Raiz.Children.Add(CartaoAtivacao());
        Raiz.Children.Add(new TextBlock { Text = "DPC e interrupções", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 10) });
        Raiz.Children.Add(CartaoDpc());
        Raiz.Children.Add(new TextBlock { Text = "MSI e IRQ", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 10) });
        Raiz.Children.Add(CartaoMsiIrq());

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Salvaguardas", FontWeight = FontWeights.SemiBold, Foreground = Success });
        sp.Children.Add(new TextBlock
        {
            Text = "O SevenzinTwks não executa nada de forma oculta nem usa ativadores não-oficiais. Toda alteração no Windows explica o impacto antes, pede sua confirmação explícita e oferece reversão/auditoria (os itens avançados de segurança ficam isolados em Proteção/Ajustes).",
            Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
        });
        Raiz.Children.Add(new Border { Style = CardStyle, Child = sp });
    }

    Border CartaoAtivacao()
    {
        var sp = new StackPanel();
        var estado = new TextBlock { Text = "Consultando estado da licença…", Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        async void AtualizarAsync()
        {
            estado.Text = "Consultando estado da licença…";
            var (e, venc) = await System.Threading.Tasks.Task.Run(() => (Licenciamento.EstadoDetalhado(), Licenciamento.Vencimento()));
            var linhaVenc = venc.Length > 0 ? $"\n{venc}" : "";
            estado.Text = $"Estado: {(e.Estado.Length > 0 ? e.Estado : "—")} · Canal: {(e.Canal.Length > 0 ? e.Canal : "—")}{(e.KmsHost is null ? "" : $" · KMS: {e.KmsHost}")}{linhaVenc}";
        }
        AtualizarAsync();
        sp.Children.Add(estado);
        sp.Children.Add(new TextBlock { Text = "Gerenciamento legítimo de ativação (slmgr.vbs oficial) — sem crack nem servidor público. Informe o host KMS da sua organização (ex.: kms.empresa.local) para ativar via infraestrutura corporativa autorizada.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var hostBox = new TextBox { Style = (Style)Application.Current.Resources["DarkBox"], Width = 240, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 10, 0) };
        var botoes = new WrapPanel();
        botoes.Children.Add(Botao(Primario, "Configurar KMS", async () =>
        {
            var host = hostBox.Text.Trim();
            if (!Licenciamento.HostKmsValido(host))
            {
                Toasts.Mostrar(this, TipoToast.Aviso, "Host KMS inválido", "Informe um domínio ou IP corporativo válido (ex.: kms.empresa.local).");
                return;
            }
            if (!Elevacao.EhAdmin())
            {
                if (MessageBox.Show("Configurar KMS exige administrador.\n\nReabrir como administrador?", "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK) Elevacao.ReabrirComoAdmin();
                return;
            }
            if (MessageBox.Show($"Definir o servidor KMS corporativo como '{host}' e solicitar ativação (slmgr /skms + /ato)?\n\nReversível em 'Remover KMS' (/ckms).",
                    "Confirmação — KMS corporativo", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            var r = await System.Threading.Tasks.Task.Run(() => Licenciamento.ConfigurarKms(host));
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Windows", $"KMS configurado ({host})", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, "Ativação", r.Mensagem);
            AtualizarAsync();
        }));
        botoes.Children.Add(Botao(Secundario, "Remover KMS", async () =>
        {
            if (!Elevacao.EhAdmin())
            {
                if (MessageBox.Show("Remover o servidor KMS exige administrador.\n\nReabrir como administrador?", "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK) Elevacao.ReabrirComoAdmin();
                return;
            }
            if (MessageBox.Show("Limpar o servidor KMS configurado (slmgr /ckms) e voltar ao canal padrão do Windows?",
                    "Confirmação — Remover KMS", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            var r = await System.Threading.Tasks.Task.Run(() => Licenciamento.ConfigurarKms(""));
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Windows", "Servidor KMS removido", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, "Ativação", r.Mensagem);
            AtualizarAsync();
        }));
        botoes.Children.Add(Botao(Secundario, "Atualizar estado", AtualizarAsync));
        var linha = new WrapPanel();
        linha.Children.Add(hostBox);
        linha.Children.Add(botoes);
        sp.Children.Add(linha);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = sp };
    }

    Border CartaoDpc()
    {
        var sp = new StackPanel();
        var status = new TextBlock { Text = "Medindo carga de DPC/ISR…", Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        async void AtualizarAsync()
        {
            status.Text = "Medindo carga de DPC/ISR (250 ms)…";
            var m = await DpcDiagnostico.LerAsync(250);
            status.Text = m is null
                ? "Contador de DPC/ISR indisponível neste equipamento."
                : $"DPC {m.DpcPct:0.##}% · ISR {m.InterruptPct:0.##}% · total {m.TotalPct:0.##}% — carga {DpcDiagnostico.Classificar(m)}.";
        }
        AtualizarAsync();
        sp.Children.Add(status);
        sp.Children.Add(new TextBlock { Text = "Carga de DPC/interrupções (fração de CPU) — não é a latência em µs (exigiria ETW/LatencyMon, disponível em Instaladores). O SevenzinTwks não grava DpcWatchdogPeriod=0 (mascararia instabilidade de driver).", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        sp.Children.Add(Botao(Secundario, "Medir novamente", AtualizarAsync));
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = sp };
    }

    Border CartaoMsiIrq()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Estado real de MSI Mode (Message Signaled Interrupts) nos dispositivos PCI/PCIe e linhas de IRQ — somente leitura. Não altera MSISupported/DevicePriority (risco de BSOD/travamento sem teste por dispositivo).", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var hostLista = new StackPanel();
        hostLista.Children.Add(new TextBlock { Text = "Lendo dispositivos PCI e IRQs…", Foreground = Secondary, FontSize = 12 });
        sp.Children.Add(hostLista);

        async void CarregarAsync()
        {
            var (pciMsi, irqs) = await System.Threading.Tasks.Task.Run(() => (MsiIrq.ListarPciMsi(), MsiIrq.Listar()));
            hostLista.Children.Clear();
            if (pciMsi.Count > 0)
            {
                hostLista.Children.Add(new TextBlock { Text = $"Dispositivos PCI com Interrupt Management ({pciMsi.Count}):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 2, 0, 4) });
                foreach (var d in pciMsi.Take(25))
                {
                    var msiTxt = d.Msi switch { true => "MSI: Ativo", false => "MSI: Desativado (Line-based)", _ => "MSI: Padrão" };
                    var drv = string.IsNullOrEmpty(d.Driver) ? "" : $" · driver {d.Driver}";
                    hostLista.Children.Add(new TextBlock
                    {
                        Text = $"• {d.Nome} — {msiTxt} · Prioridade: {d.PrioridadeIrq ?? "Padrão"}{drv}",
                        Foreground = d.Msi == true ? Success : Secondary,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 2),
                    });
                }
            }
            if (irqs.Count > 0)
            {
                hostLista.Children.Add(new TextBlock { Text = $"Recursos de IRQ ({irqs.Count}):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 10, 0, 4) });
                foreach (var d in irqs.Take(20))
                    hostLista.Children.Add(new TextBlock { Text = $"IRQ {d.Irq?.ToString() ?? "?"} · {d.Nome}", Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
            }
            if (pciMsi.Count == 0 && irqs.Count == 0)
                hostLista.Children.Add(new TextBlock { Text = "Nenhum dispositivo de interrupção listado.", Foreground = Secondary, FontSize = 12 });
        }
        CarregarAsync();
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = sp };
    }

    static UIElement Linha(string rotulo, string valor)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = rotulo, Foreground = Muted });
        var v = new TextBlock { Text = valor, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }

    static UIElement Item(string titulo, string corpo)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        sp.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock { Text = corpo, Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        return sp;
    }

    static Button Botao(Style estilo, string texto, Action acao)
    {
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(12, 7, 12, 7), VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => acao();
        return b;
    }

    void AtualizarEnergia() => _energiaLbl.Text = $"Plano de energia ativo agora: {GestorEnergia.NomeAtivo() ?? "—"}";

    async void DesinstalarAsync(string nome, Func<CancellationToken, System.Threading.Tasks.Task<ResultadoDesinstalacao>> acao, TextBlock status)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show($"Desinstalar {nome} exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show($"Desinstalar {nome} usando o instalador OFICIAL?\n\n" +
                "A desinstalação pode afetar outros perfis do Windows nesta máquina." +
                (nome == "Microsoft Edge"
                    ? "\n\nSe a Microsoft recusar a remoção (bloqueio regional), o SevenzinTwks libera a remoção só durante a tentativa e desfaz a liberação logo em seguida."
                    : "") + "\n\nContinuar?",
                $"Confirmação — desinstalar {nome}", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        status.Foreground = Muted;
        status.Text = $"Desinstalando {nome}…";
        var r = await System.Threading.Tasks.Task.Run(() => acao(CancellationToken.None));
        status.Foreground = r.Ok ? Secondary : (Brush)Application.Current.Resources["DangerBrush"];
        status.Text = r.Ok ? $"✔ {nome}: {r.Detalhe}" : $"Falha: {r.Detalhe}";
        Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? $"{nome} desinstalado" : $"Falha ao desinstalar {nome}", r.Detalhe);
    }

    void ExecutarEnergia(Func<ResultadoEnergia> acao, string confirmar)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Alterar o plano de energia exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show(confirmar, "Plano de energia", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        var r = acao();
        AtualizarEnergia();
        Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Plano de energia aplicado" : "Falha no plano de energia", r.Mensagem);
        MessageBox.Show(r.Mensagem, "Plano de energia", MessageBoxButton.OK, r.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    void AtualizarHibernacao(TextBlock status) =>
        status.Text = $"Hibernação: {(GestorEnergia.HibernacaoAtiva() is { } h ? (h ? "ativada" : "desativada") : "indeterminada")}.";

    void ExecutarHibernacao(bool ativa, TextBlock status)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Alterar a hibernação exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show($"{(ativa ? "Reativar" : "Desativar")} a hibernação?\n\n" +
                "Desativar libera o arquivo hiberfil.sys e desabilita a inicialização rápida. É reversível.",
                "Hibernação", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        var r = GestorEnergia.DefinirHibernacao(ativa);
        AtualizarHibernacao(status);
        Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Hibernação alterada" : "Falha na hibernação", r.Mensagem);
        MessageBox.Show(r.Mensagem, "Hibernação", MessageBoxButton.OK, r.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
