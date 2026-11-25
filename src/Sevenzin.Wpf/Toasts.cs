using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Anfitrião visual dos toasts (F9): overlay no canto superior direito, sobre o conteúdo, sem
/// deslocar o layout. Máximo de 4 visíveis (excedente enfileirado), dedup de evento repetido em < 1 s,
/// 3 s de exibição com saída fade+slide (200–250 ms) ou sumiço simples com "reduzir movimento".
/// Pausa no hover/foco, dismiss por Esc, botão "Desfazer" quando a ação é reversível. Acessibilidade:
/// aria-live polite (assertive para erro), role=status, contraste do tema.</summary>
public sealed class ToastHost : StackPanel
{
    const int MaxVisiveis = 4;

    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly FilaToasts _fila = new();
    readonly Dictionary<Guid, Border> _visuais = new();
    readonly Queue<(FilaToasts.Item Item, Action? Acao, string Rotulo)> _espera = new();
    readonly HashSet<Guid> _pausados = new();

    public ToastHost()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 52, 16, 0);
        MaxWidth = 380;
        // Sem Background, os vazios deixam o clique passar; IsHitTestVisible=false bloquearia também os avisos.
        Focusable = true;
        _tick.Tick += (_, _) => Tick();
        _tick.Start();
    }

    public void Adicionar(TipoToast tipo, string titulo, string mensagem, Action? desfazer = null, string rotulo = "Desfazer")
    {
        var item = _fila.Adicionar(tipo, titulo, mensagem);
        if (item is null) return; // dedup: evento repetido em < 1 s

        if (Children.Count >= MaxVisiveis) { _espera.Enqueue((item, desfazer, rotulo)); return; }
        Exibir(item, desfazer, rotulo);
    }

    void Exibir(FilaToasts.Item item, Action? desfazer, string rotulo)
    {
        var card = CriarCard(item, desfazer, rotulo);
        _visuais[item.Id] = card;
        Children.Add(card);
        Movimento.Viajar(card, 28, 0);
        if (Movimento.Ativo) card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    void Tick()
    {
        foreach (var expirado in _fila.RemoverExpirados(DateTime.UtcNow, FilaToasts.DuracaoPadrao, _pausados))
            RemoverComAnimacao(expirado.Id);

        // promove da fila de espera enquanto houver vaga
        while (Children.Count < MaxVisiveis && _espera.Count > 0)
        {
            var proximo = _espera.Dequeue();
            if (_fila.Itens.Any(i => i.Id == proximo.Item.Id)) Exibir(proximo.Item, proximo.Acao, proximo.Rotulo); // item ainda vivo
        }
    }

    void RemoverComAnimacao(Guid id)
    {
        if (!_visuais.Remove(id, out var card)) return;
        _pausados.Remove(id);
        if (SystemParameters.ClientAreaAnimation)
        {
            Movimento.Sair(card, () =>
            {
                // os de baixo sobem até o lugar vago em vez de pular
                int i = Children.IndexOf(card);
                double altura = card.ActualHeight + card.Margin.Bottom;
                Children.Remove(card);
                for (; i >= 0 && i < Children.Count; i++) Movimento.Viajar(Children[i], 0, altura, 220);
            });
        }
        else
        {
            Children.Remove(card); // reduzir movimento: desaparecimento simples
        }
    }

    void Dispensar(FilaToasts.Item item)
    {
        _fila.Remover(item.Id);
        RemoverComAnimacao(item.Id);
    }

    Border CriarCard(FilaToasts.Item item, Action? desfazer, string rotulo)
    {
        var (corBorda, corFundo, icone) = item.Tipo switch
        {
            TipoToast.Sucesso => ("SuccessBrush", "SuccessBg", "M0,4 L3.5,7.5 L10,0"),
            TipoToast.Erro => ("DangerBrush", "DangerBg", "M0,0 L9,9 M9,0 L0,9"),
            TipoToast.Aviso => ("WarningBrush", "WarningBg", "M5,0 V6.5 M5,9.6 V9.7"),
            _ => ("AccentCyanBrush", "AccentCyanBg", "M5,0 V0.1 M5,3.4 V10"),
        };

        var sp = new StackPanel();
        var topo = new DockPanel();
        topo.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(icone), Stroke = (Brush)Application.Current.Resources[corBorda], StrokeThickness = 1.8, Width = 12, Height = 12,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0),
        });
        topo.Children.Add(new TextBlock
        {
            Text = item.Titulo,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });
        sp.Children.Add(topo);
        sp.Children.Add(new TextBlock
        {
            Text = item.Mensagem,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(20, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        var card = new Border
        {
            Background = new SolidColorBrush((Color)Application.Current.Resources[corFundo]),
            BorderBrush = (Brush)Application.Current.Resources["BorderStrongBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 8),
            Child = sp,
            IsHitTestVisible = true,
            Focusable = true,
        };

        if (desfazer is not null)
        {
            var link = new Button
            {
                Content = rotulo,
                Style = (Style)Application.Current.Resources["LinkButton"],
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(14, 2, 0, -4),
            };
            link.Click += (_, _) => { Dispensar(item); desfazer(); };
            sp.Children.Add(link);
        }

        // Pausa do timer no hover/foco; Esc dispensa; clique no corpo também dispensa.
        card.MouseEnter += (_, _) => _pausados.Add(item.Id);
        card.MouseLeave += (_, _) => _pausados.Remove(item.Id);
        card.GotKeyboardFocus += (_, _) => _pausados.Add(item.Id);
        card.LostKeyboardFocus += (_, _) => _pausados.Remove(item.Id);
        card.KeyDown += (_, e) => { if (e.Key == Key.Escape) Dispensar(item); };
        card.MouseLeftButtonDown += (_, _) => Dispensar(item);

        AutomationProperties.SetLiveSetting(card, item.Tipo == TipoToast.Erro ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        AutomationProperties.SetName(card, item.Titulo + ". " + item.Mensagem);
        return card;
    }
}

/// <summary>Ponto de entrada das telas: dispara um toast na janela que hospeda o elemento.
/// <paramref name="desfazer"/> opcional — quando presente, o toast ganha o botão "Desfazer".</summary>
public static class Toasts
{
    public static void Mostrar(FrameworkElement origem, TipoToast tipo, string titulo, string mensagem, Action? desfazer = null, string rotulo = "Desfazer")
    {
        // Todo erro mostrado também fica em Notificações (o toast some em 3 s). Se a tela já registrou o mesmo erro
        // com título próprio, AdicionarSeNovo não duplica. Registrar nunca pode impedir o aviso.
        if (tipo == TipoToast.Erro)
            try { NotificacoesStore.Padrao.AdicionarSeNovo("Erro", titulo, mensagem); } catch { }
        (Window.GetWindow(origem) as MainWindow)?.ToastHost.Adicionar(tipo, titulo, mensagem, desfazer, rotulo);
    }
}
