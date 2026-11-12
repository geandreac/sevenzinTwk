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
        botoes.Children.Add(Botao(Primario, "Criar e ativar \"Sevenzin\"", () => ExecutarEnergia(GestorEnergia.AplicarSevenzin)));
        botoes.Children.Add(Botao(Secundario, "Restaurar padrão", () => ExecutarEnergia(GestorEnergia.Restaurar)));
        en.Children.Add(botoes);
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = en });

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

    void ExecutarEnergia(Func<ResultadoEnergia> acao)
    {
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Alterar o plano de energia exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        var r = acao();
        AtualizarEnergia();
        MessageBox.Show(r.Mensagem, "Plano de energia", MessageBoxButton.OK, r.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
