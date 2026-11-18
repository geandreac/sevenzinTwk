using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Instaladores" (§21.5/21.7): catálogo completo de utilitários de fonte oficial com ícone à esquerda
/// e botão universal "Baixar e verificar" em todos os itens. O download é verificado (assinatura Authenticode + hash
/// quando fixado) antes de permitir executar, e nada roda sem confirmação.</summary>
public partial class InstaladoresView : UserControl
{
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "instaladores");

    readonly FilaInstaladores _fila = new();
    StackPanel? _filaLista;
    StackPanel _catalogoHost = null!;
    string? _filtro;
    readonly List<Button> _pills = new();

    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Brush Borda => (Brush)Application.Current.Resources["BorderDefaultBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];

    public InstaladoresView()
    {
        InitializeComponent();
        Lista.Children.Add(Pills());
        _catalogoHost = new StackPanel();
        Lista.Children.Add(_catalogoHost);
        RenderCatalogo();
        Lista.Children.Add(CartaoWinget());
        Lista.Children.Add(CartaoFila());
    }

    WrapPanel Pills()
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        void Add(string? cat, string texto)
        {
            var b = new Button
            {
                Content = texto,
                Tag = cat,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(13, 6, 13, 6),
                Style = cat == _filtro ? Primario : Secundario,
            };
            b.Click += (_, _) => SelecionarFiltro(cat);
            _pills.Add(b);
            wrap.Children.Add(b);
        }
        Add(null, $"Todos  {CatalogoUtilitarios.Todos.Count}");
        foreach (var (cat, qtd) in CatalogoUtilitarios.ContagemPorCategoria()) Add(cat, $"{cat}  {qtd}");
        return wrap;
    }

    void SelecionarFiltro(string? cat)
    {
        _filtro = cat;
        foreach (var p in _pills) p.Style = (string?)p.Tag == _filtro ? Primario : Secundario;
        RenderCatalogo();
    }

    void RenderCatalogo()
    {
        _catalogoHost.Children.Clear();
        var itens = _filtro is null ? CatalogoUtilitarios.Todos : CatalogoUtilitarios.Todos.Where(u => u.Categoria == _filtro);
        foreach (var u in itens) _catalogoHost.Children.Add(Cartao(u));
    }

    static UIElement CriarIconeAplicativo(Utilitario u)
    {
        var caixa = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(12),
            Background = Surface2,
            BorderBrush = Borda,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/Apps/{u.Icone}.png", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            var img = new Image
            {
                Source = bmp,
                Width = 36,
                Height = 36,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            caixa.Child = img;
        }
        catch
        {
            caixa.Child = new TextBlock
            {
                Text = u.Nome.Length > 0 ? u.Nome[..1].ToUpperInvariant() : "?",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        return caixa;
    }

    Border Cartao(Utilitario u)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icone = CriarIconeAplicativo(u);
        Grid.SetColumn(icone, 0);
        grid.Children.Add(icone);

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = u.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(new TextBlock { Text = u.Descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        var publisher = u.PublisherEsperado is { } p ? $"Assinatura esperada: {p}" : "Assinatura Authenticode exigida na verificação.";
        sp.Children.Add(new TextBlock { Text = publisher, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });

        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

        var acoes = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var baixar = new Button { Style = Primario, Content = "Baixar e verificar", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 7, 12, 7) };
        var abrir = new Button { Style = Secundario, Content = "Abrir página oficial", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 7, 12, 7) };
        var executar = new Button { Style = Primario, Content = "Executar instalador", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 7, 12, 7), Visibility = Visibility.Collapsed };

        baixar.Click += async (_, _) => await BaixarEVerificar(u, status, baixar, executar);
        abrir.Click += (_, _) => AbrirUrl(u.UrlOficial);
        executar.Click += (_, _) => Executar((string?)executar.Tag);

        acoes.Children.Add(baixar);
        acoes.Children.Add(abrir);
        acoes.Children.Add(executar);

        sp.Children.Add(acoes);
        sp.Children.Add(status);

        Grid.SetColumn(sp, 1);
        grid.Children.Add(sp);

        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = grid };
    }

    async Task BaixarEVerificar(Utilitario u, TextBlock status, Button baixar, Button executar)
    {
        baixar.IsEnabled = false;
        executar.Visibility = Visibility.Collapsed;
        status.Visibility = Visibility.Visible;
        status.Foreground = Muted;
        status.Text = "Resolvendo fonte oficial e iniciando download…";
        var urlRef = u.UrlDownload ?? u.UrlOficial;
        _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Baixando, null, null));
        try
        {
            Directory.CreateDirectory(Pasta);
            var prog = new Progress<ProgressoDownload>(p => status.Text =
                p.Percentual is { } pct
                    ? $"Baixando… {pct:P0} · {p.BytesPorSegundo / 1024:0} KB/s" + (p.TempoRestante is { } eta ? $" · falta ~{eta.TotalSeconds:0}s" : "")
                    : $"Baixando… {p.BytesRecebidos / 1024:N0} KB");

            var destino = await DownloaderInstalador.ObterInstaladorProntoAsync(u, Pasta, prog);

            _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Verificando, null, null));
            var r = VerificadorDownload.Verificar(destino, u);
            if (r == ResultadoVerificacao.Ok)
            {
                status.Text = $"✔ Verificado ({Path.GetFileName(destino)} — assinatura confere). Você pode executar.";
                status.Foreground = Success;
                executar.Tag = destino;
                executar.Visibility = Visibility.Visible;
                _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Concluido, "assinatura verificada", null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"'{u.Nome}' verificado", "Assinatura confere — pronto para executar.");
                Toasts.Mostrar(this, TipoToast.Sucesso, "Download verificado", $"{u.Nome} — assinatura confere.");
            }
            else
            {
                status.Text = $"✖ Verificação falhou: {Motivo(r)}. O arquivo NÃO será oferecido para execução.";
                status.Foreground = Danger;
                _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Falhou, Motivo(r), null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"'{u.Nome}' reprovado na verificação", Motivo(r));
                Toasts.Mostrar(this, TipoToast.Erro, "Verificação falhou", $"{u.Nome}: {Motivo(r)}. O arquivo foi descartado.");
                try { File.Delete(destino); } catch { }
            }
        }
        catch (Exception ex)
        {
            status.Text = "Falha no download direto: " + ex.Message + " (você pode usar 'Abrir página oficial').";
            status.Foreground = Danger;
            _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Falhou, ex.Message, null));
            NotificacoesStore.Padrao.Adicionar("Instaladores", $"Falha no download de '{u.Nome}'", ex.Message);
            Toasts.Mostrar(this, TipoToast.Erro, "Falha no download", ex.Message);
        }
        finally { baixar.IsEnabled = true; AtualizarFila(); }
    }

    static string Motivo(ResultadoVerificacao r) => r switch
    {
        ResultadoVerificacao.HashNaoConfere => "o hash não confere",
        ResultadoVerificacao.SemAssinatura => "o arquivo não possui assinatura digital Authenticode",
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

    static string NomeAmigavelWinget(string chave) => chave switch
    {
        "steam" => "Steam",
        "discord" => "Discord",
        "7zip" => "7-Zip",
        "chrome" => "Google Chrome",
        "vlc" => "VLC",
        _ => chave,
    };

    Border CartaoWinget()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "INSTALAÇÃO RÁPIDA (WINGET)", Style = SectionStyle });
        sp.Children.Add(new TextBlock
        {
            Text = "Instala pelos IDs oficiais do winget (lista fechada do original). Cada item pede confirmação; o andamento aparece na fila abaixo.",
            Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 8), TextWrapping = TextWrapping.Wrap,
        });

        var caixas = WingetAllowlist.Lista.Select(item =>
        {
            var cb = new CheckBox { Content = $"{NomeAmigavelWinget(item.Chave)}  —  {item.Id}", Tag = item.Id, FontSize = 13, Margin = new Thickness(0, 3, 0, 3) };
            return cb;
        }).ToList();
        foreach (var cb in caixas) sp.Children.Add(cb);

        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var botao = new Button { Style = Primario, Content = "Instalar selecionados", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
        botao.Click += async (_, _) =>
        {
            var escolhidos = caixas.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            if (escolhidos.Count == 0) { status.Text = "Selecione ao menos um item."; status.Foreground = Muted; status.Visibility = Visibility.Visible; return; }
            if (MessageBox.Show($"O winget vai instalar:\n\n• {string.Join("\n• ", escolhidos)}\n\nConfirmar?", "Instalação via winget",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

            botao.IsEnabled = false;
            status.Visibility = Visibility.Visible;
            foreach (var id in escolhidos)
            {
                status.Foreground = Muted;
                status.Text = $"Instalando {id}…";
                _fila.Marcar(new ItemFila(id, "winget:" + id, EstadoFila.Baixando, null, null));
                var r = await WingetRunner.InstalarAsync(id);
                status.Foreground = r.Ok ? Success : Danger;
                status.Text = $"{id}: {(r.JaInstalado ? "já estava instalado" : r.Detalhe)}";
                _fila.Marcar(new ItemFila(id, "winget:" + id, r.Ok ? EstadoFila.Concluido : EstadoFila.Falhou, r.Detalhe, null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"winget: {id}", r.JaInstalado ? "já estava instalado" : r.Detalhe);
            }
            botao.IsEnabled = true;
            AtualizarFila();
        };
        sp.Children.Add(botao);
        sp.Children.Add(status);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    static string NomeEstado(EstadoFila e) => e switch
    {
        EstadoFila.Pendente => "Pendente",
        EstadoFila.Baixando => "Baixando…",
        EstadoFila.Verificando => "Verificando…",
        EstadoFila.Concluido => "✔ Concluído",
        _ => "✖ Falhou",
    };

    Border CartaoFila()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "FILA DE DOWNLOADS (salva localmente)", Style = SectionStyle });
        _filaLista = new StackPanel();
        sp.Children.Add(_filaLista);

        var limpar = new Button { Style = Secundario, Content = "Limpar concluídos/falhos", Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7) };
        limpar.Click += (_, _) => { _fila.LimparConcluidos(); AtualizarFila(); };
        sp.Children.Add(limpar);

        var card = new Border { Style = CardStyle, Margin = new Thickness(0, 6, 0, 0), Child = sp };
        AtualizarFila();
        return card;
    }

    void AtualizarFila()
    {
        if (_filaLista is null) return;
        _filaLista.Children.Clear();
        var itens = _fila.Todos.OrderByDescending(i => i.AtualizadoUtc).ToList();
        if (itens.Count == 0)
        {
            _filaLista.Children.Add(new TextBlock { Text = "Nada ainda. Os downloads feitos nesta tela aparecem aqui e continuam registrados após fechar o app.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
            return;
        }
        foreach (var i in itens)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            linha.Children.Add(new TextBlock { Text = i.Nome, FontSize = 12, Width = 240, TextTrimming = TextTrimming.CharacterEllipsis });
            var estado = new TextBlock { Text = NomeEstado(i.Estado), FontSize = 12, Width = 110, Foreground = i.Estado == EstadoFila.Concluido ? Success : i.Estado == EstadoFila.Falhou ? Danger : Muted };
            linha.Children.Add(estado);
            var detalhe = (i.Detalhe is { } d ? $" — {d}" : "") + (i.AtualizadoUtc is { } t ? $" · {t.ToLocalTime():dd/MM HH:mm}" : "");
            linha.Children.Add(new TextBlock { Text = detalhe, FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = detalhe.TrimStart(' ', '—', '·') });
            _filaLista.Children.Add(linha);
        }
    }
}
