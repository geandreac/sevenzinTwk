using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Instaladores" (§21.5/21.7): catálogo de utilitários de fonte oficial. O download é
/// verificado (assinatura + hash quando fixado) antes de permitir executar, e nada roda sem confirmação.
/// O andamento de cada item fica registrado numa fila persistida em JSON local (sobrevive a reinícios).</summary>
public partial class InstaladoresView : UserControl
{
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "instaladores");

    readonly FilaInstaladores _fila = new();
    StackPanel? _filaLista;

    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];

    public InstaladoresView()
    {
        InitializeComponent();
        foreach (var u in CatalogoUtilitarios.Todos) Lista.Children.Add(Cartao(u));
        Lista.Children.Add(CartaoWinget());
        Lista.Children.Add(CartaoFila());
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
        _fila.Marcar(new ItemFila(u.Nome, u.UrlDownload!, EstadoFila.Baixando, null, null));
        try
        {
            Directory.CreateDirectory(Pasta);
            var destino = Path.Combine(Pasta, Path.GetFileName(new Uri(u.UrlDownload!).LocalPath));
            var prog = new Progress<double>(p => status.Text = $"Baixando… {p:P0}");
            await DownloaderInstalador.BaixarAsync(u.UrlDownload!, destino, prog);

            _fila.Marcar(new ItemFila(u.Nome, u.UrlDownload!, EstadoFila.Verificando, null, null));
            var r = VerificadorDownload.Verificar(destino, u);
            if (r == ResultadoVerificacao.Ok)
            {
                status.Text = "✔ Verificado (assinatura confere). Você pode executar.";
                status.Foreground = Success;
                executar.Tag = destino;
                executar.Visibility = Visibility.Visible;
                _fila.Marcar(new ItemFila(u.Nome, u.UrlDownload!, EstadoFila.Concluido, "assinatura verificada", null));
            }
            else
            {
                status.Text = $"✖ Verificação falhou: {Motivo(r)}. O arquivo NÃO será oferecido para execução.";
                status.Foreground = Danger;
                _fila.Marcar(new ItemFila(u.Nome, u.UrlDownload!, EstadoFila.Falhou, Motivo(r), null));
                try { File.Delete(destino); } catch { }
            }
        }
        catch (Exception ex)
        {
            status.Text = "Falha no download: " + ex.Message;
            status.Foreground = Danger;
            _fila.Marcar(new ItemFila(u.Nome, u.UrlDownload!, EstadoFila.Falhou, ex.Message, null));
        }
        finally { baixar.IsEnabled = true; AtualizarFila(); }
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

    static string NomeAmigavelWinget(string chave) => chave switch
    {
        "steam" => "Steam",
        "discord" => "Discord",
        "7zip" => "7-Zip",
        "chrome" => "Google Chrome",
        "vlc" => "VLC",
        _ => chave,
    };

    /// <summary>Instalação rápida via winget: SOMENTE os 5 IDs da allowlist confirmada na engenharia
    /// reversa. Cada execução exige confirmação explícita; nada roda sozinho.</summary>
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
            }
            botao.IsEnabled = true;
            AtualizarFila();
        };
        sp.Children.Add(botao);
        sp.Children.Add(status);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    static string NomeEstado(EstadoFila e) => e switch    {
        EstadoFila.Pendente => "Pendente",
        EstadoFila.Baixando => "Baixando…",
        EstadoFila.Verificando => "Verificando…",
        EstadoFila.Concluido => "✔ Concluído",
        _ => "✖ Falhou",
    };

    /// <summary>Cartão "Fila" no fim da lista: andamento persistido em JSON local (nada é enviado pela rede).</summary>
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
