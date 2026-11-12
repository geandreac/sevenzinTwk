using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Notificações" (paridade com a tela 18 do original — notificações e tarefas agendadas,
/// aqui reconstruídas como histórico LOCAL real + a fila persistida de instaladores).</summary>
public partial class NotificacoesView : UserControl
{
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];

    readonly FilaInstaladores _fila = new();

    public NotificacoesView()
    {
        InitializeComponent();
        Loaded += (_, _) => Montar();
    }

    void Montar()
    {
        Lista.Children.Clear();
        Lista.Children.Add(new TextBlock { Text = "HISTÓRICO DE ATIVIDADE", Style = SectionStyle });
        var notificacoes = NotificacoesStore.Padrao.Todos;
        if (notificacoes.Count == 0)
        {
            Lista.Children.Add(new TextBlock { Text = "Nada ainda. Tweaks aplicados, instalações e capturas aparecem aqui.", Foreground = Muted, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });
        }
        foreach (var n in notificacoes)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = n.Titulo, FontSize = 14, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock
            {
                Text = $"{n.Origem} · {n.Utc.ToLocalTime():dd/MM HH:mm:ss}",
                Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 2, 0, 0),
            });
            sp.Children.Add(new TextBlock { Text = n.Detalhe, Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
            Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 10), Child = sp });
        }

        Lista.Children.Add(new TextBlock { Text = "FILA DE INSTALADORES (salva localmente)", Style = SectionStyle, Margin = new Thickness(0, 8, 0, 0) });
        var fila = _fila.Todos.OrderByDescending(i => i.AtualizadoUtc).ToList();
        if (fila.Count == 0)
        {
            Lista.Children.Add(new TextBlock { Text = "Fila vazia — use a tela Instaladores.", Foreground = Muted, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });
            return;
        }
        foreach (var i in fila)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = i.Nome, FontSize = 14, FontWeight = FontWeights.SemiBold });
            var estado = i.Estado switch
            {
                EstadoFila.Concluido => "✔ concluído",
                EstadoFila.Falhou => "✖ falhou",
                EstadoFila.Baixando => "baixando…",
                EstadoFila.Verificando => "verificando…",
                _ => "pendente",
            };
            sp.Children.Add(new TextBlock
            {
                Text = estado + (i.Detalhe is { } d ? $" — {d}" : "") + (i.AtualizadoUtc is { } t ? $" · {t.ToLocalTime():dd/MM HH:mm}" : ""),
                Foreground = i.Estado == EstadoFila.Concluido ? Success : i.Estado == EstadoFila.Falhou ? Danger : Muted,
                FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap,
            });
            Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 10), Child = sp });
        }
    }

    void Limpar_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Limpar o histórico de notificações? (a fila de instaladores é mantida)", "Limpar histórico",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        NotificacoesStore.Padrao.Limpar();
        Montar();
    }
}
