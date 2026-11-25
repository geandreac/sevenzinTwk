using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Jogos" (Figma, §21.4): perfil reversível por jogo — prioridade, afinidade (P-cores, Excluir Core 0,
/// Sem SMT, 1º CCD), compatibilidade FSE/HighDPI, inicialização direta do jogo e controle avançado de prioridade por PID.</summary>
public partial class JogosView : UserControl
{
    readonly CatalogoJogos _catalogo = new();
    readonly ControleProcessoWindows _controle = new();
    readonly GestorJogos _gestor;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly TopologiaCpu.Resultado? _topo = TopologiaCpu.Ler();
    string _filtro = "";
    List<JogoDetectado>? _detectados;
    bool _detectando;
    Grid? _overlay;
    string? _perfilAberto;

    static Style Primario => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Secundario => (Style)Application.Current.Resources["SecondaryButton"];
    static Style DarkCombo => (Style)Application.Current.Resources["DarkCombo"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static Brush Primary => (Brush)Application.Current.Resources["TextPrimaryBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Brush Borda => (Brush)Application.Current.Resources["BorderDefaultBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionLabel => (Style)Application.Current.Resources["SectionLabel"];

    public JogosView()
    {
        InitializeComponent();
        _gestor = new GestorJogos(_catalogo, _controle);
        _timer.Tick += (_, _) => { _gestor.Sincronizar(); Montar(); };
        Unloaded += (_, _) => _timer.Stop();
        Montar();
    }

    bool Hibrido => _topo?.Hibrido ?? false;
    int DominiosL3 => _topo?.DominiosL3 ?? 0;
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
        Lista.Children.Add(CartaoPrioridadeProcesso());
    }

    Border CartaoDetectados()
    {
        var sp = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(new TextBlock { Text = "Jogos detectados (Steam e Epic)", Style = SectionLabel });
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
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 12), Child = sp };
    }

    Border CartaoPrioridadeProcesso()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Prioridade de um processo aberto", Style = SectionLabel });
        sp.Children.Add(new TextBlock
        {
            Text = "Ajuste temporário da classe de prioridade de um processo aberto (não persiste após fechar). Processos críticos do Windows são bloqueados contra Tempo Real.",
            Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap,
        });

        var comboProc = new ComboBox { Style = DarkCombo, MinWidth = 260, Margin = new Thickness(0, 0, 8, 8) };
        var comboNivel = new ComboBox { Style = DarkCombo, Width = 170, Margin = new Thickness(0, 0, 8, 8) };
        foreach (var (rotulo, niv) in new[]
        {
            ("Normal (padrão)", NivelPrioridade.Normal),
            ("Acima do normal", NivelPrioridade.AcimaNormal),
            ("Alta", NivelPrioridade.Alto),
            ("Abaixo do normal", NivelPrioridade.AbaixoNormal),
            ("Ocioso", NivelPrioridade.Ocioso),
            ("Tempo Real (avançado)", NivelPrioridade.TempoReal),
        })
        {
            comboNivel.Items.Add(new ComboBoxItem { Content = rotulo, Tag = niv });
        }
        comboNivel.SelectedIndex = 2; // Alta por padrão

        void PopularProcessos()
        {
            comboProc.Items.Clear();
            try
            {
                var procs = Process.GetProcesses()
                    .Where(p =>
                    {
                        try { return p.Id > 4 && !PrioridadeProcesso.EhCritico(p.ProcessName) && p.MainWindowHandle != IntPtr.Zero; }
                        catch { return false; }
                    })
                    .OrderBy(p => p.ProcessName)
                    .Take(50)
                    .ToList();
                foreach (var p in procs)
                {
                    comboProc.Items.Add(new ComboBoxItem
                    {
                        Content = $"{p.ProcessName} (PID {p.Id})",
                        Tag = (p.Id, p.ProcessName),
                    });
                }
                if (comboProc.Items.Count > 0) comboProc.SelectedIndex = 0;
            }
            catch { }
        }
        PopularProcessos();

        var wrap = new WrapPanel();
        wrap.Children.Add(comboProc);
        wrap.Children.Add(comboNivel);
        wrap.Children.Add(Botao(Secundario, "Atualizar lista", PopularProcessos));
        wrap.Children.Add(Botao(Primario, "Aplicar prioridade", () =>
        {
            if ((comboProc.SelectedItem as ComboBoxItem)?.Tag is not ValueTuple<int, string> alvo)
            {
                Toasts.Mostrar(this, TipoToast.Aviso, "Selecione um processo", "Nenhum processo com janela visível selecionado.");
                return;
            }
            var nivel = (comboNivel.SelectedItem as ComboBoxItem)?.Tag is NivelPrioridade np ? np : NivelPrioridade.Normal;
            var erroVal = PrioridadeProcesso.Validar(nivel, alvo.Item2);
            if (erroVal is not null)
            {
                Toasts.Mostrar(this, TipoToast.Erro, "Processo protegido", erroVal);
                return;
            }
            if (nivel == NivelPrioridade.TempoReal)
            {
                if (!Elevacao.EhAdmin())
                {
                    MessageBox.Show("Tempo Real exige executar o Sevenzin como Administrador.", "Elevação necessária", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (MessageBox.Show($"Definir '{alvo.Item2}' (PID {alvo.Item1}) em prioridade TEMPO REAL?\n\nATENÇÃO: se o processo consumir 100% da CPU, mouse, áudio e entrada podem congelar.\nReversão: clique em 'Restaurar Normal' ou feche o processo.\n\nContinuar?",
                        "Confirmação — Prioridade Tempo Real", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }
            var res = PrioridadeProcesso.Aplicar(alvo.Item1, nivel);
            if (res.Ok) NotificacoesStore.Padrao.Adicionar("Jogos", $"Prioridade {nivel} em {alvo.Item2}", res.Mensagem);
            Toasts.Mostrar(this, res.Ok ? TipoToast.Sucesso : TipoToast.Erro, res.Ok ? "Prioridade ajustada" : "Falha", res.Mensagem,
                res.Ok ? (Action?)(() => PrioridadeProcesso.Aplicar(alvo.Item1, NivelPrioridade.Normal)) : null);
        }));
        wrap.Children.Add(Botao(Secundario, "Restaurar Normal", () =>
        {
            if ((comboProc.SelectedItem as ComboBoxItem)?.Tag is not ValueTuple<int, string> alvo) return;
            var res = PrioridadeProcesso.Aplicar(alvo.Item1, NivelPrioridade.Normal);
            Toasts.Mostrar(this, res.Ok ? TipoToast.Sucesso : TipoToast.Erro, res.Ok ? "Restaurado" : "Falha", res.Mensagem);
        }));
        sp.Children.Add(wrap);

        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    async void DetectarAsync()
    {
        _detectando = true;
        Montar();
        _detectados = await Task.Run(DescobridorJogos.DetectarTudo);
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
            acoes.Children.Add(new TextBlock { Text = "✓ no catálogo", Foreground = Success, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
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

    Border Cartao(PerfilJogo j)
    {
        bool aplicado = _gestor.EstaAplicado(j.Executavel);
        var raiz = new StackPanel();

        var topo = new Grid();
        var esq = new StackPanel();
        esq.Children.Add(new TextBlock { Text = j.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        esq.Children.Add(new TextBlock { Text = j.Executavel, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = j.Executavel });
        topo.Children.Add(esq);
        var selo = new Border { Background = aplicado ? SuccessBg : Surface2, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        selo.Child = new TextBlock { Text = aplicado ? "Aplicado" : "Perfil salvo", Foreground = aplicado ? Success : Muted, FontSize = 11, FontWeight = FontWeights.SemiBold };
        topo.Children.Add(selo);
        raiz.Children.Add(topo);

        raiz.Children.Add(new TextBlock { Text = Resumo(j), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });

        var acoes = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        acoes.Children.Add(Botao(Primario, "Perfil e otimizações", () => AbrirPerfil(j.Executavel)));
        acoes.Children.Add(Botao(Secundario, aplicado ? "Reverter" : "Aplicar", () => { if (aplicado) Reverter(j); else Aplicar(j); }));
        acoes.Children.Add(Botao(Secundario, "Iniciar jogo", () => IniciarJogo(j)));
        raiz.Children.Add(acoes);

        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 12), Child = raiz };
    }

    void IniciarJogo(PerfilJogo j)
    {
        if (AplicadorPerfil.IniciarExecutavelJogo(j.Executavel, out var msg))
        {
            StatusLbl.Text = $"{msg} Ligue 'Aplicar ao abrir' ou clique em 'Aplicar' após a janela iniciar.";
            Toasts.Mostrar(this, TipoToast.Sucesso, "Jogo iniciado", j.Nome);
        }
        else
        {
            StatusLbl.Text = msg;
            Toasts.Mostrar(this, TipoToast.Aviso, "Não foi possível iniciar", msg);
        }
    }

    static string Resumo(PerfilJogo j)
    {
        var extras = new List<string>();
        if (j.ExcluirCore0) extras.Add("sem Core 0");
        if (j.DesativarSmt) extras.Add("sem SMT");
        if (j.ApenasUmCcd) extras.Add("1º CCD");
        if (j.ModoCompatibilidade) extras.Add("compat FSE/DPI");
        if (j.TrimWorkingSet) extras.Add("working set");
        if (j.TimerAltaResolucao) extras.Add("timer 1 ms");
        if (j.PrefereGpuAlta) extras.Add("GPU alta");
        if (j.PriorityBoost) extras.Add("boost");
        if (j.CpuSetsPCores) extras.Add("CPU Sets P");
        return extras.Count > 0
            ? $"Prioridade {NomePrio(j.Prioridade)} · {extras.Count} otimização(ões) extra: {string.Join(", ", extras)}"
            : $"Prioridade {NomePrio(j.Prioridade)} · sem otimizações extras";
    }

    void AbrirPerfil(string exe)
    {
        _perfilAberto = exe;
        RenderPerfil();
        if (_overlay is { } o) Movimento.AbrirModal(o.Children[0], o.Children[1]);
    }

    void FecharPerfil()
    {
        _perfilAberto = null;
        if (_overlay is { } o) { _overlay = null; Movimento.Sair(o, () => Raiz.Children.Remove(o)); }
    }

    void RenderPerfil()
    {
        if (_overlay is not null) { Raiz.Children.Remove(_overlay); _overlay = null; }
        var j = _catalogo.Todos.FirstOrDefault(x => x.Executavel.Equals(_perfilAberto, StringComparison.OrdinalIgnoreCase));
        if (j is null) { _perfilAberto = null; return; }
        bool aplicado = _gestor.EstaAplicado(j.Executavel);

        var card = new StackPanel();

        var cab = new Grid();
        var hesq = new StackPanel();
        hesq.Children.Add(new TextBlock { Text = j.Nome, FontSize = 18, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        cab.Children.Add(hesq);
        var fechar = new Button { Style = Secundario, Content = "Fechar", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(10, 5, 10, 5) };
        fechar.Click += (_, _) => FecharPerfil();
        cab.Children.Add(fechar);
        card.Children.Add(cab);

        card.Children.Add(new TextBlock { Text = aplicado ? "● Aplicado agora (reversível)" : "Perfil salvo — nada aplicado ainda", Foreground = aplicado ? Success : Muted, FontSize = 12, Margin = new Thickness(0, 8, 0, 12) });

        var grade = new UniformGrid { Columns = 2 };
        grade.Children.Add(Tile("Prioridade", NomePrio(j.Prioridade), j.Prioridade != PrioridadeJogo.Normal, () => { CiclarPrioridade(j); RenderPerfil(); }));
        if (Hibrido) grade.Children.Add(Tile("Afinidade P/E", NomeAfinidade(j), j.MascaraAfinidade != 0, () => { CiclarAfinidade(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Excluir Core 0", j.ExcluirCore0 ? "Sem núcleo 0 (menos DPC/IRQ)" : "Todos os núcleos", j.ExcluirCore0, () => { AlternarExcluirCore0(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Sem SMT / HT", j.DesativarSmt ? "1 thread por núcleo físico" : "SMT padrão", j.DesativarSmt, () => { AlternarSmt(j); RenderPerfil(); }));
        grade.Children.Add(Tile("1º CCD (Cache L3)", j.ApenasUmCcd ? "Fixado no 1º CCD" : (DominiosL3 > 1 ? $"{DominiosL3} CCDs disponíveis" : "CCD único detectado"), j.ApenasUmCcd, () => { AlternarCcd(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Compatibilidade FSE/DPI", j.ModoCompatibilidade ? "FSE off + HighDPI Aware" : "Padrão do Windows", j.ModoCompatibilidade, () => { AlternarCompat(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Working set", j.TrimWorkingSet ? "Trim ao aplicar" : "Padrão", j.TrimWorkingSet, () => { AlternarWorkingSet(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Timer 1 ms", j.TimerAltaResolucao ? "Ligado (mais energia)" : "Desligado", j.TimerAltaResolucao, () => { AlternarTimer(j); RenderPerfil(); }));
        grade.Children.Add(Tile("GPU", j.PrefereGpuAlta ? "Alto desempenho" : "Automática", j.PrefereGpuAlta, () => { AlternarGpu(j); RenderPerfil(); }));
        grade.Children.Add(Tile("Boost de prioridade", j.PriorityBoost ? "Ligado" : "Desligado", j.PriorityBoost, () => { AlternarBoost(j); RenderPerfil(); }));
        if (Hibrido) grade.Children.Add(Tile("Núcleos P (CPU Sets)", j.CpuSetsPCores ? "Preferir P-cores" : "Padrão", j.CpuSetsPCores, () => { AlternarCpuSets(j); RenderPerfil(); }));
        card.Children.Add(grade);

        card.Children.Add(new TextBlock { Text = "Executável do jogo", Style = SectionLabel, Margin = new Thickness(0, 10, 0, 2) });
        card.Children.Add(new TextBlock { Text = j.Executavel, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = j.Executavel });

        var rodape = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        rodape.Children.Add(Botao(Primario, "Aplicar", () => { Aplicar(j); RenderPerfil(); }));
        rodape.Children.Add(Botao(Secundario, "Iniciar jogo", () => IniciarJogo(j)));
        rodape.Children.Add(Botao(Secundario, "Reverter", () => { Reverter(j); RenderPerfil(); }));
        rodape.Children.Add(Botao(Secundario, "Remover", () => { Remover(j); FecharPerfil(); }));
        card.Children.Add(rodape);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 580, Content = card };
        var painel = new Border { Style = CardStyle, Child = scroll, Width = 580, MaxWidth = 580, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24) };

        var scrim = new Border { Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)) };
        scrim.MouseLeftButtonDown += (_, _) => FecharPerfil();

        _overlay = new Grid();
        _overlay.Children.Add(scrim);
        _overlay.Children.Add(painel);
        Grid.SetRowSpan(_overlay, 4);
        Panel.SetZIndex(_overlay, 50);
        Raiz.Children.Add(_overlay);
    }

    Border Tile(string titulo, string estado, bool ativo, Action aoClicar)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = titulo, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = ativo ? Success : Primary });
        sp.Children.Add(new TextBlock { Text = estado, FontSize = 11, Foreground = Muted, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        var b = new Border
        {
            Background = Surface2,
            BorderBrush = ativo ? Success : Borda,
            BorderThickness = new Thickness(ativo ? 1.4 : 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = Cursors.Hand,
            Child = sp,
        };
        b.MouseLeftButtonUp += (_, _) => aoClicar();
        return b;
    }

    static Button Botao(Style estilo, string texto, Action aoClicar)
    {
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
        long prox = j.MascaraAfinidade == 0 ? MascaraPCores : 0;
        _catalogo.Adicionar(j with { MascaraAfinidade = prox });
        Montar();
    }

    void AlternarExcluirCore0(PerfilJogo j)
    {
        var novo = j with { ExcluirCore0 = !j.ExcluirCore0 };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.ExcluirCore0
            ? "Excluir Core 0 ligado — remove os threads do núcleo 0 (onde costumam cair DPCs/IRQs do Windows); revertido ao fechar."
            : "Excluir Core 0 desligado.";
    }

    void AlternarSmt(PerfilJogo j)
    {
        var novo = j with { DesativarSmt = !j.DesativarSmt };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.DesativarSmt
            ? "Sem SMT ligado — o jogo roda apenas no 1º thread de cada núcleo físico; meça antes/depois (revertido ao fechar)."
            : "Sem SMT desligado.";
    }

    void AlternarCcd(PerfilJogo j)
    {
        var novo = j with { ApenasUmCcd = !j.ApenasUmCcd };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.ApenasUmCcd
            ? "1º CCD ligado — em CPUs multi-CCD evita latência entre dies (Infinity Fabric); sem efeito negativo em CPU de 1 CCD."
            : "1º CCD desligado.";
    }

    void AlternarCompat(PerfilJogo j)
    {
        var novo = j with { ModoCompatibilidade = !j.ModoCompatibilidade };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.ModoCompatibilidade
            ? "Compatibilidade FSE/HighDPI marcada (DISABLEDXMAXIMIZEDWINDOWEDMODE + HIGHDPIAWARE) — gravada ao aplicar e restaurada ao reverter."
            : "Compatibilidade FSE/HighDPI desligada.";
    }

    void AlternarWorkingSet(PerfilJogo j)
    {
        var novo = j with { TrimWorkingSet = !j.TrimWorkingSet };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.TrimWorkingSet
            ? "Trim de working set ligado para aplicar — pode causar micro-travadas logo após aplicar (memória volta a crescer)."
            : "Trim de working set desligado.";
    }

    void AlternarTimer(PerfilJogo j)
    {
        var novo = j with { TimerAltaResolucao = !j.TimerAltaResolucao };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.TimerAltaResolucao
            ? "Timer de 1 ms ligado — ATENÇÃO: aumenta o consumo de energia enquanto o jogo estiver aplicado; revertido ao fechar."
            : "Timer de alta resolução desligado.";
    }

    void AlternarGpu(PerfilJogo j)
    {
        var novo = j with { PrefereGpuAlta = !j.PrefereGpuAlta };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.PrefereGpuAlta
            ? "GPU de alto desempenho marcada para este .exe (UserGpuPreferences) — aplicada com o perfil e restaurada ao reverter."
            : "Preferência de GPU removida (volta à automática).";
    }

    void AlternarBoost(PerfilJogo j)
    {
        var novo = j with { PriorityBoost = !j.PriorityBoost };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.PriorityBoost
            ? "Boost de prioridade ligado — temporário (revertido ao fechar)."
            : "Boost de prioridade desligado.";
    }

    void AlternarCpuSets(PerfilJogo j)
    {
        var novo = j with { CpuSetsPCores = !j.CpuSetsPCores };
        _catalogo.Adicionar(novo);
        Montar();
        StatusLbl.Text = novo.CpuSetsPCores
            ? "Núcleos P (CPU Sets) ligado — os threads do jogo passam a preferir os P-cores enquanto aplicado; revertido ao fechar."
            : "Núcleos P (CPU Sets) desligado — volta ao agendamento padrão do Windows.";
    }

    void Aplicar(PerfilJogo j)
    {
        if (_controle.ExesRodando(new[] { j.Executavel }).Count == 0)
        {
            StatusLbl.Text = $"'{j.Nome}' não está aberto — abra o jogo (ou clique em 'Iniciar jogo') e clique em Aplicar.";
            return;
        }
        if (_gestor.AplicarAgora(j))
        {
            StatusLbl.Text = $"'{j.Nome}' aplicado ({Resumo(j)}) — reversível.";
            Toasts.Mostrar(this, TipoToast.Sucesso, "Perfil aplicado", j.Nome);
        }
        else
        {
            StatusLbl.Text = $"Não foi possível aplicar em '{j.Nome}' (pode exigir administrador ou ser bloqueado por anti-cheat).";
            Toasts.Mostrar(this, TipoToast.Erro, "Falha ao aplicar perfil", j.Nome);
        }
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
