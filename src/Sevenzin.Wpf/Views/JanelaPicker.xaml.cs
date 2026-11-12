using System.Windows;
using System.Windows.Controls;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Seletor de janela para o modo "capturar janela": lista as janelas visíveis com título
/// (EnumWindows) e devolve o hwnd escolhido, ou null se cancelado.</summary>
public partial class JanelaPicker : Window
{
    public nint HwndSelecionado { get; private set; }

    public JanelaPicker()
    {
        InitializeComponent();
        foreach (var j in CapturaTela.JanelasVisiveis().OrderBy(x => x.Titulo))
            Lista.Items.Add(new ListBoxItem { Content = j.Titulo, Tag = j.Hwnd });
    }

    void Lista_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        BtnOk.IsEnabled = Lista.SelectedItem is not null;

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is ListBoxItem item && item.Tag is nint hwnd) HwndSelecionado = hwnd;
        DialogResult = true;
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
