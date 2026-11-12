using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Jogos" (Figma, §21.4): perfil reversível por jogo — prioridade (nunca Realtime) e afinidade
/// (todos os núcleos ou só P-cores, quando a CPU é híbrida). "Aplicar ao abrir" aplica/restaura automaticamente.</summary>
public partial class JogosView : UserControl
{
    readonly CatalogoJogos _catalogo = new();
    readonly ControleProcessoWindows _controle = new();
    readonly GestorJogos _gestor;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly TopologiaCpu.Resultado? _topo = TopologiaCpu.Ler();
    string _filtro = "";

    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];

    public JogosView()
    {
        InitializeComponent();
        _gestor = new GestorJogos(_catalogo, _controle);
        _timer.Tick += (_, _) => { _gestor.Sincronizar(); Montar(); };
        Unloaded += (_, _) => _timer.Stop();
        Montar();
    }

    bool Hibrido => _topo?.Hibrido ?? false;
    long MascaraPCores => _topo?.MascaraPCores ?? 0;

    static string NomePrio(PrioridadeJogo p) => p switch
    {
        PrioridadeJogo.Alta => "Alta",
        PrioridadeJogo.AcimaDoNormal => "Acima do normal",
        _ => "Normal",
    };

    string NomeAfinidade(PerfilJogo j) =>
        j.MascaraAfinidade == 0 ? "Todos" : (j.MascaraAfinidade == MascaraPCores ? "P-cores" : "personalizada");

    void Montar()
    {
        Lista.Children.Clear();
        var jogos = _catalogo.Todos
            .Where(j => string.IsNullOrEmpty(_filtro) || j.Nome.Contains(_filtro, StringComparison.OrdinalIgnoreCase))
            .OrderBy(j => j.Nome).ToList();

        if (jogos.Count == 0)
        {
            Lista.Children.Add(new TextBlock
            {
                Text = _catalogo.Todos.Count == 0 ? "Nenhum jogo ainda. Clique em \"Adicionar jogo\" e escolha o .exe." : "Nenhum jogo corresponde à busca.",
                Foreground = Muted,
                Margin = new Thickness(2, 8, 0, 0),
            });
            return;
        }
        foreach (var j in jogos) Lista.Children.Add(Cartao(j));
    }

    Border Cartao(PerfilJogo j)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool aplicado = _gestor.EstaAplicado(j.Executavel);
        var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = j.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = j.Executavel, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        info.Children.Add(new TextBlock
        {
            Text = aplicado ? "● Aplicado agora (reversível)" : $"Perfil salvo · prioridade {NomePrio(j.Prioridade)} · afinidade {NomeAfinidade(j)}",
            Foreground = aplicado ? Success : Muted,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
        });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        acoes.Children.Add(Botao(Secundario, $"Prioridade: {NomePrio(j.Prioridade)}", () => CiclarPrioridade(j)));
        if (Hibrido) acoes.Children.Add(Botao(Secundario, $"Afinidade: {NomeAfinidade(j)}", () => CiclarAfinidade(j)));
        acoes.Children.Add(Botao(Primario, "Aplicar", () => Aplicar(j)));
        acoes.Children.Add(Botao(Secundario, "Reverter", () => Reverter(j)));
        acoes.Children.Add(Botao(Secundario, "Remover", () => Remover(j)));
        Grid.SetColumn(acoes, 1);
        grid.Children.Add(acoes);

        return new Border { Style = (Style)Application.Current.Resources["Card"], Margin = new Thickness(0, 0, 0, 12), Child = grid };
    }

    static Button Botao(Style estilo, string texto, Action aoClicar)
    {
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
        b.Click += (_, _) => aoClicar();
        return b;
    }

    void CiclarPrioridade(PerfilJogo j)
    {
        var prox = j.Prioridade switch
        {
            PrioridadeJogo.Normal => PrioridadeJogo.AcimaDoNormal,
            PrioridadeJogo.AcimaDoNormal => PrioridadeJogo.Alta,
            _ => PrioridadeJogo.Normal,
        };
        _catalogo.Adicionar(j with { Prioridade = prox });
        Montar();
    }

    void CiclarAfinidade(PerfilJogo j)
    {
        long prox = j.MascaraAfinidade == 0 ? MascaraPCores : 0; // alterna Todos ↔ P-cores
        _catalogo.Adicionar(j with { MascaraAfinidade = prox });
        Montar();
    }

    void Aplicar(PerfilJogo j)
    {
        if (_controle.ExesRodando(new[] { j.Executavel }).Count == 0)
        {
            StatusLbl.Text = $"'{j.Nome}' não está aberto — abra o jogo e clique em Aplicar.";
            return;
        }
        if (_gestor.AplicarAgora(j))
            StatusLbl.Text = $"'{j.Nome}' aplicado (prioridade {NomePrio(j.Prioridade)}, afinidade {NomeAfinidade(j)}) — reversível.";
        else
            StatusLbl.Text = $"Não foi possível aplicar em '{j.Nome}' (pode exigir administrador ou ser bloqueado por anti-cheat).";
        Montar();
    }

    void Reverter(PerfilJogo j)
    {
        StatusLbl.Text = _gestor.Reverter(j)
            ? $"'{j.Nome}' revertido ao estado anterior."
            : $"'{j.Nome}' não foi aplicado por aqui — nada a reverter.";
        Montar();
    }

    void Remover(PerfilJogo j)
    {
        _gestor.Reverter(j);
        _catalogo.Remover(j.Executavel);
        Montar();
        StatusLbl.Text = $"'{j.Nome}' removido.";
    }

    void Adicionar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Selecione o executável do jogo", Filter = "Executável (*.exe)|*.exe" };
        if (dlg.ShowDialog() != true) return;
        var nome = Path.GetFileNameWithoutExtension(dlg.FileName);
        _catalogo.Adicionar(new PerfilJogo(nome, dlg.FileName, 0, PrioridadeJogo.AcimaDoNormal));
        Montar();
        StatusLbl.Text = $"'{nome}' adicionado. Abra o jogo e clique em Aplicar (ou ligue \"Aplicar ao abrir\").";
    }

    void Auto_Click(object sender, RoutedEventArgs e)
    {
        if (AutoSwitch.IsChecked == true)
        {
            _gestor.Sincronizar();
            Montar();
            _timer.Start();
            StatusLbl.Text = "Modo automático ligado: perfis aplicados ao abrir o jogo e restaurados ao fechar.";
        }
        else
        {
            _timer.Stop();
            StatusLbl.Text = "Modo automático desligado. Os perfis já aplicados continuam até você reverter ou o jogo fechar na próxima verificação.";
        }
    }

    void Busca_Changed(object sender, TextChangedEventArgs e)
    {
        _filtro = BuscaBox.Text?.Trim() ?? "";
        Montar();
    }
}
