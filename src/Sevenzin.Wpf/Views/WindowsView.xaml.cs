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
