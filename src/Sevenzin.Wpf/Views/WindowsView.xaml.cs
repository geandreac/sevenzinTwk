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
}
