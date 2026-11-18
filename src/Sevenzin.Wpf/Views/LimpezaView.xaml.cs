using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>
/// Tela "Limpeza": varre áreas temporárias/cache conhecidas e, em "Limpeza Total",
/// deleta os itens selecionados permanentemente do computador (pulando a etapa da Lixeira) + esvazia a Lixeira,
/// sempre com confirmação explícita e sem tocar em nenhum arquivo pessoal.
/// </summary>
public partial class LimpezaView : UserControl
{
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SecButton => (Style)Application.Current.Resources["SecondaryButton"];

    sealed record Controles(CheckBox Selecao, TextBlock Tamanho);
    readonly Dictionary<CategoriaLimpeza, Controles> _ctrl = new();
    readonly Dictionary<CategoriaLimpeza, ResultadoAnalise> _ultima = new();

    public LimpezaView()
    {
        InitializeComponent();
        foreach (var a in Limpeza.Alvos) Lista.Children.Add(Cartao(a));
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
            tag.Child = new TextBlock { Text = "admin", Foreground = Muted, FontSize = 10, FontWeight = FontWeights.SemiBold };
            titulo.Children.Add(tag);
        }
        esq.Children.Add(titulo);
        esq.Children.Add(new TextBlock { Text = a.Descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        var tam = new TextBlock { Text = bloqueadoPorAdmin ? "Reabra como administrador para medir e limpar." : "—", Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
        esq.Children.Add(tam);

        var chk = new CheckBox
        {
            Content = esq,
            IsChecked = !bloqueadoPorAdmin,
            IsEnabled = !bloqueadoPorAdmin,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(chk, a.Nome);
        chk.Checked += (_, _) => AtualizarResumo();
        chk.Unchecked += (_, _) => AtualizarResumo();
        Grid.SetColumn(chk, 0);
        g.Children.Add(chk);

        if (a.Permanente)
        {
            var esvaziar = new Button { Style = SecButton, Content = "Esvaziar agora", Padding = new Thickness(12, 7, 12, 7), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            esvaziar.Click += (_, _) => Esvaziar(a);
            Grid.SetColumn(esvaziar, 1);
            g.Children.Add(esvaziar);
        }
        else
        {
            Grid.SetColumnSpan(chk, 2);
        }

        _ctrl[a.Id] = new Controles(chk, tam);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 12), Child = g };
    }

    async Task ExecutarAnaliseAsync()
    {
        AnalisarBtn.IsEnabled = false;
        LimparBtn.IsEnabled = false;
        ResumoTxt.Text = "Analisando…";
        foreach (var a in Limpeza.Alvos)
        {
            if (a.ExigeAdmin && !Elevacao.EhAdmin())
            {
                _ctrl[a.Id].Tamanho.Text = "Reabra como administrador para medir e limpar.";
                continue;
            }
            var r = await Task.Run(() => Limpeza.Analisar(a.Id));
            _ultima[a.Id] = r;
            _ctrl[a.Id].Tamanho.Text = $"{Tam(r.Bytes)} · {r.Itens} item(ns)";
        }
        AnalisarBtn.IsEnabled = true;
        LimparBtn.IsEnabled = true;
        AtualizarResumo();
    }

    async void Analisar_Click(object sender, RoutedEventArgs e) => await ExecutarAnaliseAsync();

    void AtualizarResumo()
    {
        long bytes = 0; int itens = 0;
        foreach (var a in Limpeza.Alvos)
            if (_ctrl[a.Id].Selecao.IsChecked == true && _ultima.TryGetValue(a.Id, out var r))
            { bytes += r.Bytes; itens += r.Itens; }
        if (_ultima.Count == 0)
        {
            ResumoTxt.Text = "Clique em Analisar para medir ou em Limpeza Total para excluir direto do computador.";
            return;
        }
        ResumoTxt.Text = $"Selecionado para exclusão direta: {Tam(bytes)} em {itens} item(ns) (pula a Lixeira).";
    }

    async void Limpar_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = Limpeza.Alvos.Where(a => _ctrl[a.Id].Selecao.IsChecked == true && (!a.ExigeAdmin || Elevacao.EhAdmin())).ToList();
        if (selecionados.Count == 0)
        {
            Toasts.Mostrar(this, TipoToast.Aviso, "Nada selecionado", "Marque ao menos uma categoria para executar a Limpeza Total.");
            return;
        }

        if (_ultima.Count == 0)
            await ExecutarAnaliseAsync();

        long bytes = selecionados.Sum(a => _ultima.TryGetValue(a.Id, out var r) ? r.Bytes : 0);
        int itens = selecionados.Sum(a => _ultima.TryGetValue(a.Id, out var r) ? r.Itens : 0);
        if (MessageBox.Show(
                $"Executar a Limpeza Total agora?\n\n• {itens} item(ns) · {Tam(bytes)} nas {selecionados.Count} categoria(s) marcadas.\n• Os arquivos serão DELETADOS DIRETAMENTE do computador, pulando a etapa da Lixeira.\n• Nenhum arquivo pessoal é tocado e itens em uso pelo sistema são ignorados.",
                "Confirmação — Limpeza Total (Exclusão Direta)", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        AnalisarBtn.IsEnabled = false;
        LimparBtn.IsEnabled = false;
        ResumoTxt.Text = "Limpando permanentemente…";
        int totItens = 0, totErros = 0; long totBytes = 0;
        try
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso("Limpeza Total");
            foreach (var a in selecionados)
            {
                var r = await Task.Run(() => Limpeza.LimparPermanente(a.Id));
                totItens += r.Itens; totErros += r.Erros; totBytes += r.Bytes;
                _ultima[a.Id] = await Task.Run(() => Limpeza.Analisar(a.Id));
                _ctrl[a.Id].Tamanho.Text = $"{Tam(_ultima[a.Id].Bytes)} · {_ultima[a.Id].Itens} item(ns)";
            }
        }
        finally
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso(null);
            AnalisarBtn.IsEnabled = true;
            LimparBtn.IsEnabled = true;
        }

        AtualizarResumo();
        var resumo = totErros > 0
            ? $"{totItens} item(ns) ({Tam(totBytes)}) deletados do PC; {totErros} em uso/ignorado(s)."
            : $"{totItens} item(ns) ({Tam(totBytes)}) deletados permanentemente do computador.";
        NotificacoesStore.Padrao.Adicionar("Limpeza", "Limpeza Total concluída", resumo + " (exclusão direta sem Lixeira)");
        Toasts.Mostrar(this, TipoToast.Sucesso, "Limpeza Total concluída", resumo);
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

        var r = Limpeza.LimparPermanente(a.Id);
        _ultima[a.Id] = Limpeza.Analisar(a.Id);
        _ctrl[a.Id].Tamanho.Text = $"{Tam(_ultima[a.Id].Bytes)} · {_ultima[a.Id].Itens} item(ns)";
        AtualizarResumo();
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
