using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sevenzin.Diagnostics;
using PointD = System.Drawing.Point;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Capturas" completa (F2): modos tela inteira/monitor/região/janela (F2.1), editor de
/// anotações lápis/retângulo/seta/texto com desfazer (F2.2) e envio EXPLÍCITO para webhook configurado
/// pelo usuário (F2.3) — o padrão continua sendo salvar local, nada sai sem confirmação.</summary>
public partial class CapturasView : UserControl
{
    enum ModoCaptura { TelaInteira, Monitor, Regiao, Janela }

    readonly ConfigCapturasStore _cfg = new();
    readonly PilhaAnotacoes _pilha = new();
    Bitmap? _base;                    // captura original em resolução cheia (nunca alterada)
    BitmapImage? _previewAtual;       // prévia composta para a UI
    string? _ultimoCaminho;           // último PNG salvo
    TipoAnotacao _ferramenta = TipoAnotacao.Lapis;
    System.Drawing.Color _cor = System.Drawing.Color.FromArgb(255, 255, 77, 79); // vermelho do tema
    List<PointD>? _traco;
    PointD _pontoInicial;
    bool _desenhando;

    static System.Windows.Media.Brush Muted => (System.Windows.Media.Brush)Application.Current.Resources["TextMutedBrush"];

    public CapturasView()
    {
        InitializeComponent();
        ModoBox.Items.Add(new ComboBoxItem { Content = "Tela inteira", Tag = ModoCaptura.TelaInteira });
        ModoBox.Items.Add(new ComboBoxItem { Content = "Monitor", Tag = ModoCaptura.Monitor });
        ModoBox.Items.Add(new ComboBoxItem { Content = "Região (arrastar)", Tag = ModoCaptura.Regiao });
        ModoBox.Items.Add(new ComboBoxItem { Content = "Janela", Tag = ModoCaptura.Janela });
        ModoBox.SelectedIndex = 0;

        var monitores = CapturaTela.Monitores();
        for (int i = 0; i < monitores.Count; i++)
            MonitorBox.Items.Add(new ComboBoxItem { Content = $"Monitor {i + 1} — {monitores[i].Width}×{monitores[i].Height}", Tag = i });
        if (MonitorBox.Items.Count > 0) MonitorBox.SelectedIndex = 0;

        BtnEnviar.IsEnabled = _cfg.Carregar().EnvioConfigurado;
        if (!BtnEnviar.IsEnabled)
            BtnEnviar.ToolTip = "Configure a URL do webhook em Configurações › Capturas para habilitar o envio.";
    }

    // ── F2.1: capturar nos 4 modos ────────────────────────────────────────────────

    void Modo_Changed(object sender, SelectionChangedEventArgs e) =>
        MonitorBox.Visibility = ModoAtual == ModoCaptura.Monitor ? Visibility.Visible : Visibility.Collapsed;

    ModoCaptura ModoAtual => (ModoBox.SelectedItem as ComboBoxItem)?.Tag is ModoCaptura m ? m : ModoCaptura.TelaInteira;

    async void Capturar_Click(object sender, RoutedEventArgs e)
    {
        BtnCapturar.IsEnabled = false;
        var win = Window.GetWindow(this);
        var estado = win?.WindowState ?? WindowState.Normal;
        try
        {
            Bitmap? bmp = null;
            switch (ModoAtual)
            {
                case ModoCaptura.TelaInteira:
                    var area = CapturaTela.AreaTelaInteira();
                    await MinimizarAsync(win);
                    bmp = await Task.Run(() => CapturaTela.CapturarArea(area));
                    break;
                case ModoCaptura.Monitor:
                    var m = CapturaTela.Monitores();
                    int idx = (MonitorBox.SelectedItem as ComboBoxItem)?.Tag is int i ? Math.Clamp(i, 0, m.Count - 1) : 0;
                    var alvo = m[idx];
                    await MinimizarAsync(win);
                    bmp = await Task.Run(() => CapturaTela.CapturarArea(alvo));
                    break;
                case ModoCaptura.Regiao:
                    await MinimizarAsync(win);
                    var overlay = new RegiaoOverlay { Owner = win };
                    overlay.ShowDialog();
                    if (overlay.Regiao is { } r) bmp = await Task.Run(() => CapturaTela.CapturarArea(r));
                    break;
                case ModoCaptura.Janela:
                    var picker = new JanelaPicker { Owner = win };
                    if (picker.ShowDialog() == true && picker.HwndSelecionado != nint.Zero)
                        bmp = await Task.Run(() => CapturaTela.CapturarJanela(picker.HwndSelecionado));
                    break;
            }
            if (bmp is null) { StatusLbl.Text = "Captura cancelada ou a janela não estava disponível."; return; }
            DefinirBase(bmp);
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

    static async Task MinimizarAsync(Window? win)
    {
        if (win is null) { await Task.Delay(200); return; }
        win.WindowState = WindowState.Minimized;
        await Task.Delay(400); // deixa o desktop assentar antes de copiar os pixels
    }

    // ── F2.2: editor de anotações ─────────────────────────────────────────────────

    void DefinirBase(Bitmap bmp)
    {
        _base?.Dispose();
        _base = bmp;
        _pilha.Limpar();
        BarraFerramentas.Visibility = Visibility.Visible;
        Placeholder.Visibility = Visibility.Collapsed;
        Preview.Visibility = Visibility.Visible;
        BtnSalvar.IsEnabled = BtnCopiar.IsEnabled = true;
        StatusLbl.Text = "Capturado. Desenhe por cima e use Salvar PNG (ou Copiar). O envio continua sendo opcional e explícito.";
        AtualizarPreview();
    }

    void AtualizarPreview()
    {
        if (_base is null) return;
        var composto = EditorCaptura.Renderizar(_base, _pilha.Itens);
        _previewAtual = ParaImagem(composto);
        composto.Dispose();
        Preview.Source = _previewAtual;
    }

    static BitmapImage ParaImagem(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = new MemoryStream(ms.ToArray());
        img.EndInit();
        img.Freeze();
        return img;
    }

    void Ferramenta_Click(object sender, RoutedEventArgs e) =>
        _ferramenta = ((sender as Button)?.Tag as string) switch
        {
            "Retangulo" => TipoAnotacao.Retangulo,
            "Seta" => TipoAnotacao.Seta,
            "Texto" => TipoAnotacao.Texto,
            _ => TipoAnotacao.Lapis,
        };

    void Cor_Click(object sender, RoutedEventArgs e) =>
        _cor = (sender as Button)?.Tag as string == "Yellow"
            ? System.Drawing.Color.FromArgb(255, 255, 209, 102)  // amarelo do tema
            : System.Drawing.Color.FromArgb(255, 255, 77, 79);   // vermelho do tema

    void Desfazer_Click(object sender, RoutedEventArgs e)
    {
        if (_pilha.Desfazer()) AtualizarPreview();
        else StatusLbl.Text = "Nada para desfazer.";
    }

    void Limpar_Click(object sender, RoutedEventArgs e)
    {
        _pilha.Limpar();
        AtualizarPreview();
        StatusLbl.Text = "Anotações removidas. A imagem original permanece intacta.";
    }

    void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_base is null) return;
        var p = ParaImagem(e.GetPosition(Preview));
        if (_ferramenta == TipoAnotacao.Texto)
        {
            var texto = PromptTexto.Perguntar(Window.GetWindow(this), "Texto da anotação");
            if (!string.IsNullOrWhiteSpace(texto))
            {
                _pilha.Adicionar(new Anotacao(TipoAnotacao.Texto, new() { p }, _cor, 2.5f, texto));
                AtualizarPreview();
            }
            return;
        }
        _desenhando = true;
        _pontoInicial = p;
        _traco = new List<PointD> { p };
        if (_ferramenta == TipoAnotacao.Retangulo || _ferramenta == TipoAnotacao.Seta)
            Preview.CaptureMouse();
    }

    void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_desenhando || _base is null || _traco is null) return;
        if (_ferramenta == TipoAnotacao.Lapis)
        {
            _traco.Add(ParaImagem(e.GetPosition(Preview)));
            AtualizarPreview();
        }
    }

    void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_desenhando || _base is null || _traco is null) return;
        _desenhando = false;
        Preview.ReleaseMouseCapture();
        var fim = ParaImagem(e.GetPosition(Preview));
        var anotacao = _ferramenta switch
        {
            TipoAnotacao.Lapis when _traco.Count == 1 => new Anotacao(_ferramenta, _traco, _cor, 2.5f, null),
            TipoAnotacao.Lapis => new Anotacao(_ferramenta, _traco, _cor, 2.5f, null),
            _ => new Anotacao(_ferramenta, new() { _pontoInicial, fim }, _cor, 2.5f, null),
        };
        _pilha.Adicionar(anotacao);
        _traco = null;
        AtualizarPreview();
    }

    /// <summary>Converte a posição do mouse (no elemento) para coordenadas da imagem em resolução cheia.</summary>
    PointD ParaImagem(System.Windows.Point pos)
    {
        if (_base is null) return new PointD((int)Math.Round(pos.X), (int)Math.Round(pos.Y));
        double escala = Math.Min(Preview.ActualWidth / _base.Width, Preview.ActualHeight / _base.Height);
        double deslX = (Preview.ActualWidth - _base.Width * escala) / 2;
        double deslY = (Preview.ActualHeight - _base.Height * escala) / 2;
        return new PointD(
            (int)Math.Clamp((pos.X - deslX) / escala, 0, _base.Width - 1),
            (int)Math.Clamp((pos.Y - deslY) / escala, 0, _base.Height - 1));
    }

    // ── F2.3: salvar / copiar / enviar ────────────────────────────────────────────

    Bitmap ComporFinal() => _base is null ? throw new InvalidOperationException("Nenhuma captura.") : EditorCaptura.Renderizar(_base, _pilha.Itens);

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null) return;
        using var final = ComporFinal();
        _ultimoCaminho = CapturaTela.Salvar(final);
        StatusLbl.Text = $"Salvo em {_ultimoCaminho} · envio continua opcional.";
    }

    void Copiar_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null) return;
        using var final = ComporFinal();
        using var ms = new MemoryStream();
        final.Save(ms, ImageFormat.Png);
        Clipboard.SetImage(ParaImagem(final));
        StatusLbl.Text = "Imagem copiada para a área de transferência.";
    }

    async void Enviar_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null) return;
        var cfg = _cfg.Carregar();
        if (!cfg.EnvioConfigurado || cfg.WebhookUrl is null) return;
        if (MessageBox.Show($"Enviar a captura para o webhook configurado?\n\n{cfg.WebhookUrl}\n\nO envio é explícito: só acontece agora, com sua confirmação.",
                "Confirmar envio", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        BtnEnviar.IsEnabled = false;
        try
        {
            using var final = ComporFinal();
            using var ms = new MemoryStream();
            final.Save(ms, ImageFormat.Png);
            bool ok = await EnvioCaptura.EnviarAsync(ms.ToArray(), CapturaTela.NomeArquivo(DateTime.Now), cfg.WebhookUrl);
            StatusLbl.Text = ok ? "Enviado para o webhook (ação explícita)." : "Falha no envio — verifique a URL e sua conexão. Nada foi salvo a menos que você tenha usado Salvar PNG.";
        }
        catch (Exception ex)
        {
            StatusLbl.Text = "Erro ao enviar: " + ex.Message;
        }
        finally { BtnEnviar.IsEnabled = true; }
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
