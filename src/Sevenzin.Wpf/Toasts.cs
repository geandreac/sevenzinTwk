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
    readonly Queue<FilaToasts.Item> _espera = new();
    readonly HashSet<Guid> _pausados = new();

    public ToastHost()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 52, 16, 0);
        MaxWidth = 380;
        IsHitTestVisible = false; // cliques passam pelos vazios; cada toast é clicável
        Focusable = true;
        _tick.Tick += (_, _) => Tick();
        _tick.Start();
    }

    public void Adicionar(TipoToast tipo, string titulo, string mensagem, Action? desfazer = null)
    {
        var item = _fila.Adicionar(tipo, titulo, mensagem);
        if (item is null) return; // dedup: evento repetido em < 1 s

        if (Children.Count >= MaxVisiveis) { _espera.Enqueue(item); return; }
        Exibir(item, desfazer);
    }

    void Exibir(FilaToasts.Item item, Action? desfazer)
    {
        var card = CriarCard(item, desfazer);
        _visuais[item.Id] = card;
        Children.Add(card);
        if (SystemParameters.ClientAreaAnimation)
        {
            card.Opacity = 0;
            card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }
    }

    void Tick()
    {
        foreach (var expirado in _fila.RemoverExpirados(DateTime.UtcNow, FilaToasts.DuracaoPadrao, _pausados))
            RemoverComAnimacao(expirado.Id);

        // promove da fila de espera enquanto houver vaga
        while (Children.Count < MaxVisiveis && _espera.Count > 0)
        {
            var proximo = _espera.Dequeue();
            if (_fila.Itens.Any(i => i.Id == proximo.Id)) Exibir(proximo, null); // item ainda vivo
        }
    }

    void RemoverComAnimacao(Guid id)
    {
        if (!_visuais.Remove(id, out var card)) return;
        _pausados.Remove(id);
        if (SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fade.Completed += (_, _) => Children.Remove(card);
            card.BeginAnimation(OpacityProperty, fade);
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

    Border CriarCard(FilaToasts.Item item, Action? desfazer)
    {
        var (corBorda, corFundo, icone) = item.Tipo switch
        {
            TipoToast.Sucesso => ("SuccessBrush", "SuccessBg", "✓ "),
            TipoToast.Erro => ("DangerBrush", "DangerBg", "✕ "),
            TipoToast.Aviso => ("WarningBrush", "WarningBg", "⚠ "),
            _ => ("AccentCyanBrush", "AccentCyanBg", "ℹ "),
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = icone + item.Titulo,
            Foreground = (Brush)Application.Current.Resources[corBorda],
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });
        sp.Children.Add(new TextBlock
        {
            Text = item.Mensagem,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        var card = new Border
        {
            Background = new SolidColorBrush((Color)Application.Current.Resources[corFundo]),
            BorderBrush = (Brush)Application.Current.Resources[corBorda],
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
            var link = new TextBlock
            {
                Text = "Desfazer",
                Foreground = (Brush)Application.Current.Resources["Brand400Brush"],
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 6, 0, 0),
            };
            link.MouseLeftButtonDown += (_, e) => { e.Handled = true; desfazer(); Dispensar(item); };
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
    public static void Mostrar(FrameworkElement origem, TipoToast tipo, string titulo, string mensagem, Action? desfazer = null)
    {
        // Todo erro mostrado também fica em Notificações (o toast some em 3 s). Se a tela já registrou o mesmo erro
        // com título próprio, AdicionarSeNovo não duplica. Registrar nunca pode impedir o aviso.
        if (tipo == TipoToast.Erro)
            try { NotificacoesStore.Padrao.AdicionarSeNovo("Erro", titulo, mensagem); } catch { }
        (Window.GetWindow(origem) as MainWindow)?.ToastHost.Adicionar(tipo, titulo, mensagem, desfazer);
    }
}
