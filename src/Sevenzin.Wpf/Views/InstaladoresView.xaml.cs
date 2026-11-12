using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Instaladores" (§21.5/21.7): catálogo de utilitários de fonte oficial. O download é
/// verificado (assinatura + hash quando fixado) antes de permitir executar, e nada roda sem confirmação.</summary>
public partial class InstaladoresView : UserControl
{
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "instaladores");

    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];

    public InstaladoresView()
    {
        InitializeComponent();
        foreach (var u in CatalogoUtilitarios.Todos) Lista.Children.Add(Cartao(u));
    }

    Border Cartao(Utilitario u)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = u.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock { Text = u.Descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        var publisher = u.PublisherEsperado is { } p ? $"Assinatura esperada: {p}" : "Assinatura exigida na verificação.";
        sp.Children.Add(new TextBlock { Text = publisher, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });

        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var abrir = new Button { Style = Secundario, Content = "Abrir página oficial", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
        abrir.Click += (_, _) => AbrirUrl(u.UrlOficial);
        acoes.Children.Add(abrir);

        var executar = new Button { Style = Primario, Content = "Executar instalador", Padding = new Thickness(12, 7, 12, 7), Visibility = Visibility.Collapsed };
        if (u.UrlDownload is not null)
        {
            var baixar = new Button { Style = Primario, Content = "Baixar e verificar", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
            baixar.Click += async (_, _) => await BaixarEVerificar(u, status, baixar, executar);
            acoes.Children.Add(baixar);
            executar.Click += (_, _) => Executar((string?)executar.Tag);
            acoes.Children.Add(executar);
        }

        sp.Children.Add(acoes);
        sp.Children.Add(status);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    async Task BaixarEVerificar(Utilitario u, TextBlock status, Button baixar, Button executar)
    {
        baixar.IsEnabled = false;
        executar.Visibility = Visibility.Collapsed;
        status.Visibility = Visibility.Visible;
        status.Foreground = Muted;
        try
        {
            Directory.CreateDirectory(Pasta);
            var destino = Path.Combine(Pasta, Path.GetFileName(new Uri(u.UrlDownload!).LocalPath));
            var prog = new Progress<double>(p => status.Text = $"Baixando… {p:P0}");
            await DownloaderInstalador.BaixarAsync(u.UrlDownload!, destino, prog);

            var r = VerificadorDownload.Verificar(destino, u);
            if (r == ResultadoVerificacao.Ok)
            {
                status.Text = "✔ Verificado (assinatura confere). Você pode executar.";
                status.Foreground = Success;
                executar.Tag = destino;
                executar.Visibility = Visibility.Visible;
            }
            else
            {
                status.Text = $"✖ Verificação falhou: {Motivo(r)}. O arquivo NÃO será oferecido para execução.";
                status.Foreground = Danger;
                try { File.Delete(destino); } catch { }
            }
        }
        catch (Exception ex)
        {
            status.Text = "Falha no download: " + ex.Message;
            status.Foreground = Danger;
        }
        finally { baixar.IsEnabled = true; }
    }

    static string Motivo(ResultadoVerificacao r) => r switch
    {
        ResultadoVerificacao.HashNaoConfere => "o hash não confere",
        ResultadoVerificacao.SemAssinatura => "o arquivo não está assinado",
        ResultadoVerificacao.PublisherDiferente => "o publisher da assinatura não é o esperado",
        _ => "desconhecido",
    };

    void Executar(string? caminho)
    {
        if (caminho is null || !File.Exists(caminho)) return;
        if (MessageBox.Show($"Executar o instalador verificado?\n\n{caminho}", "Confirmar execução", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro ao executar", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    static void AbrirUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro ao abrir", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
