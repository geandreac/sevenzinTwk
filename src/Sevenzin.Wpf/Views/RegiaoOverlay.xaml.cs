using System.Drawing;
using System.Windows;
using System.Windows.Input;
using PointW = System.Windows.Point;

namespace Sevenzin.Wpf.Views;

/// <summary>Overlay de tela inteira para escolher a REGIÃO a capturar: arrasta um retângulo
/// (borda azul do tema), Esc cancela. Devolve os limites em coordenadas de tela.</summary>
public partial class RegiaoOverlay : Window
{
    PointW _inicio;
    bool _arrastando;
    bool _cancelado;

    /// <summary>Região escolhida (coordenadas de tela), ou null se cancelada.</summary>
    public Rectangle? Regiao { get; private set; }

    public RegiaoOverlay()
    {
        InitializeComponent();
        var virtualTela = new Rectangle(
            (int)SystemParameters.VirtualScreenLeft, (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
        Left = virtualTela.Left; Top = virtualTela.Top;
        Width = virtualTela.Width; Height = virtualTela.Height;
        PreviewMouseLeftButtonDown += MousePressionou;
        PreviewMouseMove += MouseMoveu;
        PreviewMouseLeftButtonUp += MouseSoltou;
    }

    void Janela_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _cancelado = true; Close(); }
    }

    void MousePressionou(object sender, MouseButtonEventArgs e)
    {
        _inicio = e.GetPosition(this);
        _arrastando = true;
        Selecao.Visibility = Visibility.Visible;
        Dica.Visibility = Visibility.Collapsed;
        AtualizarSelecao(_inicio);
    }

    void MouseMoveu(object sender, MouseEventArgs e)
    {
        if (!_arrastando) return;
        AtualizarSelecao(e.GetPosition(this));
    }

    void MouseSoltou(object sender, MouseButtonEventArgs e)
    {
        if (!_arrastando) return;
        _arrastando = false;
        var fim = e.GetPosition(this);
        int x1 = (int)Math.Min(_inicio.X, fim.X), y1 = (int)Math.Min(_inicio.Y, fim.Y);
        int x2 = (int)Math.Max(_inicio.X, fim.X), y2 = (int)Math.Max(_inicio.Y, fim.Y);
        var regiao = new Rectangle((int)Left + x1, (int)Top + y1, x2 - x1, y2 - y1);
        Close();
        Regiao = regiao.Width >= 8 && regiao.Height >= 8 ? regiao : null; // muito pequeno = cancelado
    }

    void AtualizarSelecao(PointW atual)
    {
        double x = Math.Min(_inicio.X, atual.X), y = Math.Min(_inicio.Y, atual.Y);
        double w = Math.Abs(atual.X - _inicio.X), h = Math.Abs(atual.Y - _inicio.Y);
        Selecao.Margin = new Thickness(x, y, 0, 0);
        Selecao.Width = w; Selecao.Height = h;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_cancelado) Regiao = null;
    }
}
