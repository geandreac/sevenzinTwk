using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Sevenzin.Diagnostics;
using Forma = System.Windows.Shapes.Path;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Instaladores" (§21.5/21.7): catálogo de utilitários de fonte oficial com busca, chips de categoria
/// e cards em colunas que se ajustam à largura, mais o módulo de instalação rápida pelo WinGet. O download é
/// verificado (assinatura Authenticode + hash quando fixado) antes de permitir executar, e nada roda sem confirmação.</summary>
public partial class InstaladoresView : UserControl
{
    static readonly string Pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "instaladores");

    readonly FilaInstaladores _fila = new();
    StackPanel? _filaLista;
    readonly StackPanel _catalogoHost = new();
    // Cards criados uma vez só: buscar ou filtrar só reposiciona (o estado do download de cada card continua).
    readonly Dictionary<Utilitario, FrameworkElement> _cards = new();
    string? _categoria;
    int _colunas;
    bool _pronto;

    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Style LinkStyle => (Style)Application.Current.Resources["LinkButton"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Brush Recurso(string k) => (Brush)Application.Current.Resources[k];
    static Brush Muted => Recurso("TextMutedBrush");
    static Brush Secondary => Recurso("TextSecondaryBrush");
    static Brush Primary => Recurso("TextPrimaryBrush");
    static Brush Surface2 => Recurso("Surface2Brush");
    static Brush Success => Recurso("SuccessBrush");
    static Brush Warning => Recurso("WarningBrush");
    static Brush Danger => Recurso("DangerBrush");

    public InstaladoresView()
    {
        InitializeComponent();
        var todos = CatalogoUtilitarios.Todos;
        SeloTxt.Text = $"{todos.Count} instaladores de fontes oficiais · assinatura digital conferida antes de executar · nada roda sem a sua confirmação";

        AdicionarChip(null, "Todos", todos.Count);
        foreach (var (cat, qtd) in CatalogoUtilitarios.ContagemPorCategoria()) AdicionarChip(cat, cat, qtd);

        foreach (var u in todos) _cards[u] = Cartao(u);
        Lista.Children.Add(_catalogoHost);
        Lista.Children.Add(ModuloWinget());
        Lista.Children.Add(new TextBlock { Text = "Fila de downloads", Style = SectionStyle, Margin = new Thickness(0, 20, 0, 10) });
        Lista.Children.Add(CartaoFila());

        _pronto = true;
        _colunas = ColunasPara(1100);
        RenderCatalogo();
        Lista.SizeChanged += (_, e) =>
        {
            int c = ColunasPara(e.NewSize.Width);
            if (c != _colunas) { _colunas = c; RenderCatalogo(); }
        };
    }

    static int ColunasPara(double largura) => largura >= 1500 ? 3 : largura >= 860 ? 2 : 1;

    // ── Busca e chips ──

    void AdicionarChip(string? categoria, string nome, int qtd)
    {
        var conteudo = new StackPanel { Orientation = Orientation.Horizontal };
        conteudo.Children.Add(new TextBlock { Text = nome });
        conteudo.Children.Add(new TextBlock { Text = qtd.ToString(), Foreground = Muted, Margin = new Thickness(7, 0, 0, 0) });
        var chip = new RadioButton
        {
            Style = (Style)Application.Current.Resources["Chip"], GroupName = "categoria-instaladores",
            Content = conteudo, IsChecked = categoria is null,
        };
        System.Windows.Automation.AutomationProperties.SetName(chip, $"{nome}, {qtd} instaladores");
        chip.Checked += (_, _) =>
        {
            if (!_pronto) return;
            _categoria = categoria;
            RenderCatalogo();
            Rolagem.ScrollToTop();
            Movimento.Entrar(_catalogoHost, 6, 200);
        };
        Chips.Children.Add(chip);
    }

    void Busca_TextChanged(object sender, TextChangedEventArgs e)
    {
        LimparBusca.Visibility = Busca.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_pronto) RenderCatalogo();
    }

    void Busca_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && Busca.Text.Length > 0) { Busca.Clear(); e.Handled = true; }
    }

    void LimparBusca_Click(object sender, RoutedEventArgs e) { Busca.Clear(); Busca.Focus(); }

    void RenderCatalogo()
    {
        // Solta os cards dos grids antigos antes de remontar.
        foreach (var g in _catalogoHost.Children.OfType<Grid>()) g.Children.Clear();
        _catalogoHost.Children.Clear();

        var achados = CatalogoUtilitarios.Filtrar(CatalogoUtilitarios.Todos, Busca.Text, _categoria);
        ContagemTxt.Text = achados.Count == CatalogoUtilitarios.Todos.Count ? "" : $"{achados.Count} de {CatalogoUtilitarios.Todos.Count}";

        if (achados.Count == 0)
        {
            var vazio = new StackPanel { Margin = new Thickness(0, 16, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            vazio.Children.Add(new TextBlock { Text = "Nenhum instalador encontrado", FontSize = 15, FontWeight = FontWeights.SemiBold });
            vazio.Children.Add(new TextBlock
            {
                Text = Busca.Text.Trim().Length > 0 ? $"Nada corresponde a \"{Busca.Text.Trim()}\"" + (_categoria is { } c ? $" em {c}." : ".") : "Esta categoria está vazia.",
                Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 12),
            });
            var limpar = new Button { Style = Secundario, Content = "Limpar busca e filtro", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7) };
            limpar.Click += (_, _) =>
            {
                _pronto = false;
                Busca.Clear();
                if (Chips.Children[0] is RadioButton todos) todos.IsChecked = true;
                _categoria = null;
                _pronto = true;
                RenderCatalogo();
                Busca.Focus();
            };
            vazio.Children.Add(limpar);
            _catalogoHost.Children.Add(vazio);
            return;
        }

        // Um bloco por tema, na ordem definida dos grupos.
        bool primeiro = true;
        foreach (var grupo in achados.GroupBy(u => u.Categoria).OrderBy(g => OrdemDe(g.Key)))
        {
            var itens = grupo.ToList();
            _catalogoHost.Children.Add(new TextBlock { Text = $"{grupo.Key} · {itens.Count}", Style = SectionStyle, Margin = new Thickness(0, primeiro ? 8 : 16, 0, 10) });
            primeiro = false;
            var grade = new Grid();
            for (int c = 0; c < _colunas; c++)
                grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < itens.Count; i++)
            {
                if (i % _colunas == 0) grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var card = _cards[itens[i]];
                int col = i % _colunas;
                card.Margin = new Thickness(col == 0 ? 0 : 6, 0, col == _colunas - 1 ? 0 : 6, 12);
                Grid.SetRow(card, i / _colunas);
                Grid.SetColumn(card, col);
                grade.Children.Add(card);
            }
            _catalogoHost.Children.Add(grade);
        }
    }

    static int OrdemDe(string categoria)
    {
        int i = CatalogoUtilitarios.OrdemCategorias.ToList().IndexOf(categoria);
        return i < 0 ? int.MaxValue : i;
    }

    // ── Peças visuais ──

    static readonly Dictionary<string, BitmapImage?> IconesCache = new();

    static Border CaixaIcone(string icone, string nome, double caixa, double imagem)
    {
        var b = new Border { Width = caixa, Height = caixa, CornerRadius = new CornerRadius(caixa / 4), Background = Surface2 };
        if (!IconesCache.TryGetValue(icone, out var bmp))
        {
            try
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri($"pack://application:,,,/Assets/Apps/{icone}.png", UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
            }
            catch { bmp = null; }
            IconesCache[icone] = bmp;
        }
        if (bmp is not null)
        {
            var img = new Image { Source = bmp, Width = imagem, Height = imagem, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            b.Child = img;
        }
        else
            b.Child = new TextBlock
            {
                Text = nome.Length > 0 ? nome[..1].ToUpperInvariant() : "?", FontSize = imagem * 0.6, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
        return b;
    }

    static Forma Icone(string dados, Brush cor, double tamanho) => new()
    {
        Data = Geometry.Parse(dados), Stroke = cor, StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        Width = tamanho, Height = tamanho, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
    };

    const string IconeEscudo = "M7,0.5 L13,2.8 V7 C13,10.3 10.6,12.6 7,13.5 C3.4,12.6 1,10.3 1,7 V2.8 Z M4.4,7 L6.2,8.8 L9.8,5.2";
    const string IconeAlerta = "M7,1 L13.5,12.5 H0.5 Z M7,5.2 V8.4 M7,10.4 V10.5";
    const string IconePacote = "M12,2 L21,6.5 V17.5 L12,22 L3,17.5 V6.5 Z M3,6.5 L12,11 L21,6.5 M12,11 V22 M7.5,4.3 L16.5,8.8";

    static string Dominio(string url) => Uri.TryCreate(url, UriKind.Absolute, out var x) ? x.Host.Replace("www.", "") : url;

    /// <summary>Faixa de espera sem porcentagem: um trecho curto corre pela trilha enquanto a tarefa dura.
    /// Sem animações do sistema, fica a trilha cheia parada (o texto ao lado diz o estado).</summary>
    static Grid BarraEspera()
    {
        var trilha = new Grid { Height = 3, ClipToBounds = true, Background = Recurso("Surface3Brush"), Visibility = Visibility.Collapsed };
        var trecho = new Border { Background = Recurso("Brand500Brush"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        var tt = new TranslateTransform();
        trecho.RenderTransform = tt;
        trilha.Children.Add(trecho);
        void Rodar()
        {
            double w = trilha.ActualWidth;
            if (w <= 0 || trilha.Visibility != Visibility.Visible) { tt.BeginAnimation(TranslateTransform.XProperty, null); return; }
            if (!Movimento.Ativo) { trecho.Width = w; tt.BeginAnimation(TranslateTransform.XProperty, null); tt.X = 0; return; }
            trecho.Width = w * 0.3;
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-w * 0.3, w, TimeSpan.FromMilliseconds(1100))
            { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }
        trilha.SizeChanged += (_, _) => Rodar();
        trilha.IsVisibleChanged += (_, _) => Rodar();
        return trilha;
    }

    // ── Card do catálogo ──

    FrameworkElement Cartao(Utilitario u)
    {
        var g = new Grid();
        for (int r = 0; r < 4; r++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Cabeçalho: ícone + nome (protagonista do card) + domínio oficial em segundo plano.
        var cab = new DockPanel();
        var icone = CaixaIcone(u.Icone, u.Nome, 48, 30);
        icone.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(icone, Dock.Left);
        cab.Children.Add(icone);
        var nomes = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nomes.Children.Add(new TextBlock { Text = u.Nome, FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = u.Nome });
        nomes.Children.Add(new TextBlock { Text = Dominio(u.UrlOficial), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        cab.Children.Add(nomes);
        g.Children.Add(cab);

        // Descrição: no máximo 2 linhas e altura sempre reservada, para os botões ficarem na mesma posição em todo card.
        var desc = new TextBlock
        {
            Text = u.Descricao, Foreground = Secondary, FontSize = 13, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
            LineHeight = 19, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Height = 38, Margin = new Thickness(0, 14, 0, 14),
            ToolTip = u.SoPagina is null ? u.Descricao : $"{u.Descricao}\n\n{u.SoPagina}",
        };
        Grid.SetRow(desc, 1);
        g.Children.Add(desc);

        // Andamento do download (só aparece durante e depois de usar o card).
        var andamento = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
        var barra = new ProgressBar { Height = 3, Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed };
        var espera = BarraEspera();
        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        andamento.Children.Add(barra);
        andamento.Children.Add(espera);
        andamento.Children.Add(status);
        Grid.SetRow(andamento, 2);
        g.Children.Add(andamento);

        // Rodapé: ações à esquerda (sempre na mesma posição), segurança discreta à direita.
        var rodape = new Grid();
        rodape.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rodape.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var acoes = new StackPanel { Orientation = Orientation.Horizontal };
        var baixar = new Button { Style = Primario, Content = "Baixar e verificar", Margin = new Thickness(0, 0, 12, 0), Padding = new Thickness(14, 7, 14, 7) };
        var executar = new Button { Style = Primario, Content = "Executar instalador", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(14, 7, 14, 7), Visibility = Visibility.Collapsed };
        var abrir = u.SoPagina is null
            ? new Button { Style = LinkStyle, Content = "Página oficial ↗", VerticalAlignment = VerticalAlignment.Center }
            : new Button { Style = Secundario, Content = "Abrir página oficial ↗", Padding = new Thickness(14, 7, 14, 7) };
        System.Windows.Automation.AutomationProperties.SetName(baixar, $"Baixar e verificar {u.Nome}");
        System.Windows.Automation.AutomationProperties.SetName(executar, $"Executar instalador de {u.Nome}");
        System.Windows.Automation.AutomationProperties.SetName(abrir, $"Abrir página oficial de {u.Nome}");
        baixar.Click += async (_, _) => await BaixarEVerificar(u, new Andamento(andamento, barra, espera, status), baixar, executar);
        abrir.Click += (_, _) => AbrirUrl(u.UrlOficial);
        executar.Click += (_, _) => Executar((string?)executar.Tag);
        if (u.SoPagina is null) { acoes.Children.Add(executar); acoes.Children.Add(baixar); }
        acoes.Children.Add(abrir);
        rodape.Children.Add(acoes);

        var seguranca = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        if (u.SoPagina is null)
        {
            seguranca.Children.Add(Icone(IconeEscudo, Success, 13));
            seguranca.Children.Add(new TextBlock { Text = u.PublisherEsperado ?? "Assinatura exigida", Foreground = Muted, FontSize = 12, Margin = new Thickness(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            seguranca.ToolTip = (u.PublisherEsperado is { } p ? $"Assinatura digital de {p} conferida antes de executar." : "Assinatura digital Authenticode conferida antes de executar.")
                + $"\nFonte oficial: {Dominio(u.UrlDownload ?? u.UrlOficial)}"
                + (u.Sha256Esperado is not null ? "\nHash SHA-256 fixado: o arquivo precisa ser idêntico ao registrado." : "");
        }
        else
        {
            seguranca.Children.Add(Icone(IconeAlerta, Warning, 13));
            seguranca.Children.Add(new TextBlock { Text = "Só pela página oficial", Foreground = Muted, FontSize = 12, Margin = new Thickness(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            seguranca.ToolTip = u.SoPagina;
        }
        System.Windows.Automation.AutomationProperties.SetName(seguranca, seguranca.ToolTip?.ToString() ?? "");
        Grid.SetColumn(seguranca, 1);
        rodape.Children.Add(seguranca);
        Grid.SetRow(rodape, 3);
        g.Children.Add(rodape);

        var card = new Border { Style = CardStyle, Padding = new Thickness(18), Child = g };
        // Hover: só a borda acende; o card não pula.
        card.MouseEnter += (_, _) => card.BorderBrush = Recurso("BorderStrongBrush");
        card.MouseLeave += (_, _) => card.BorderBrush = Recurso("BorderDefaultBrush");
        return card;
    }

    /// <summary>As peças de andamento de um card (barra com %, faixa de espera e texto).</summary>
    sealed record Andamento(StackPanel Painel, ProgressBar Barra, Grid Espera, TextBlock Status)
    {
        public void Mostrar(string texto, Brush cor, double? fracao = null, bool esperando = false)
        {
            Painel.Visibility = Visibility.Visible;
            Status.Text = texto;
            Status.Foreground = cor;
            Barra.Visibility = fracao is null ? Visibility.Collapsed : Visibility.Visible;
            if (fracao is { } f) Barra.Value = f;
            Espera.Visibility = esperando && fracao is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    async Task BaixarEVerificar(Utilitario u, Andamento a, Button baixar, Button executar)
    {
        baixar.IsEnabled = false;
        executar.Visibility = Visibility.Collapsed;
        baixar.Style = Primario;
        baixar.Content = "Baixar e verificar";
        a.Mostrar("Resolvendo a fonte oficial e iniciando o download…", Muted, esperando: true);
        var urlRef = u.UrlDownload ?? u.UrlOficial;
        _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Baixando, null, null));
        try
        {
            Directory.CreateDirectory(Pasta);
            var prog = new Progress<ProgressoDownload>(p =>
            {
                if (p.Percentual is { } pct)
                    a.Mostrar($"Baixando… {pct:P0} · {p.BytesPorSegundo / 1024:0} KB/s" + (p.TempoRestante is { } eta ? $" · falta ~{eta.TotalSeconds:0} s" : ""), Muted, pct);
                else
                    a.Mostrar($"Baixando… {p.BytesRecebidos / 1024:N0} KB", Muted, esperando: true);
            });

            var destino = await DownloaderInstalador.ObterInstaladorProntoAsync(u, Pasta, prog);

            a.Mostrar("Conferindo a assinatura digital…", Muted, esperando: true);
            _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Verificando, null, null));
            var r = VerificadorDownload.Verificar(destino, u);
            if (r == ResultadoVerificacao.Ok)
            {
                a.Mostrar($"Verificado: {System.IO.Path.GetFileName(destino)} tem a assinatura esperada. Você pode executar.", Success);
                executar.Tag = destino;
                executar.Visibility = Visibility.Visible;
                baixar.Style = Secundario; // agora a ação principal é executar
                baixar.Content = "Baixar de novo";
                _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Concluido, "assinatura verificada", null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"'{u.Nome}' verificado", "Assinatura confere — pronto para executar.");
                Toasts.Mostrar(this, TipoToast.Sucesso, "Download verificado", $"{u.Nome} — assinatura confere.");
            }
            else
            {
                a.Mostrar($"Verificação falhou: {Motivo(r)}. O arquivo foi descartado e não será oferecido para execução.", Danger);
                _fila.Marcar(new ItemFila(u.Nome, urlRef, EstadoFila.Falhou, Motivo(r), null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"'{u.Nome}' reprovado na verificação", Motivo(r));
                Toasts.Mostrar(this, TipoToast.Erro, "Verificação falhou", $"{u.Nome}: {Motivo(r)}. O arquivo foi descartado.");
                try { File.Delete(destino); } catch { }
            }
        }
        catch (Exception ex)
        {
            a.Mostrar("Falha no download direto: " + ex.Message + " Você pode usar a página oficial.", Danger);
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

    // ── Instalação rápida (WinGet) ──

    static (string Nome, string Icone) InfoWinget(string chave) => chave switch
    {
        "steam" => ("Steam", "logo_steam"),
        "discord" => ("Discord", "logo_discord"),
        "7zip" => ("7-Zip", "logo_7zip"),
        "chrome" => ("Google Chrome", "logo_googlechrome"),
        "vlc" => ("VLC", "logo_vlcplayer"),
        _ => (chave, chave),
    };

    sealed record LinhaWinget(string Id, string Nome, CheckBox Caixa, TextBlock Status, Grid Espera);

    FrameworkElement ModuloWinget()
    {
        var sp = new StackPanel();

        // Cabeçalho do módulo: ícone + título + descrição curta; a ação fica à direita.
        var cab = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icone = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(12), Background = Recurso("BrandTintBrush"), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        icone.Child = Icone(IconePacote, Recurso("Brand400Brush"), 24);
        cab.Children.Add(icone);
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock { Text = "Instalação rápida com WinGet", FontSize = 16, FontWeight = FontWeights.SemiBold });
        textos.Children.Add(new TextBlock
        {
            Text = "Instala pelo gerenciador de pacotes oficial do Windows, sem abrir sites. Você confirma a lista antes de começar.",
            Foreground = Secondary, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 16, 0),
        });
        Grid.SetColumn(textos, 1);
        cab.Children.Add(textos);
        var instalar = new Button { Style = Primario, Content = "Instalar selecionados", Padding = new Thickness(14, 7, 14, 7), VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
        Grid.SetColumn(instalar, 2);
        cab.Children.Add(instalar);
        sp.Children.Add(cab);

        // Uma linha por pacote: seleção, ícone, nome, ID do pacote e o estado dele.
        var linhas = new List<LinhaWinget>();
        var lista = new StackPanel();
        foreach (var (chave, id) in WingetAllowlist.Lista)
        {
            var (nome, ic) = InfoWinget(chave);
            var conteudo = new StackPanel { Orientation = Orientation.Horizontal };
            var caixaIcone = CaixaIcone(ic, nome, 32, 20);
            caixaIcone.Margin = new Thickness(4, 0, 12, 0);
            conteudo.Children.Add(caixaIcone);
            var nomes = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nomes.Children.Add(new TextBlock { Text = nome, FontSize = 14, FontWeight = FontWeights.SemiBold });
            nomes.Children.Add(new TextBlock { Text = id, FontSize = 12, Foreground = Muted, FontFamily = new FontFamily("Cascadia Mono, Consolas"), Margin = new Thickness(0, 1, 0, 0) });
            conteudo.Children.Add(nomes);
            var caixa = new CheckBox { Content = conteudo, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(caixa, $"{nome} ({id})");

            var status = new TextBlock { FontSize = 12, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 320 };
            var espera = BarraEspera();
            espera.Margin = new Thickness(0, 10, 0, 0);

            var linha = new Grid { Margin = new Thickness(0, 10, 0, 10) };
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            linha.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            linha.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            linha.Children.Add(caixa);
            Grid.SetColumn(status, 1);
            linha.Children.Add(status);
            Grid.SetRow(espera, 1);
            Grid.SetColumnSpan(espera, 2);
            linha.Children.Add(espera);

            if (lista.Children.Count > 0) lista.Children.Add(new Border { Height = 1, Background = Recurso("BorderDefaultBrush") });
            lista.Children.Add(linha);
            linhas.Add(new LinhaWinget(id, nome, caixa, status, espera));
        }
        sp.Children.Add(lista);

        void AtualizarBotao()
        {
            int n = linhas.Count(l => l.Caixa.IsChecked == true);
            instalar.IsEnabled = n > 0 && linhas.All(l => l.Caixa.IsEnabled);
            instalar.Content = n switch { 0 => "Instalar selecionados", 1 => "Instalar 1 selecionado", _ => $"Instalar {n} selecionados" };
        }
        foreach (var l in linhas) { l.Caixa.Checked += (_, _) => AtualizarBotao(); l.Caixa.Unchecked += (_, _) => AtualizarBotao(); }

        void Estado(LinhaWinget l, string texto, Brush cor, bool esperando)
        {
            l.Status.Text = texto;
            l.Status.Foreground = cor;
            l.Espera.Visibility = esperando ? Visibility.Visible : Visibility.Collapsed;
            Movimento.Entrar(l.Status, 4, 160);
        }

        instalar.Click += async (_, _) =>
        {
            var escolhidos = linhas.Where(l => l.Caixa.IsChecked == true).ToList();
            if (escolhidos.Count == 0) return;
            if (MessageBox.Show($"O WinGet vai instalar, um por vez:\n\n• {string.Join("\n• ", escolhidos.Select(l => $"{l.Nome} ({l.Id})"))}\n\nConfirmar?", "Instalação via WinGet",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

            foreach (var l in linhas) l.Caixa.IsEnabled = false;
            AtualizarBotao();
            foreach (var l in escolhidos) Estado(l, "Na fila", Muted, false);
            foreach (var l in escolhidos)
            {
                Estado(l, "Instalando…", Primary, true);
                _fila.Marcar(new ItemFila(l.Id, "winget:" + l.Id, EstadoFila.Baixando, null, null));
                var r = await WingetRunner.InstalarAsync(l.Id);
                if (r.Ok) Estado(l, r.JaInstalado ? "Já estava instalado" : "Instalado", r.JaInstalado ? Secondary : Success, false);
                else { Estado(l, "Falhou: " + r.Detalhe, Danger, false); l.Status.ToolTip = r.Detalhe; }
                _fila.Marcar(new ItemFila(l.Id, "winget:" + l.Id, r.Ok ? EstadoFila.Concluido : EstadoFila.Falhou, r.Detalhe, null));
                NotificacoesStore.Padrao.Adicionar("Instaladores", $"WinGet: {l.Nome}", r.JaInstalado ? "já estava instalado" : r.Detalhe);
                AtualizarFila();
            }
            foreach (var l in linhas) { l.Caixa.IsEnabled = true; l.Caixa.IsChecked = false; }
            AtualizarBotao();
        };

        return new Border { Style = CardStyle, Padding = new Thickness(20, 20, 20, 10), Margin = new Thickness(0, 8, 0, 0), Child = sp };
    }

    static string NomeEstado(EstadoFila e) => e switch
    {
        EstadoFila.Pendente => "Pendente",
        EstadoFila.Baixando => "Baixando…",
        EstadoFila.Verificando => "Verificando…",
        EstadoFila.Concluido => "Concluído",
        _ => "Falhou",
    };

    Border CartaoFila()
    {
        var sp = new StackPanel();
        _filaLista = new StackPanel();
        sp.Children.Add(_filaLista);

        var limpar = new Button { Style = Secundario, Content = "Limpar concluídos/falhos", Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7) };
        limpar.Click += (_, _) => { _fila.LimparConcluidos(); AtualizarFila(); };
        sp.Children.Add(limpar);

        var card = new Border { Style = CardStyle, Child = sp };
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
