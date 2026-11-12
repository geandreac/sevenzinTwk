using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Mídia" (paridade com a tela 15 do original — Pendrive/Windows Media; fluxos F6/F7):
/// construir ISO personalizada (só a edição escolhida, boot UEFI) e gravar pendrive (FAT32 + split de WIM,
/// SafeUsb + confirmação explícita). Tudo exige administrador; gravar é destrutivo para o pendrive.</summary>
public partial class MediaView : UserControl
{
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Style DarkCombo => (Style)Application.Current.Resources["DarkCombo"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];

    string? _origemIso;
    List<EdicaoImagem> _edicoes = new();
    List<UsbRecord> _usbs = new();
    string? _destino;
    string? _pastaDrivers;
    TextBlock _origemLbl = null!, _destinoLbl = null!, _isoStatus = null!, _usbStatus = null!, _driversLbl = null!;
    ComboBox _edicoesBox = null!, _usbBox = null!;
    Button _construirBtn = null!, _gravarBtn = null!;
    StackPanel _appsPainel = null!;

    public MediaView()
    {
        InitializeComponent();
        Montar();
    }

    void Montar()
    {
        Lista.Children.Clear();

        // ── Construir ISO personalizada ──
        Lista.Children.Add(new TextBlock { Text = "CONSTRUIR ISO PERSONALIZADA", Style = SectionStyle });
        var iso = new StackPanel();
        iso.Children.Add(new TextBlock
        {
            Text = "Exporta só a edição que você escolher e gera uma ISO bootável por UEFI. A ISO de origem não é alterada.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });

        var origemBtn = Botao(Secundario, _origemIso is null ? "Escolher ISO de origem" : "Trocar ISO de origem", EscolherOrigem);
        iso.Children.Add(origemBtn);
        _origemLbl = new TextBlock { Text = _origemIso ?? "Nenhuma ISO de origem.", Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        iso.Children.Add(_origemLbl);

        _edicoesBox = new ComboBox { Style = DarkCombo, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
        foreach (var e in _edicoes) _edicoesBox.Items.Add(new ComboBoxItem { Content = $"{e.Indice} — {e.Nome} ({e.Arquitetura ?? "n/d"})", Tag = e.Indice });
        if (_edicoesBox.Items.Count > 0) { _edicoesBox.SelectedIndex = 0; _edicoesBox.Visibility = Visibility.Visible; }
        iso.Children.Add(_edicoesBox);

        _destinoLbl = new TextBlock { Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = _destino is null ? Visibility.Collapsed : Visibility.Visible, Text = _destino is null ? "" : $"Destino: {_destino}", TextWrapping = TextWrapping.Wrap };
        iso.Children.Add(_destinoLbl);
        _isoStatus = new TextBlock { Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        iso.Children.Add(_isoStatus);

        // Serviço opcional (F12): apps provisionados para remover + pasta de drivers INF.
        var appsBtn = Botao(Secundario, "Apps provisionados (opcional): carregar lista da edição", CarregarApps);
        iso.Children.Add(appsBtn);
        _appsPainel = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        iso.Children.Add(_appsPainel);
        var driversBtn = Botao(Secundario, "Pasta de drivers INF (opcional)", EscolherDrivers);
        iso.Children.Add(driversBtn);
        _driversLbl = new TextBlock { Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        iso.Children.Add(_driversLbl);

        _construirBtn = Botao(Primario, "Construir ISO", () => { });
        _construirBtn.Click += async (_, _) => await ConstruirAsync();
        iso.Children.Add(_construirBtn);
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 16), Child = iso });

        // ── Gravar pendrive ──
        Lista.Children.Add(new TextBlock { Text = "GRAVAR PENDRIVE (FAT32 · UEFI)", Style = SectionStyle });
        var usb = new StackPanel();
        usb.Children.Add(new TextBlock
        {
            Text = "Grava a ISO escolhida acima num pendrive seguro (SafeUsb: USB, ≥ 8 GB, não é disco de sistema). O install.wim acima de 4 GB é dividido em partes de 3.800 MB. GRAVAR APAGA TODO O CONTEÚDO DO PENDRIVE.",
            Foreground = Danger, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });

        var atualizarBtn = Botao(Secundario, "Atualizar pendrives", AtualizarUsbs);
        usb.Children.Add(atualizarBtn);
        _usbBox = new ComboBox { Style = DarkCombo, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 10) };
        usb.Children.Add(_usbBox);
        _gravarBtn = Botao(Primario, "Gravar no pendrive selecionado", () => { });
        _gravarBtn.Visibility = Visibility.Collapsed;
        _gravarBtn.Click += async (_, _) => await GravarAsync();
        usb.Children.Add(_gravarBtn);
        _usbStatus = new TextBlock { Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        usb.Children.Add(_usbStatus);
        Lista.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 16), Child = usb });

        // ── Salvaguardas ──
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Salvaguardas", FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock
        {
            Text = "Nada roda sem sua confirmação. A ISO de origem é montada só para leitura. O pendrive só é tocado depois de passar no filtro SafeUsb e da sua confirmação explícita com o modelo e o tamanho. Boot por UEFI — sem suporte a BIOS/Legacy (limitação registrada, como no original).",
            Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
        });
        Lista.Children.Add(new Border { Style = CardStyle, Child = sp });
    }

    async void EscolherOrigem()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Escolha a ISO do Windows", Filter = "Imagem ISO (*.iso)|*.iso" };
        if (dlg.ShowDialog() != true) return;
        _origemIso = dlg.FileName;
        _origemLbl.Text = _origemIso;
        _destino = null;
        _destinoLbl.Visibility = Visibility.Collapsed;
        _destinoLbl.Text = "";
        _isoStatus.Text = "";
        await AnalisarAsync();
    }

    async System.Threading.Tasks.Task AnalisarAsync()
    {
        if (_origemIso is null) return;
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Esta operação monta a ISO e exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        _isoStatus.Foreground = Muted;
        _isoStatus.Text = "Lendo as edições da ISO…";
        var resultado = await System.Threading.Tasks.Task.Run(() => ExecutorDism.AnalisarIsoAsync(_origemIso!));
        if (!resultado.Ok)
        {
            _isoStatus.Foreground = Danger;
            _isoStatus.Text = "Falha ao ler a ISO: " + resultado.Erro;
            return;
        }
        _edicoes = resultado.Edicoes.ToList();
        _edicoesBox.Items.Clear();
        foreach (var e in _edicoes) _edicoesBox.Items.Add(new ComboBoxItem { Content = $"{e.Indice} — {e.Nome} ({e.Arquitetura ?? "n/d"})", Tag = e.Indice });
        if (_edicoesBox.Items.Count > 0) _edicoesBox.SelectedIndex = 0;
        _edicoesBox.Visibility = _edicoesBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _isoStatus.Foreground = Secondary;
        _isoStatus.Text = _edicoesBox.Items.Count > 0
            ? $"{_edicoesBox.Items.Count} edição(ões) encontrada(s). Escolha a edição e o destino, e clique em Construir ISO."
            : "Nenhuma edição encontrada na imagem.";
    }

    async void CarregarApps()
    {
        if (_origemIso is null || _edicoesBox.SelectedItem is not ComboBoxItem item || item.Tag is not int indice) { _isoStatus.Text = "Escolha a ISO de origem e a edição antes de carregar os apps."; return; }
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Ler os apps da imagem exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        _isoStatus.Foreground = Muted;
        _isoStatus.Text = $"Lendo os apps provisionados da edição {indice}…";
        var resultado = await System.Threading.Tasks.Task.Run(() => ExecutorDism.AnalisarIsoAsync(_origemIso!, indice));
        if (!resultado.Ok) { _isoStatus.Foreground = Danger; _isoStatus.Text = "Falha: " + resultado.Erro; return; }

        _appsPainel.Children.Clear();
        foreach (var app in resultado.Apps)
        {
            var cb = new CheckBox { Content = app.Pacote, FontSize = 11, Margin = new Thickness(0, 2, 0, 2), Tag = app.Pacote };
            _appsPainel.Children.Add(cb);
        }
        _appsPainel.Visibility = resultado.Apps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _isoStatus.Foreground = Secondary;
        _isoStatus.Text = resultado.Apps.Count > 0
            ? $"{resultado.Apps.Count} app(s) listado(s) — marque os que devem ser removidos da imagem."
            : "Nenhum app provisionado nesta edição.";
    }

    void EscolherDrivers()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Pasta com os drivers INF assinados" };
        if (dlg.ShowDialog() != true) return;
        _pastaDrivers = dlg.FolderName;
        _driversLbl.Text = $"Drivers: {_pastaDrivers}";
        _driversLbl.Visibility = Visibility.Visible;
    }

    async System.Threading.Tasks.Task ConstruirAsync()
    {
        if (_origemIso is null || _edicoesBox.SelectedItem is not ComboBoxItem item || item.Tag is not int indice) { _isoStatus.Text = "Escolha a ISO de origem e uma edição."; return; }
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Construir a ISO exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (_destino is null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Onde salvar a nova ISO", Filter = "Imagem ISO (*.iso)|*.iso", FileName = "sevenzin-custom.iso" };
            if (dlg.ShowDialog() != true) return;
            _destino = dlg.FileName;
            _destinoLbl.Text = $"Destino: {_destino}";
            _destinoLbl.Visibility = Visibility.Visible;
        }

        var appsRemover = _appsPainel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
        _construirBtn.IsEnabled = false;
        _isoStatus.Foreground = Muted;
        var resultado = await System.Threading.Tasks.Task.Run(() =>
            ConstrutorIso.BuildAsync(_origemIso!, _destino!, indice,
                t => Dispatcher.Invoke(() => _isoStatus.Text = t),
                appsRemover: appsRemover.Count > 0 ? appsRemover : null,
                pastaDrivers: _pastaDrivers));
        _construirBtn.IsEnabled = true;
        _isoStatus.Foreground = resultado.Ok ? Secondary : Danger;
        _isoStatus.Text = resultado.Ok
            ? $"✔ ISO criada em {resultado.Destino} (boot UEFI). Você pode gravá-la no pendrive abaixo."
            : "Falha: " + resultado.Erro;
    }

    void AtualizarUsbs()
    {
        _usbStatus.Foreground = Muted;
        _usbStatus.Text = "Procurando pendrives seguros…";
        _usbs = UsbMedia.Listar();
        _usbBox.Items.Clear();
        foreach (var d in _usbs)
            _usbBox.Items.Add(new ComboBoxItem { Content = $"{d.Nome} — {d.Tamanho / 1024.0 / 1024 / 1024:0} GB (disco {d.Number})", Tag = d });
        if (_usbBox.Items.Count > 0) _usbBox.SelectedIndex = 0;
        _usbStatus.Text = _usbs.Count == 0
            ? "Nenhum pendrive seguro encontrado (USB, ≥ 8 GB, sem papel de sistema)."
            : $"{_usbs.Count} pendrive(s) seguro(s) encontrado(s).";
        _gravarBtn.Visibility = _usbBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    async System.Threading.Tasks.Task GravarAsync()
    {
        if (_origemIso is null) { _usbStatus.Foreground = Danger; _usbStatus.Text = "Escolha a ISO de origem primeiro (card acima)."; return; }
        if (_usbBox.SelectedItem is not ComboBoxItem item || item.Tag is not UsbRecord usb) { _usbStatus.Foreground = Danger; _usbStatus.Text = "Selecione um pendrive."; return; }
        if (!Elevacao.EhAdmin())
        {
            if (MessageBox.Show("Gravar o pendrive exige administrador.\n\nReabrir o Sevenzin como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (MessageBox.Show(
                $"GRAVAR no pendrive:\n\n{usb.Nome} — {usb.Tamanho / 1024.0 / 1024 / 1024:0} GB (disco {usb.Number})\n\n" +
                "⚠ TODO o conteúdo do pendrive será APAGADO.\n\nContinuar?",
                "Confirmação — gravar pendrive", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _gravarBtn.IsEnabled = false;
        _usbStatus.Foreground = Muted;
        var resultado = await System.Threading.Tasks.Task.Run(() =>
            GravadorUsb.GravarAsync(usb, _origemIso!, t => Dispatcher.Invoke(() => _usbStatus.Text = t)));
        _gravarBtn.IsEnabled = true;
        _usbStatus.Foreground = resultado.Ok ? Secondary : Danger;
        _usbStatus.Text = resultado.Ok ? "✔ " + resultado.Detalhe : "Falha: " + resultado.Detalhe;
    }

    static Button Botao(Style estilo, string texto, Action acao)
    {
        // Esquerda + margem inferior: no stack vertical os botões não esticam na largura toda nem ficam colados.
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => acao();
        return b;
    }
}
