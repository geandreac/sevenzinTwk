using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Assistente" (tela 12 do original — 03_telas §13): conversa por endpoint HTTPS configurado
/// PELO USUÁRIO. Envio explícito por mensagem, contexto do PC só com o checkbox marcado (consentimento por
/// envio). Sem endpoint = envio desligado. O histórico fica só nesta sessão (nada é persistido ou enviado
/// em segundo plano).</summary>
public partial class AssistenteView : UserControl
{
    readonly ConfigAssistenteStore _cfg = new();
    string? _endpoint;

    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];

    public AssistenteView()
    {
        InitializeComponent();
        Loaded += (_, _) => AtualizarEstado();
    }

    void AtualizarEstado()
    {
        var cfg = _cfg.Carregar();
        _endpoint = cfg.EnvioConfigurado ? cfg.EndpointUrl : null;
        BtnEnviar.IsEnabled = _endpoint is not null;
        IncluirContexto.IsChecked = cfg.IncluirContextoPadrao;
        StatusLbl.Text = _endpoint is null
            ? "Sem endpoint configurado — configure a URL em Configurações › Preferências para habilitar o envio."
            : $"Endpoint configurado ({_endpoint}). Cada mensagem é enviada só quando você aperta Enviar.";
    }

    void Mensagem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Enviar_Click(sender, e);
    }

    async void Enviar_Click(object sender, RoutedEventArgs e)
    {
        var texto = Mensagem.Text.Trim();
        if (texto.Length == 0 || _endpoint is null) return;
        Mensagem.Text = "";

        AdicionarBalao("Você", texto, alinhadoDireita: true);
        var envio = AdicionarBalao("Assistente", "enviando…", alinhadoDireita: false);
        BtnEnviar.IsEnabled = false;

        string? contexto = null;
        if (IncluirContexto.IsChecked == true)
        {
            var itens = await System.Threading.Tasks.Task.Run(Relatorio.Coletar);
            contexto = Assistente.ContextoPc(itens);
        }

        var resposta = await System.Threading.Tasks.Task.Run(() => Assistente.EnviarMensagemAsync(texto, contexto, _endpoint!));
        envio.Text = resposta ?? "(sem resposta ou falha de rede/servidor — nada foi enviado em segundo plano)";
        envio.Foreground = resposta is null ? (Brush)Application.Current.Resources["DangerBrush"] : Brushes.Transparent;

        BtnEnviar.IsEnabled = _endpoint is not null;
    }

    TextBlock AdicionarBalao(string autor, string texto, bool alinhadoDireita)
    {
        var sp = new StackPanel { HorizontalAlignment = alinhadoDireita ? HorizontalAlignment.Right : HorizontalAlignment.Left, MaxWidth = 560, Margin = new Thickness(0, 4, 0, 4) };
        var rotulo = new TextBlock { Text = autor, Foreground = Muted, FontSize = 11, HorizontalAlignment = alinhadoDireita ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        var balao = new Border
        {
            Background = (Brush)Application.Current.Resources[alinhadoDireita ? "BrandTintBrush" : "Surface2Brush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 2, 0, 0),
        };
        var corpo = new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        balao.Child = corpo;
        sp.Children.Add(rotulo);
        sp.Children.Add(balao);
        Conversa.Children.Add(sp);
        return corpo;
    }
}
