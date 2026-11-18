using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "GPU" — paridade com a tela de GPU do original (Perfil Gráfico + Ajustes de Driver +
/// Auto NVIDIA Debloat), dentro dos limites da reconstrução segura. A detecção e os modos de vídeo são
/// somente leitura; os "Ajustes do Driver" e os perfis aplicam SÓ tweaks de registro reversíveis do catálogo
/// (HAGS, TdrDelay, power gating AMD, Game DVR, visual, mouse raw) — o original não usa NVAPI/NVML, então
/// não há controle real de clock/memória/debloat do driver a reproduzir; essas partes ficam informativas.</summary>
public partial class GpuView : UserControl
{
    readonly MotorTweaks _motor = new(new RegistroWindows());
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style SecButton => (Style)Application.Current.Resources["SecondaryButton"];
    static Style PriButton => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Switch => (Style)Application.Current.Resources["ToggleSwitch"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static Style EyebrowStyle => (Style)Application.Current.Resources["Eyebrow"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Brush BorderStrong => (Brush)Application.Current.Resources["BorderStrongBrush"];
    static Brush Brand500 => (Brush)Application.Current.Resources["Brand500Brush"];

    /// <summary>Perfis gráficos (adaptação segura): o original exibia esses nomes, mas sem NVAPI o efeito exato
    /// de cada perfil não é recuperável. Cada perfil aplica um conjunto DOCUMENTADO de tweaks reversíveis do
    /// catálogo (nada inventado de driver). "Básico" = remover qualquer perfil.</summary>
    static readonly (string Nome, string Desc, string[] TweakIds)[] Perfis =
    {
        ("Básico", "Remove qualquer perfil aplicado (restaura os padrões).", Array.Empty<string>()),
        ("Casual", "Visual mais leve: transparência, Aero Peek e animações desligadas.", new[] { "transparencia-off", "taskbar-transparencia-off", "aero-peek-off", "min-animate-off" }),
        ("Competitivo", "Menos overhead: Game DVR e captura em segundo plano desligados.", new[] { "gamedvr-off", "gamedvr-captura-off" }),
        ("FPS", "HAGS ligado + TDR Delay 8 s (recuperação de GPU travada).", new[] { "hags", "tdr-delay" }),
        ("FPS 2.0", "FPS + Game DVR desligado.", new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off" }),
        ("Latência", "HAGS + TDR Delay + mouse sem aceleração (raw input).", new[] { "hags", "tdr-delay", "mouse-aceleracao-off", "mouse-limiar1-off", "mouse-limiar2-off" }),
        ("Avançado", "HAGS + TDR + Game DVR off + mouse raw + visual leve.", new[] { "hags", "tdr-delay", "gamedvr-off", "gamedvr-captura-off", "mouse-aceleracao-off", "transparencia-off", "aero-peek-off" }),
    };

    public GpuView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var gpus = await Task.Run(DiagnosticoGpu.Coletar);
            Lista.Children.Clear();
            if (gpus.Count == 0)
            {
                Lista.Children.Add(new TextBlock { Text = "Nenhum adaptador de vídeo detectado.", Foreground = Muted, Margin = new Thickness(2, 8, 0, 0) });
                return;
            }
            foreach (var g in gpus) Lista.Children.Add(CartaoGpu(g));

            Lista.Children.Add(Secao("AJUSTES DO DRIVER — ENERGIA E FUNCIONAMENTO"));
            Lista.Children.Add(CartaoAjustesDriver(gpus));

            Lista.Children.Add(Secao("PERFIL GRÁFICO — ESCOLHA UM"));
            Lista.Children.Add(CartaoPerfilGrafico());

            Lista.Children.Add(Secao("AUTO NVIDIA DEBLOAT"));
            Lista.Children.Add(CartaoDebloat(gpus));

            Lista.Children.Add(CartaoModosVideo());
            Lista.Children.Add(CartaoRecomendacoes());
        };
    }

    static TextBlock Secao(string t) => new() { Text = t, Style = SectionStyle, Margin = new Thickness(0, 4, 0, 10) };
    static Tweak? Tw(string id) => CatalogoTweaks.Todos.FirstOrDefault(t => t.Id == id);

    // ─────────────────────────── Ajustes do Driver ───────────────────────────

    Border CartaoAjustesDriver(IReadOnlyList<InfoGpu> gpus)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Liga/desliga ajustes de driver do catálogo do original, todos reversíveis. Os de HKLM exigem administrador; cada aplicação tem confirmação.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        sp.Children.Add(LinhaToggle(Tw("hags")!));
        sp.Children.Add(LinhaToggle(Tw("tdr-delay")!));

        bool amd = gpus.Any(g => g.Fabricante == FabricanteGpu.Amd);
        if (amd)
        {
            sp.Children.Add(new TextBlock
            {
                Text = "Economia de energia (AMD) — desliga o power gating dinâmico da GPU. Só aparece com GPU AMD; pode aumentar calor/consumo (meça A/B).",
                Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            });
            sp.Children.Add(LinhaGrupoAmd());
        }
        else
        {
            sp.Children.Add(new TextBlock
            {
                Text = "Economia de energia (power gating) — disponível apenas com GPU AMD (nenhum adaptador AMD detectado).",
                Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            });
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    /// <summary>Linha de toggle para um único tweak (HAGS, TdrDelay).</summary>
    Border LinhaToggle(Tweak t)
    {
        var g = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(new TextBlock { Text = t.Nome, FontSize = 14 });
        left.Children.Add(new TextBlock { Text = t.Descricao + (t.ExigeAdmin ? "  ·  requer administrador" : ""), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        var sw = new ToggleButton { Style = Switch, IsChecked = _motor.Aplicado(t) };
        sw.Click += (_, _) => AplicarToggle(sw, t, onApply: null);
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return new Border { Child = g };
    }

    /// <summary>Toggle agrupado do power gating AMD (10 valores do original).</summary>
    Border LinhaGrupoAmd()
    {
        var ids = new[] { "amd-pstate-off", "amd-gmm-powergate-off", "amd-samu-powergate-off", "amd-vcn-powergate-off", "amd-drmdma-powergate-off", "amd-ulps-off", "amd-asic-lowpower-off", "amd-gpu-powerdown-off", "amd-sclk-deepsleep-off", "amd-power-containment-off" };
        var tweaks = ids.Select(Tw).Where(t => t is not null).Cast<Tweak>().ToList();

        var g = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(new TextBlock { Text = "Power gating AMD off (10 valores)", FontSize = 14 });
        left.Children.Add(new TextBlock { Text = "Desliga DisableDynamicPstate, Disable*PowerGating, EnableUlps=0, PP_* — valores literais do original. Requer administrador.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        var sw = new ToggleButton { Style = Switch, IsChecked = tweaks.All(_motor.Aplicado) };
        sw.Click += (_, _) =>
        {
            if (sw.IsChecked == true)
            {
                if (!ExigirAdmin()) { sw.IsChecked = false; return; }
                if (MessageBox.Show("Desligar o power gating da GPU AMD? Pode aumentar calor/consumo. Reversível (desfazer restaura).", "Confirmação — AMD", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                { sw.IsChecked = false; return; }
                foreach (var t in tweaks) _motor.Aplicar(t);
                Toasts.Mostrar(this, TipoToast.Sucesso, "Power gating AMD desligado", "10 valores do original aplicados (reversível).", () => { foreach (var t in tweaks) _motor.Desfazer(t); sw.IsChecked = false; });
            }
            else foreach (var t in tweaks) _motor.Desfazer(t);
        };
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return new Border { Child = g };
    }

    bool ExigirAdmin()
    {
        if (Elevacao.EhAdmin()) return true;
        if (MessageBox.Show("Este ajuste mexe no sistema (HKLM) e exige administrador.\n\nReabrir o SevenzinTwks como administrador?", "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
            Elevacao.ReabrirComoAdmin();
        return false;
    }

    void AplicarToggle(ToggleButton sw, Tweak t, Action? onApply)
    {
        try
        {
            if (sw.IsChecked == true)
            {
                if (t.ExigeAdmin && !ExigirAdmin()) { sw.IsChecked = false; return; }
                _motor.Aplicar(t);
                bool ok = _motor.Aplicado(t);
                onApply?.Invoke();
                Toasts.Mostrar(this, ok ? TipoToast.Sucesso : TipoToast.Aviso, ok ? "Ajuste aplicado" : "Ajuste não confirmado", t.Nome,
                    ok ? (Action?)(() => { if (_motor.Desfazer(t)) sw.IsChecked = false; }) : null);
            }
            else if (!_motor.Desfazer(t))
            {
                sw.IsChecked = true; // mantém "ligado": não havia backup (valor aplicado fora do SevenzinTwks)
                Toasts.Mostrar(this, TipoToast.Aviso, "Sem backup para desfazer", $"'{t.Nome}' já estava aplicado antes do SevenzinTwks — não há valor anterior salvo para restaurar.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = _motor.Aplicado(t);
            var msg = t.Caminho.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase)
                ? "O Windows bloqueia a gravação nesta chave do Defender (Proteção contra adulteração)."
                : "Sem permissão para gravar nesta chave. Reabra como administrador (botão direito → Executar como administrador).";
            Toasts.Mostrar(this, TipoToast.Erro, "Chave protegida", msg);
            MessageBox.Show(msg, "Permissão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ─────────────────────────── Perfil Gráfico ───────────────────────────

    string[]? _perfilSelecionado;

    Border CartaoPerfilGrafico()
    {
        var sp = new StackPanel();
        var status = new TextBlock { Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        var grid = new UniformGrid { Columns = 3 };
        var cards = new List<(Border Card, string[] Ids)>();

        foreach (var p in Perfis)
        {
            var ids = p.TweakIds;
            var card = new Border
            {
                Background = Surface2Brush(),
                BorderThickness = new Thickness(1),
                BorderBrush = BorderStrong,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var cp = new StackPanel();
            cp.Children.Add(new TextBlock { Text = p.Nome, FontSize = 13, FontWeight = FontWeights.SemiBold });
            cp.Children.Add(new TextBlock { Text = p.Desc, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            card.Child = cp;
            card.MouseLeftButtonUp += (_, _) => { _perfilSelecionado = ids; AtualizarSelecao(); };
            grid.Children.Add(card);
            cards.Add((card, ids));
        }
        sp.Children.Add(grid);

        void AtualizarSelecao()
        {
            var sel = _perfilSelecionado ?? Array.Empty<string>();
            foreach (var (c, ids) in cards)
            {
                bool ativo = ids.SequenceEqual(sel);
                c.BorderBrush = ativo ? Brand500 : BorderStrong;
                c.BorderThickness = new Thickness(ativo ? 2 : 1);
            }
            var nome = sel.Length == 0 ? "Básico" : Perfis.FirstOrDefault(p => p.TweakIds.SequenceEqual(sel)).Nome;
            bool aplicado = sel.Length > 0 && sel.All(id => Tw(id) is { } t && _motor.Aplicado(t));
            status.Text = sel.Length == 0
                ? "Básico selecionado — use 'Remover perfil' para restaurar os padrões."
                : $"{nome}: {(aplicado ? "aplicado" : "clique em 'Aplicar perfil selecionado'.")}";
        }
        AtualizarSelecao();

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var aplicar = new Button { Style = PriButton, Content = "Aplicar perfil selecionado", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        aplicar.Click += (_, _) => AplicarPerfil(AtualizarSelecao);
        var remover = new Button { Style = SecButton, Content = "Remover perfil", Padding = new Thickness(14, 8, 14, 8) };
        remover.Click += (_, _) =>
        {
            int n = 0;
            foreach (var (_, ids) in cards)
                foreach (var id in ids)
                    if (Tw(id) is { } t && _motor.TemBackup(id)) { _motor.Desfazer(t); n++; }
            AtualizarSelecao();
            Toasts.Mostrar(this, TipoToast.Sucesso, "Perfil removido", $"{n} valor(es) restaurado(s).");
        };
        botoes.Children.Add(aplicar);
        botoes.Children.Add(remover);
        sp.Children.Add(botoes);
        sp.Children.Add(status);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    static Brush Surface2Brush() => (Brush)Application.Current.Resources["Surface2Brush"];

    void AplicarPerfil(Action refresh)
    {
        var ids = _perfilSelecionado ?? Array.Empty<string>();
        if (ids.Length == 0) { Toasts.Mostrar(this, TipoToast.Aviso, "Nenhum ajuste", "Básico não aplica nada — use 'Remover perfil'."); return; }
        var tweaks = ids.Select(Tw).Where(t => t is not null).Cast<Tweak>().ToList();
        if (tweaks.Any(t => t.ExigeAdmin) && !ExigirAdmin()) return;
        if (MessageBox.Show($"Aplicar o perfil selecionado?\n\n{tweaks.Count} ajuste(s) reversível(eis) do catálogo serão aplicados (HAGS, TDR, Game DVR, visual, mouse raw). Desfazer restaura o anterior.",
                "Confirmação — Perfil Gráfico", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        foreach (var t in tweaks) _motor.Aplicar(t);
        refresh();
        Toasts.Mostrar(this, TipoToast.Sucesso, "Perfil aplicado", $"{tweaks.Count} ajuste(s) aplicado(s) — reversível.");
    }

    // ─────────────────────────── Debloat NVIDIA ───────────────────────────

    static readonly string[] NvidiaBloatNomes =
    {
        "NVIDIA GeForce Experience", "NVIDIA Broadcast", "NVIDIA FrameView SDK",
        "NVIDIA GeForce NOW", "NVIDIA Canvas", "NVIDIA Omniverse",
    };

    static List<string> NvidiaBloatInstalados()
    {
        var achados = new List<string>();
        foreach (var raiz in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(raiz);
                if (k is null) continue;
                foreach (var sub in k.GetSubKeyNames())
                {
                    using var sk = k.OpenSubKey(sub);
                    if (sk?.GetValue("DisplayName") is string nome && NvidiaBloatNomes.Any(n => nome.Contains(n, StringComparison.OrdinalIgnoreCase)))
                        achados.Add(nome);
                }
            }
            catch { }
        }
        return achados.Distinct().ToList();
    }

    Border CartaoDebloat(IReadOnlyList<InfoGpu> gpus)
    {
        var sp = new StackPanel();
        bool nvidia = gpus.Any(g => g.Fabricante == FabricanteGpu.Nvidia);
        if (!nvidia)
        {
            sp.Children.Add(new TextBlock { Text = "Disponível apenas com GPU NVIDIA (nenhum adaptador NVIDIA detectado).", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
        }

        sp.Children.Add(new TextBlock
        {
            Text = "O original tinha um 'Auto NVIDIA Debloat' (remover apps de telemetria/overlay da NVIDIA). O SevenzinTwks não desinstala nada automaticamente: lista o que está instalado e abre 'Aplicativos instalados' para você decidir.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });

        var instalados = NvidiaBloatInstalados();
        if (instalados.Count == 0)
            sp.Children.Add(new TextBlock { Text = "Nenhum app de bloat NVIDIA detectado (GeForce Experience, Broadcast, etc.).", Foreground = Success, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        else
            foreach (var n in instalados)
                sp.Children.Add(new TextBlock { Text = "•  " + n, Foreground = Secondary, FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });

        var abrir = new Button { Style = SecButton, Content = "Abrir Aplicativos instalados", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        abrir.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        sp.Children.Add(abrir);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    // ─────────────────────────── detecção / modos (mantido) ───────────────────────────

    static string NomeFab(FabricanteGpu f) => f switch
    {
        FabricanteGpu.Nvidia => "NVIDIA",
        FabricanteGpu.Amd => "AMD",
        FabricanteGpu.Intel => "Intel",
        _ => "não identificado",
    };

    Border CartaoGpu(InfoGpu g)
    {
        var sp = new StackPanel();
        var topo = new Grid();
        var esq = new StackPanel();
        esq.Children.Add(new TextBlock { Text = "PLACA DE VÍDEO", Style = EyebrowStyle });
        esq.Children.Add(new TextBlock { Text = g.Adaptador, FontSize = 16, FontWeight = FontWeights.SemiBold });
        topo.Children.Add(esq);
        var selo = new Border { Background = SuccessBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        selo.Child = new TextBlock { Text = $"Detectada · {NomeFab(g.Fabricante)}", Foreground = Success, FontSize = 11, FontWeight = FontWeights.SemiBold };
        topo.Children.Add(selo);
        sp.Children.Add(topo);

        var campos = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        campos.Children.Add(Campo("Fabricante", NomeFab(g.Fabricante)));
        campos.Children.Add(Campo("Driver", g.VersaoDriver is null ? "—" : $"{g.VersaoDriver}{(g.DataDriver is { } d ? $"  ·  {d:yyyy-MM-dd}" : "")}"));
        campos.Children.Add(Campo("VRAM reportada", g.Vram ?? "— (WMI satura acima de 4 GB)"));
        campos.Children.Add(Campo("Modo atual", g.ModoAtual ?? "—"));
        sp.Children.Add(campos);

        if (g.DriverPossivelmenteAntigo)
            sp.Children.Add(new TextBlock { Text = "⚠ Driver possivelmente desatualizado — considere atualizar pela página oficial.", Foreground = Warning, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });

        if (g.UrlDriverOficial is { } url)
        {
            var b = new Button { Style = SecButton, Content = $"Abrir página oficial do driver ({NomeFab(g.Fabricante)})", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (_, _) => AbrirUrl(url);
            sp.Children.Add(b);
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    static UIElement Campo(string rotulo, string valor)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var r = new TextBlock { Text = rotulo, Foreground = Muted, FontSize = 13 };
        var v = new TextBlock { Text = valor, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        g.Children.Add(r); g.Children.Add(v);
        return g;
    }

    Border CartaoModosVideo()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Modos de vídeo suportados (somente leitura)", FontSize = 16, FontWeight = FontWeights.SemiBold });

        var dispositivos = LeitorModosVideo.Dispositivos();
        var alvos = dispositivos.Count > 0 ? dispositivos : new List<DispositivoVideo> { new("", "Exibição primária") };
        foreach (var d in alvos)
        {
            var nomeAlvo = d.Nome.Length > 0 ? d.Nome : null;
            var modos = LeitorModosVideo.Listar(nomeAlvo);
            var atual = LeitorModosVideo.Atual(nomeAlvo);

            var titulo = $"{d.Descricao}{(d.Nome.Length > 0 ? $" ({d.Nome})" : "")}";
            sp.Children.Add(new TextBlock
            {
                Text = atual is { } a ? $"{titulo} — em uso: {a.Largura}×{a.Altura} @ {a.Hz} Hz" : titulo,
                FontSize = 13, Foreground = Secondary, Margin = new Thickness(0, 10, 0, 4),
            });

            if (modos.Count == 0)
            {
                sp.Children.Add(new TextBlock { Text = "Indisponível neste equipamento.", Foreground = Muted, FontSize = 12 });
                continue;
            }
            foreach (var grupo in modos.GroupBy(m => (m.Largura, m.Altura)).Take(12))
            {
                var hz = string.Join(" · ", grupo.Select(m => m.Hz).Distinct().OrderByDescending(h => h).Select(h => $"{h} Hz"));
                var bpp = grupo.First().BitsPerPel != 32 ? $" · {grupo.First().BitsPerPel} bpp" : "";
                sp.Children.Add(new TextBlock { Text = $"•  {grupo.Key.Largura}×{grupo.Key.Altura} — {hz}{bpp}", FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
            }
            var grupos = modos.GroupBy(m => (m.Largura, m.Altura)).Count();
            if (grupos > 12)
                sp.Children.Add(new TextBlock { Text = $"… e mais {grupos - 12} resoluções.", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        }

        sp.Children.Add(new TextBlock
        {
            Text = "O SevenzinTwks lista os modos, mas não altera a taxa de atualização por você — troque nas configurações de vídeo do Windows ou do driver.",
            Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        return new Border { Style = CardStyle, Margin = new Thickness(0, 6, 0, 0), Child = sp };
    }

    Border CartaoRecomendacoes()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "RECOMENDAÇÕES SEGURAS (sugestões — nada é aplicado aqui)", Style = SectionStyle });
        foreach (var t in new[]
        {
            "Mantenha o driver atualizado pela página oficial do fabricante (link acima).",
            "Ligue o HAGS e meça antes/depois (A/B) — depende de GPU e driver.",
            "Game DVR desligado reduz o overhead de gravação em jogos.",
            "Os 'perfis gráficos' aplicam só tweaks de registro reversíveis — o original não tinha NVAPI/NVML, então não há controle real de clock/memória do driver a reproduzir.",
        })
            sp.Children.Add(new TextBlock { Text = "•  " + t, Foreground = Secondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        return new Border { Style = CardStyle, Margin = new Thickness(0, 6, 0, 0), Child = sp };
    }

    static void AbrirUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro ao abrir", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
