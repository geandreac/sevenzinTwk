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

        Raiz.Children.Add(new TextBlock { Text = "ESTADO (SOMENTE LEITURA)", Style = SectionStyle });
        var linhas = new StackPanel();
        linhas.Children.Add(Linha("Edição", Val("Windows")));
        linhas.Children.Add(Linha("Versão / build", Val("Versão / build")));
        linhas.Children.Add(Linha("Arquitetura", Val("Arquitetura")));
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = linhas });

        Raiz.Children.Add(new TextBlock { Text = "RECOMENDAÇÕES SEGURAS (REVERSÍVEIS, EM AJUSTES)", Style = SectionStyle });
        var rec = new StackPanel();
        rec.Children.Add(Item("Telemetria e privacidade", "Reduzir coleta de dados, ID de publicidade e histórico de atividades. · reversível, baixo risco"));
        rec.Children.Add(Item("Efeitos visuais", "Desligar animações/transparências para a interface responder mais rápido. · reversível, cosmético"));
        rec.Children.Add(Item("Game DVR / Game Bar", "Desligar a gravação em segundo plano reduz overhead em jogos. · reversível"));
        rec.Children.Add(Item("Plano de energia", "Preferir 'Alto desempenho' quando fizer sentido. · reversível"));
        rec.Children.Add(new TextBlock { Text = "Cada uma é aplicada em Ajustes, com prévia, uma mudança por vez e opção de desfazer; tudo fica na auditoria do Admin.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = rec });

        // Plano de energia "Sevenzin" (duplica o plano ativo; os planos de fábrica continuam intactos). Admin + reversível.
        Raiz.Children.Add(new TextBlock { Text = "PLANO DE ENERGIA SEVENZIN", Style = SectionStyle });
        var en = new StackPanel();
        _energiaLbl.Foreground = Secondary;
        AtualizarEnergia();
        en.Children.Add(_energiaLbl);
        en.Children.Add(new TextBlock
        {
            Text = "Cria um plano chamado \"Sevenzin\" duplicando o seu plano atual — os planos de fábrica do Windows (Equilibrado, Alto desempenho) permanecem intactos. Exige administrador e é totalmente reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        botoes.Children.Add(Botao(Primario, "Criar e ativar \"Sevenzin\"", () => ExecutarEnergia(GestorEnergia.AplicarSevenzin,
            "Isto vai DUPLICAR o seu plano de energia ativo, nomear a cópia como \"Sevenzin\" e ativá-la. Os planos de fábrica do Windows continuam intactos e você pode voltar a qualquer momento em \"Restaurar padrão\".\n\nContinuar?")));
        botoes.Children.Add(Botao(Secundario, "Restaurar padrão", () => ExecutarEnergia(GestorEnergia.Restaurar,
            "Isto vai ativar o plano \"Equilibrado\" do Windows e remover os planos \"Sevenzin\" criados.\n\nContinuar?")));
        en.Children.Add(botoes);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = en });

        // Desinstalação assistida (Edge/OneDrive): instaladores OFICIAIS, confirmação explícita.
        Raiz.Children.Add(new TextBlock { Text = "DESINSTALAÇÃO ASSISTIDA (EDGE / ONEDRIVE)", Style = SectionStyle });
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

        // Análise de imagem do Windows (SÓ LEITURA): monta a ISO, lista edições/apps/features via DISM.
        Raiz.Children.Add(new TextBlock { Text = "ANÁLISE DE IMAGEM DO WINDOWS (SÓ LEITURA)", Style = SectionStyle });
        var analise = new StackPanel();
        var analiseStatus = new TextBlock { Foreground = Secondary, TextWrapping = TextWrapping.Wrap };
        var analisarBtn = Botao(Primario, "Escolher ISO e analisar", () => { });
        analisarBtn.Click += async (_, _) => await AnalisarIsoAsync(analiseStatus, analisarBtn);
        analise.Children.Add(new TextBlock
        {
            Text = "Lê o que há dentro de uma ISO do Windows: edições, apps provisionados e recursos opcionais. A ISO é montada só para leitura e desmontada ao final — nada é alterado.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        analise.Children.Add(analisarBtn);
        analise.Children.Add(analiseStatus);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = analise });

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Salvaguardas", FontWeight = FontWeights.SemiBold, Foreground = Success });
        sp.Children.Add(new TextBlock { Text = "O Sevenzin não constrói ISO do Windows, não remove apps em massa, não desliga o Defender/UAC/SmartScreen/Windows Update e não executa nada de forma oculta. Mudanças exigem sua confirmação e são reversíveis.", Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Raiz.Children.Add(new Border { Style = CardStyle, Child = sp });
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
            if (MessageBox.Show($"Desinstalar {nome} exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show($"Desinstalar {nome} usando o instalador OFICIAL?\n\n" +
                "A desinstalação pode afetar outros perfis do Windows nesta máquina.\n\nContinuar?",
                $"Confirmação — desinstalar {nome}", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        status.Foreground = Muted;
        status.Text = $"Desinstalando {nome}…";
        var r = await System.Threading.Tasks.Task.Run(() => acao(CancellationToken.None));
        status.Foreground = r.Ok ? Secondary : (Brush)Application.Current.Resources["DangerBrush"];
        status.Text = r.Ok ? $"✔ {nome}: {r.Detalhe}" : $"Falha: {r.Detalhe}";
    }

    async System.Threading.Tasks.Task AnalisarIsoAsync(TextBlock status, Button botao)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("A análise monta a ISO (somente leitura) e exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Escolha a ISO do Windows", Filter = "Imagem ISO (*.iso)|*.iso" };
        if (dlg.ShowDialog() != true) return;

        botao.IsEnabled = false;
        status.Foreground = Muted;
        status.Text = "Analisando… (monta a ISO só para leitura e executa o DISM; pode demorar)";
        var resultado = await System.Threading.Tasks.Task.Run(() => ExecutorDism.AnalisarIsoAsync(dlg.FileName));
        botao.IsEnabled = true;

        if (!resultado.Ok)
        {
            status.Foreground = (Brush)Application.Current.Resources["DangerBrush"];
            status.Text = "Falha na análise: " + resultado.Erro;
            return;
        }
        status.Foreground = Secondary;
        status.Text = resultado.Edicoes.Count == 0
            ? "Nenhuma edição encontrada na imagem."
            : $"Edições: {string.Join(" · ", resultado.Edicoes.Select(e => $"{e.Nome} ({e.Arquitetura ?? "arch n/d"})"))}\n" +
              $"Apps provisionados: {resultado.Apps.Count} · Recursos opcionais: {resultado.Features.Count}\n" +
              "Só leitura — nada foi alterado na ISO nem no sistema.";
    }

    void ExecutarEnergia(Func<ResultadoEnergia> acao, string confirmar)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Alterar o plano de energia exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show(confirmar, "Plano de energia", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        var r = acao();
        AtualizarEnergia();
        MessageBox.Show(r.Mensagem, "Plano de energia", MessageBoxButton.OK, r.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
