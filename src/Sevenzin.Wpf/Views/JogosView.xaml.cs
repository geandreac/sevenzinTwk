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
    List<JogoDetectado>? _detectados; // null = ainda não pediu a detecção
    bool _detectando;

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
                Text = _catalogo.Todos.Count == 0 ? "Nenhum jogo ainda. Use \"Detectar jogos instalados\" abaixo ou \"Adicionar jogo\" para escolher um .exe." : "Nenhum jogo corresponde à busca.",
                Foreground = Muted,
                Margin = new Thickness(2, 8, 0, 0),
            });
        }
        foreach (var j in jogos) Lista.Children.Add(Cartao(j));
        Lista.Children.Add(CartaoDetectados());
    }

    /// <summary>Seção "Jogos detectados" (Steam/Epic, só leitura): lista o que está instalado e deixa o
    /// usuário adicionar ao catálogo — NADA é aplicado automaticamente.</summary>
    Border CartaoDetectados()
    {
        var sp = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(new TextBlock { Text = "JOGOS DETECTADOS (STEAM/EPIC)", Style = (Style)Application.Current.Resources["SectionLabel"] });
        sp.Children.Add(new TextBlock
        {
            Text = "Só leitura: o Sevenzin procura instalações nos manifestos locais (nada é executado). Adicionar cria o perfil — aplicar continua sendo manual ou pelo \"Aplicar ao abrir\".",
            Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 8), TextWrapping = TextWrapping.Wrap,
        });

        var botoes = new StackPanel { Orientation = Orientation.Horizontal };
        var detectar = Botao(Secundario, _detectando ? "Detectando…" : "Detectar jogos instalados", () => DetectarAsync());
        detectar.IsEnabled = !_detectando;
        botoes.Children.Add(detectar);
        sp.Children.Add(botoes);

        if (_detectados is { } lista)
        {
            if (lista.Count == 0)
                sp.Children.Add(new TextBlock { Text = "Nenhum jogo detectado (launchers ausentes ou sem instalações).", Foreground = Muted, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) });
            foreach (var d in lista) sp.Children.Add(LinhaDetectado(d));
        }
        return new Border { Style = (Style)Application.Current.Resources["Card"], Margin = new Thickness(0, 0, 0, 12), Child = sp };
    }

    async void DetectarAsync()
    {
        _detectando = true;
        Montar();
        _detectados = await System.Threading.Tasks.Task.Run(DescobridorJogos.DetectarTudo);
        _detectando = false;
        Montar();
        StatusLbl.Text = _detectados is { Count: > 0 } d
            ? $"{d.Count} jogo(s) detectado(s) — clique em Adicionar para criar o perfil (nada é aplicado automaticamente)."
            : "Nenhum jogo detectado (launchers ausentes ou sem instalações).";
    }

    Grid LinhaDetectado(JogoDetectado d)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool jaExiste = d.Executavel is { } exe && _catalogo.Todos.Any(j => j.Executavel.Equals(exe, StringComparison.OrdinalIgnoreCase));
        var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        var titulo = new StackPanel { Orientation = Orientation.Horizontal };
        var badge = new Border
        {
            Background = (Brush)Application.Current.Resources["BrandTintBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = d.Plataforma, FontSize = 10, FontWeight = FontWeights.SemiBold },
        };
        titulo.Children.Add(badge);
        titulo.Children.Add(new TextBlock { Text = d.Nome, FontSize = 14, FontWeight = FontWeights.SemiBold });
        info.Children.Add(titulo);
        var detalhe = d.Executavel ?? (d.PastaInstalacao.Length > 0 ? $"pasta: {d.PastaInstalacao}" : "executável não resolvido");
        info.Children.Add(new TextBlock { Text = detalhe, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = detalhe });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (jaExiste)
        {
            var ok = new TextBlock { Text = "✓ no catálogo", Foreground = Success, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            acoes.Children.Add(ok);
        }
        else
        {
            acoes.Children.Add(Botao(Primario, "Adicionar ao catálogo", () => AdicionarDetectado(d)));
        }
        Grid.SetColumn(acoes, 1);
        grid.Children.Add(acoes);
        return grid;
    }

    void AdicionarDetectado(JogoDetectado d)
    {
        string? exe = d.Executavel is { } e && File.Exists(e) ? e : null;
        if (exe is null)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Selecione o executável de {d.Nome}",
                Filter = "Executável (*.exe)|*.exe",
                InitialDirectory = d.PastaInstalacao.Length > 0 && Directory.Exists(d.PastaInstalacao) ? d.PastaInstalacao : null,
            };
            if (dlg.ShowDialog() != true) return;
            exe = dlg.FileName;
        }
        _catalogo.Adicionar(new PerfilJogo(d.Nome, exe, 0, PrioridadeJogo.AcimaDoNormal));
        Montar();
        StatusLbl.Text = $"'{d.Nome}' adicionado ao catálogo (perfil padrão). Abra o jogo e clique em Aplicar — nada foi aplicado automaticamente.";
    }

    // Card empilhado na vertical: cabeçalho (nome/caminho/status) → ações → opt-ins. As duas faixas de
    // botões são WrapPanel, então envolvem conforme a largura (nada se sobrepõe nem é cortado em janela
    // pequena/não maximizada). O caminho usa elipse + tooltip para não quebrar o layout.
    Border Cartao(PerfilJogo j)
    {
        bool aplicado = _gestor.EstaAplicado(j.Executavel);
        var raiz = new StackPanel();
        raiz.Children.Add(new TextBlock { Text = j.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        raiz.Children.Add(new TextBlock { Text = j.Executavel, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = j.Executavel });
        raiz.Children.Add(new TextBlock
        {
            Text = aplicado ? "● Aplicado agora (reversível)" : $"Perfil salvo · prioridade {NomePrio(j.Prioridade)} · afinidade {NomeAfinidade(j)}",
            Foreground = aplicado ? Success : Muted,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
        });

        var acoes = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        acoes.Children.Add(Botao(Secundario, $"Prioridade: {NomePrio(j.Prioridade)}", () => CiclarPrioridade(j)));
        if (Hibrido) acoes.Children.Add(Botao(Secundario, $"Afinidade: {NomeAfinidade(j)}", () => CiclarAfinidade(j)));
        acoes.Children.Add(Botao(Primario, "Aplicar", () => Aplicar(j)));
        acoes.Children.Add(Botao(Secundario, "Reverter", () => Reverter(j)));
        acoes.Children.Add(Botao(Secundario, "Remover", () => Remover(j)));
        raiz.Children.Add(acoes);

        // Otimizações OPT-IN com aviso (working set, timer, GPU alta, boost, CPU Sets).
        var optins = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        optins.Children.Add(Botao(Secundario, $"Working set: {(j.TrimWorkingSet ? "trim" : "não")}", () => AlternarWorkingSet(j)));
        optins.Children.Add(Botao(Secundario, $"Timer 1 ms: {(j.TimerAltaResolucao ? "sim" : "não")}", () => AlternarTimer(j)));
        optins.Children.Add(Botao(Secundario, $"GPU: {(j.PrefereGpuAlta ? "alto desempenho" : "automática")}", () => AlternarGpu(j)));
        optins.Children.Add(Botao(Secundario, $"Boost: {(j.PriorityBoost ? "sim" : "não")}", () => AlternarBoost(j)));
        if (Hibrido) optins.Children.Add(Botao(Secundario, $"Núcleos P (CPU Sets): {(j.CpuSetsPCores ? "sim" : "não")}", () => AlternarCpuSets(j)));
        raiz.Children.Add(optins);

        return new Border { Style = (Style)Application.Current.Resources["Card"], Margin = new Thickness(0, 0, 0, 12), Child = raiz };
    }

    static Button Botao(Style estilo, string texto, Action aoClicar)
    {
        // margem inferior 8 dá respiro entre faixas quando os botões envolvem (WrapPanel).
        var b = new Button { Style = estilo, Content = texto, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 7, 12, 7) };
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

    void AlternarWorkingSet(PerfilJogo j)
    {
        _catalogo.Adicionar(j with { TrimWorkingSet = !j.TrimWorkingSet });
        Montar();
        StatusLbl.Text = j.TrimWorkingSet
            ? "Trim de working set ligado para aplicar — pode causar micro-travadas logo após aplicar (memória volta a crescer)."
            : "Trim de working set desligado.";
    }

    void AlternarTimer(PerfilJogo j)
    {
        _catalogo.Adicionar(j with { TimerAltaResolucao = !j.TimerAltaResolucao });
        Montar();
        StatusLbl.Text = j.TimerAltaResolucao
            ? "Timer de 1 ms ligado — ATENÇÃO: aumenta o consumo de energia enquanto o jogo estiver aplicado; revertido ao fechar."
            : "Timer de alta resolução desligado.";
    }

    void AlternarGpu(PerfilJogo j)
    {
        _catalogo.Adicionar(j with { PrefereGpuAlta = !j.PrefereGpuAlta });
        Montar();
        StatusLbl.Text = j.PrefereGpuAlta
            ? "GPU de alto desempenho marcada para este .exe (UserGpuPreferences) — aplicada com o perfil e removida ao reverter."
            : "Preferência de GPU removida (volta à automática).";
    }

    void AlternarBoost(PerfilJogo j)
    {
        _catalogo.Adicionar(j with { PriorityBoost = !j.PriorityBoost });
        Montar();
        StatusLbl.Text = j.PriorityBoost
            ? "Boost de prioridade ligado — temporário (revertido ao fechar)."
            : "Boost de prioridade desligado.";
    }

    void AlternarCpuSets(PerfilJogo j)
    {
        _catalogo.Adicionar(j with { CpuSetsPCores = !j.CpuSetsPCores });
        Montar();
        StatusLbl.Text = j.CpuSetsPCores
            ? "Núcleos P (CPU Sets) ligado — os threads do jogo passam a preferir os P-cores enquanto aplicado; revertido ao fechar."
            : "Núcleos P (CPU Sets) desligado — volta ao agendamento padrão do Windows.";
    }

    void Aplicar(PerfilJogo j)
    {
        if (_controle.ExesRodando(new[] { j.Executavel }).Count == 0)
        {
            StatusLbl.Text = $"'{j.Nome}' não está aberto — abra o jogo e clique em Aplicar.";
            return;
        }
        if (_gestor.AplicarAgora(j))
        {
            var extras = new List<string>();
            if (j.TrimWorkingSet) extras.Add("trim de working set");
            if (j.TimerAltaResolucao) extras.Add("timer 1 ms (revertido ao fechar)");
            var textoExtras = extras.Count > 0 ? $" · {string.Join(" + ", extras)}" : "";
            StatusLbl.Text = $"'{j.Nome}' aplicado (prioridade {NomePrio(j.Prioridade)}, afinidade {NomeAfinidade(j)}{textoExtras}) — reversível.";
        }
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
