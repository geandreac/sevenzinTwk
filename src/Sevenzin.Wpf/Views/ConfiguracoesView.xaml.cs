using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Configurações" (Figma) com abas. Só controles reais: "Iniciar com o Windows" grava
/// a chave Run do HKCU (reversível, sem admin). Toggles de bandeja ficam desabilitados até existir o
/// ícone de bandeja. Sem login, Discord ID ou validação de licença (removidos na reconstrução segura).</summary>
public partial class ConfiguracoesView : UserControl
{
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "Sevenzin";
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");

    Border[] _tabs = Array.Empty<Border>();
    StackPanel[] _paineis = Array.Empty<StackPanel>();

    public ConfiguracoesView()
    {
        InitializeComponent();
        _tabs = new[] { TabGeral, TabCapturas, TabPref };
        _paineis = new[] { GeralPanel, CapturasPanel, PrefPanel };
        SwInicio.IsChecked = StartupLigado();
        PastaTxt.Text = Pasta;
        VersaoTxt.Text = "Sevenzin Program · reconstrução segura (WPF)";
        var cfg = new Sevenzin.Diagnostics.ConfigCapturasStore().Carregar();
        WebhookTxt.Text = cfg.WebhookUrl ?? "";
        AtualizarStatusWebhook();
        var cfgAssistente = new Sevenzin.Diagnostics.ConfigAssistenteStore().Carregar();
        AssistenteTxt.Text = cfgAssistente.EndpointUrl ?? "";
        AtualizarStatusAssistente();
        SelecionarAba(0);
    }

    void AtualizarStatusAssistente()
    {
        var url = AssistenteTxt.Text.Trim();
        AssistenteStatus.Text = Sevenzin.Diagnostics.Assistente.UrlEndpointValida(url)
            ? "Endpoint configurado — a tela Assistente envia cada mensagem só quando você aperta Enviar."
            : "Nenhum endpoint configurado — a tela Assistente fica com o envio desligado.";
    }

    void AssistenteSalvar_Click(object sender, RoutedEventArgs e)
    {
        var url = AssistenteTxt.Text.Trim();
        if (url.Length == 0)
        {
            new Sevenzin.Diagnostics.ConfigAssistenteStore().Salvar(new Sevenzin.Diagnostics.ConfigAssistente(null));
            AtualizarStatusAssistente();
            return;
        }
        if (!Sevenzin.Diagnostics.Assistente.UrlEndpointValida(url))
        {
            MessageBox.Show("Use uma URL HTTPS absoluta.", "URL inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new Sevenzin.Diagnostics.ConfigAssistenteStore().Salvar(new Sevenzin.Diagnostics.ConfigAssistente(url));
        AtualizarStatusAssistente();
    }

    void AssistenteLimpar_Click(object sender, RoutedEventArgs e)
    {
        AssistenteTxt.Text = "";
        new Sevenzin.Diagnostics.ConfigAssistenteStore().Salvar(new Sevenzin.Diagnostics.ConfigAssistente(null));
        AtualizarStatusAssistente();
    }

    void AtualizarStatusWebhook()
    {
        var url = WebhookTxt.Text.Trim();
        WebhookStatus.Text = Sevenzin.Diagnostics.EnvioCaptura.UrlWebhookValida(url)
            ? $"Webhook configurado — o botão Enviar fica disponível na tela Capturas (cada envio pede confirmação)."
            : "Nenhum webhook configurado — capturas ficam só no seu PC.";
    }

    void WebhookSalvar_Click(object sender, RoutedEventArgs e)
    {
        var url = WebhookTxt.Text.Trim();
        if (url.Length == 0)
        {
            new Sevenzin.Diagnostics.ConfigCapturasStore().Salvar(new Sevenzin.Diagnostics.ConfigCaptura(null));
            AtualizarStatusWebhook();
            return;
        }
        if (!Sevenzin.Diagnostics.EnvioCaptura.UrlWebhookValida(url))
        {
            MessageBox.Show("Use uma URL HTTPS absoluta (ex.: https://hooks.exemplo.com/...).", "URL inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new Sevenzin.Diagnostics.ConfigCapturasStore().Salvar(new Sevenzin.Diagnostics.ConfigCaptura(url));
        AtualizarStatusWebhook();
    }

    void WebhookLimpar_Click(object sender, RoutedEventArgs e)
    {
        WebhookTxt.Text = "";
        new Sevenzin.Diagnostics.ConfigCapturasStore().Salvar(new Sevenzin.Diagnostics.ConfigCaptura(null));
        AtualizarStatusWebhook();
    }

    void Tab_Click(object sender, MouseButtonEventArgs e) => SelecionarAba(int.Parse((string)((Border)sender).Tag));

    void SelecionarAba(int i)
    {
        for (int k = 0; k < _tabs.Length; k++)
        {
            bool ativo = k == i;
            _tabs[k].BorderBrush = ativo ? (Brush)Application.Current.Resources["Brand500Brush"] : Brushes.Transparent;
            if (_tabs[k].Child is TextBlock t)
            {
                t.Foreground = (Brush)Application.Current.Resources[ativo ? "TextPrimaryBrush" : "TextMutedBrush"];
                t.FontWeight = ativo ? FontWeights.SemiBold : FontWeights.Normal;
            }
            _paineis[k].Visibility = ativo ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    static bool StartupLigado()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunPath);
        return k?.GetValue(RunName) != null;
    }

    void Inicio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
            if (SwInicio.IsChecked == true) k.SetValue(RunName, $"\"{Environment.ProcessPath}\"", RegistryValueKind.String);
            else k.DeleteValue(RunName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            SwInicio.IsChecked = StartupLigado();
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void AbrirPasta_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Pasta}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
