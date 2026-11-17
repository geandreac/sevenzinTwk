using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Limpeza": varre áreas temporárias/cache conhecidas (só leitura ao Analisar) e, com sua
/// confirmação, envia os selecionados para a Lixeira (reversível). Esvaziar a Lixeira é a única ação
/// permanente e tem confirmação própria. Nenhum arquivo pessoal é tocado. Toda a lógica fica em
/// <see cref="Limpeza"/> (testável); aqui só UI.</summary>
public partial class LimpezaView : UserControl
{
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SecButton => (Style)Application.Current.Resources["SecondaryButton"];

    sealed record Controles(CheckBox? Selecao, TextBlock Tamanho);
    readonly Dictionary<CategoriaLimpeza, Controles> _ctrl = new();
    readonly Dictionary<CategoriaLimpeza, ResultadoAnalise> _ultima = new();

    public LimpezaView()
    {
        InitializeComponent();
        foreach (var a in Limpeza.Alvos) Lista.Children.Add(Cartao(a));
        // Mostra "Reabrir como administrador" só quando há alvo de sistema e ainda não estamos elevados.
        if (!Elevacao.EhAdmin() && Limpeza.Alvos.Any(a => a.ExigeAdmin))
            AdminBtn.Visibility = Visibility.Visible;
    }

    void Admin_Click(object sender, RoutedEventArgs e) => Elevacao.ReabrirComoAdmin();

    Border Cartao(AlvoLimpeza a)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool bloqueadoPorAdmin = a.ExigeAdmin && !Elevacao.EhAdmin();

        var esq = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        var titulo = new StackPanel { Orientation = Orientation.Horizontal };
        titulo.Children.Add(new TextBlock { Text = a.Nome, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (a.ExigeAdmin)
        {
            var tag = new Border { Background = Surface2, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            tag.Child = new TextBlock { Text = "requer admin", Foreground = Muted, FontSize = 10, FontWeight = FontWeights.SemiBold };
            titulo.Children.Add(tag);
        }
        esq.Children.Add(titulo);
        esq.Children.Add(new TextBlock { Text = a.Descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        var tam = new TextBlock { Text = bloqueadoPorAdmin ? "Reabra como administrador para medir e limpar." : "—", Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
        esq.Children.Add(tam);

        CheckBox? chk = null;
        if (a.Permanente)
        {
            esq.Margin = new Thickness(28, 0, 16, 0); // 18 (caixa) + 10 (gap): título na mesma coluna dos cartões com checkbox
            Grid.SetColumn(esq, 0);
            g.Children.Add(esq);
            var esvaziar = new Button { Style = SecButton, Content = "Esvaziar (permanente)", Padding = new Thickness(12, 7, 12, 7), VerticalAlignment = VerticalAlignment.Center };
            esvaziar.Click += (_, _) => Esvaziar(a);
            Grid.SetColumn(esvaziar, 1);
            g.Children.Add(esvaziar);
        }
        else
        {
            // O cartão inteiro é a área de clique: a caixa fica à esquerda, alinhada com o título (padrão de lista
            // selecionável), em vez de um quadradinho solto na borda direita. Alvos de sistema começam desmarcados
            // (opt-in) e ficam desabilitados sem admin.
            chk = new CheckBox
            {
                Content = esq, IsChecked = !a.ExigeAdmin, IsEnabled = !bloqueadoPorAdmin,
                VerticalContentAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(chk, a.Nome);
            chk.Checked += (_, _) => AtualizarResumo();
            chk.Unchecked += (_, _) => AtualizarResumo();
            Grid.SetColumnSpan(chk, 2);
            g.Children.Add(chk);
        }

        _ctrl[a.Id] = new Controles(chk, tam);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 12), Child = g };
    }

    async void Analisar_Click(object sender, RoutedEventArgs e)
    {
        AnalisarBtn.IsEnabled = false;
        ResumoTxt.Text = "Analisando…";
        foreach (var a in Limpeza.Alvos)
        {
            if (a.ExigeAdmin && !Elevacao.EhAdmin())
            {
                _ctrl[a.Id].Tamanho.Text = "Reabra como administrador para medir e limpar.";
                continue; // sem admin a varredura daria 0 enganoso; não conta no resumo
            }
            var r = await System.Threading.Tasks.Task.Run(() => Limpeza.Analisar(a.Id));
            _ultima[a.Id] = r;
            _ctrl[a.Id].Tamanho.Text = $"{Tam(r.Bytes)} · {r.Itens} item(ns)";
        }
        AnalisarBtn.IsEnabled = true;
        LimparBtn.IsEnabled = true;
        AtualizarResumo();
    }

    void AtualizarResumo()
    {
        long bytes = 0; int itens = 0;
        foreach (var a in Limpeza.Alvos)
            if (!a.Permanente && _ctrl[a.Id].Selecao?.IsChecked == true && _ultima.TryGetValue(a.Id, out var r))
            { bytes += r.Bytes; itens += r.Itens; }
        if (_ultima.Count == 0) { ResumoTxt.Text = "Clique em Analisar para medir o que pode ser limpo."; return; }
        ResumoTxt.Text = $"Recuperável nos selecionados: {Tam(bytes)} em {itens} item(ns) — vai para a Lixeira.";
    }

    async void Limpar_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = Limpeza.Alvos.Where(a => !a.Permanente && _ctrl[a.Id].Selecao?.IsChecked == true).ToList();
        if (selecionados.Count == 0)
        {
            Toasts.Mostrar(this, TipoToast.Aviso, "Nada selecionado", "Marque ao menos uma categoria para limpar.");
            return;
        }
        long bytes = selecionados.Sum(a => _ultima.TryGetValue(a.Id, out var r) ? r.Bytes : 0);
        int itens = selecionados.Sum(a => _ultima.TryGetValue(a.Id, out var r) ? r.Itens : 0);
        if (MessageBox.Show(
                $"Enviar à Lixeira os itens selecionados?\n\n{itens} item(ns) · {Tam(bytes)}.\n\nVai para a Lixeira — você pode restaurar. Nenhum arquivo pessoal é tocado.",
                "Confirmação — Limpeza", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            return;

        LimparBtn.IsEnabled = false;
        int totItens = 0, totErros = 0; long totBytes = 0;
        foreach (var a in selecionados)
        {
            var r = await System.Threading.Tasks.Task.Run(() => Limpeza.Limpar(a.Id));
            totItens += r.Itens; totErros += r.Erros; totBytes += r.Bytes;
            _ultima[a.Id] = await System.Threading.Tasks.Task.Run(() => Limpeza.Analisar(a.Id));
            _ctrl[a.Id].Tamanho.Text = $"{Tam(_ultima[a.Id].Bytes)} · {_ultima[a.Id].Itens} item(ns)";
        }
        LimparBtn.IsEnabled = true;
        AtualizarResumo();
        var resumo = totErros > 0
            ? $"{totItens} item(ns) · {Tam(totBytes)} à Lixeira; {totErros} em uso/ignorado(s)."
            : $"{totItens} item(ns) · {Tam(totBytes)} enviados à Lixeira.";
        NotificacoesStore.Padrao.Adicionar("Limpeza", "Limpeza concluída", resumo + " Restaure pela Lixeira do Windows se precisar.");
        Toasts.Mostrar(this, TipoToast.Sucesso, "Limpeza concluída", resumo);
    }

    void Esvaziar(AlvoLimpeza a)
    {
        var r0 = _ultima.TryGetValue(a.Id, out var u) ? u : Limpeza.Analisar(a.Id);
        if (r0.Itens == 0)
        {
            Toasts.Mostrar(this, TipoToast.Aviso, "Lixeira vazia", "Não há nada para esvaziar.");
            return;
        }
        if (MessageBox.Show(
                $"Esvaziar a Lixeira?\n\n{Tam(r0.Bytes)} em {r0.Itens} item(ns).\n\nATENÇÃO: isto é PERMANENTE e NÃO pode ser desfeito.",
                "Confirmação — Esvaziar a Lixeira", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var r = Limpeza.Limpar(a.Id);
        _ultima[a.Id] = Limpeza.Analisar(a.Id);
        _ctrl[a.Id].Tamanho.Text = $"{Tam(_ultima[a.Id].Bytes)} · {_ultima[a.Id].Itens} item(ns)";
        NotificacoesStore.Padrao.Adicionar("Limpeza", "Lixeira esvaziada", r.Mensagem + " (ação permanente)");
        Toasts.Mostrar(this, r.Erros == 0 ? TipoToast.Sucesso : TipoToast.Erro, r.Erros == 0 ? "Lixeira esvaziada" : "Falha ao esvaziar", r.Mensagem);
    }

    static string Tam(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}";
    }
}
