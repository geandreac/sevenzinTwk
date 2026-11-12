using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Capturas" (§21.2): captura de tela local, salva em pasta no PC e nunca enviada. Sem rede.</summary>
public partial class CapturasView : UserControl
{
    public CapturasView() => InitializeComponent();

    async void Capturar_Click(object sender, RoutedEventArgs e)
    {
        BtnCapturar.IsEnabled = false;
        var win = Window.GetWindow(this);
        var estado = win?.WindowState ?? WindowState.Normal;
        try
        {
            if (win is not null) win.WindowState = WindowState.Minimized; // não aparecer na imagem
            await Task.Delay(400);
            var caminho = await Task.Run(() => CapturaTela.CapturarTelaPrincipal());
            MostrarCaptura(caminho);
        }
        catch (Exception ex)
        {
            StatusLbl.Text = "Falha na captura: " + ex.Message;
        }
        finally
        {
            if (win is not null) { win.WindowState = estado; win.Activate(); }
            BtnCapturar.IsEnabled = true;
        }
    }

    void MostrarCaptura(string caminho)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;   // não trava o arquivo
        img.UriSource = new Uri(caminho);
        img.EndInit();
        Thumb.Source = img;
        Thumb.Visibility = Visibility.Visible;
        Placeholder.Visibility = Visibility.Collapsed;
        StatusLbl.Text = $"Salvo em {caminho} · nada foi enviado.";
    }

    void AbrirPasta_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(CapturaTela.PastaCapturas);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CapturaTela.PastaCapturas}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
