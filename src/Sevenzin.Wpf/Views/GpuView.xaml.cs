using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>
/// Tela "GPU" — layout de 2 colunas (Ajustes do Driver + 7 Perfis Gráficos exclusivos com status de Ray Tracing),
/// modal sobreposto "Auto NVIDIA Debloat" (endpoints HTTPS oficiais NVIDIA, Authenticode, 7-Zip, setup.cfg),
/// suporte dedicado a GPUs AMD (5 controles reversíveis) e Overclock NVIDIA dentro do envelope da VBIOS (`nvidia-smi`).
/// </summary>
public partial class GpuView : UserControl
{
    readonly MotorTweaks _motor = new(new RegistroWindows());
    IReadOnlyList<InfoGpu> _gpus = Array.Empty<InfoGpu>();
    IReadOnlyList<string> _chavesNvidia = Array.Empty<string>();
    IReadOnlyList<string> _chavesAmd = Array.Empty<string>();
    CancellationTokenSource? _ctsDebloat;

    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];
    static Style SecButton => (Style)Application.Current.Resources["SecondaryButton"];
    static Style PriButton => (Style)Application.Current.Resources["PrimaryButton"];
    static Style Switch => (Style)Application.Current.Resources["ToggleSwitch"];
    static Style DarkBoxStyle => (Style)Application.Current.Resources["DarkBox"];
    static Style DarkComboStyle => (Style)Application.Current.Resources["DarkCombo"];
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush PrimaryText => (Brush)Application.Current.Resources["TextPrimaryBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static Brush Danger => (Brush)Application.Current.Resources["DangerBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Brush BorderDefault => (Brush)Application.Current.Resources["BorderDefaultBrush"];
    static Brush BorderStrong => (Brush)Application.Current.Resources["BorderStrongBrush"];
    static Brush Brand400 => (Brush)Application.Current.Resources["Brand400Brush"];
    static Brush Brand500 => (Brush)Application.Current.Resources["Brand500Brush"];
    static Brush BrandTint => (Brush)Application.Current.Resources["BrandTintBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Brush Surface3 => (Brush)Application.Current.Resources["Surface3Brush"];

    public GpuView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RecarregarTelaAsync();
    }

    async Task RecarregarTelaAsync()
    {
        var dados = await Task.Run(() =>
        {
            var gpus = DiagnosticoGpu.Coletar();
            var nvKeys = GpuDriverTweaks.ResolverChavesClasseDriver(FabricanteGpu.Nvidia);
            var amdKeys = GpuDriverTweaks.ResolverChavesClasseDriver(FabricanteGpu.Amd);
            var verDriver = GpuDriverTweaks.LerVersaoDriverClasse(FabricanteGpu.Nvidia)
                         ?? gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Nvidia)?.VersaoDriver
                         ?? gpus.FirstOrDefault()?.VersaoDriver;
            GpuDriverTweaks.SincronizarComVersaoDriver(verDriver);
            bool smi = gpus.Any(g => g.Fabricante == FabricanteGpu.Nvidia) && GpuOverclock.NvidiaDisponivel();
            var tele = smi ? GpuOverclock.Ler() : null;
            var dispositivos = LeitorModosVideo.Dispositivos();
            var modos = (dispositivos.Count > 0 ? dispositivos : new List<DispositivoVideo> { new("", "Exibição primária") })
                .Select(d => { var n = d.Nome.Length > 0 ? d.Nome : null; return (d, LeitorModosVideo.Listar(n), LeitorModosVideo.Atual(n)); }).ToList();
            var placas = PlacasVideoPci.Coletar();
            return (gpus, nvKeys, amdKeys, verDriver, smi, tele, modos, placas);
        });

        _gpus = dados.gpus;
        _chavesNvidia = dados.nvKeys;
        _chavesAmd = dados.amdKeys;
        // Placa no PCI sem o driver do fabricante: depois de um DDU ela some de "adaptadores de vídeo", mas continua aqui.
        var semDriver = dados.placas.Where(p => p.Estado != EstadoDriverPlaca.Instalado).ToList();

        AtualizarCabecalhoTopo(_gpus, semDriver);
        Lista.Children.Clear();
        foreach (var p in semDriver) Lista.Children.Add(CartaoPlacaSemDriver(p));

        if (_gpus.Count == 0)
        {
            if (semDriver.Count == 0)
                Lista.Children.Add(new TextBlock
                {
                    Text = "Nenhuma placa de vídeo encontrada, nem no barramento PCI. Confira se a placa está bem encaixada e com o cabo de energia ligado.",
                    Foreground = Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 8, 0, 0),
                });
            return;
        }

        // Grid principal de 2 colunas: Coluna Esquerda (Ajustes do Driver) + Coluna Direita (Perfil Gráfico)
        var gridPrincipal = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        gridPrincipal.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
        gridPrincipal.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        gridPrincipal.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        var colEsquerda = new StackPanel();
        colEsquerda.Children.Add(Secao("Ajustes do driver"));
        colEsquerda.Children.Add(CartaoAjustesDriver(_gpus));
        Grid.SetColumn(colEsquerda, 0);
        gridPrincipal.Children.Add(colEsquerda);

        var colDireita = new StackPanel();
        colDireita.Children.Add(Secao("Perfil gráfico"));
        colDireita.Children.Add(CartaoPerfilGrafico(dados.verDriver));
        Grid.SetColumn(colDireita, 2);
        gridPrincipal.Children.Add(colDireita);

        Lista.Children.Add(gridPrincipal);

        // Overclock NVIDIA (nvidia-smi oficial)
        Lista.Children.Add(Secao("Overclock (NVIDIA)"));
        Lista.Children.Add(CartaoOverclock(_gpus, dados.smi, dados.tele));

        // Detalhes de cada adaptador + Modos de vídeo
        Lista.Children.Add(Secao("Adaptadores e modos de vídeo"));
        foreach (var g in _gpus) Lista.Children.Add(CartaoGpu(g));
        Lista.Children.Add(CartaoModosVideo(dados.modos));
    }

    void AtualizarCabecalhoTopo(IReadOnlyList<InfoGpu> gpus, IReadOnlyList<PlacaVideoPci> semDriver)
    {
        if (semDriver.FirstOrDefault() is { } s)
        {
            TxtModeloTopo.Text = s.Nome;
            PontoStatusTopo.Fill = Warning;
            TxtSubstatusTopo.Text = s.Estado == EstadoDriverPlaca.Ausente ? "Encontrada sem driver" : "Usando o driver básico do Windows";
            return;
        }

        var nv = gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Nvidia);
        var amd = gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Amd);
        var principal = nv ?? amd ?? gpus.FirstOrDefault();

        if (principal is null)
        {
            TxtModeloTopo.Text = "Nenhuma GPU detectada";
            PontoStatusTopo.Fill = Warning;
            TxtSubstatusTopo.Text = "Sem adaptador de vídeo ativo";
            return;
        }

        TxtModeloTopo.Text = principal.Adaptador;
        PontoStatusTopo.Fill = Success;
        TxtSubstatusTopo.Text = nv is not null
            ? "Detectada – ajustes NVIDIA liberados"
            : amd is not null
                ? "Detectada – ajustes AMD liberados"
                : "Detectada – diagnóstico somente leitura";
    }

    static TextBlock Secao(string t) => new() { Text = t, Style = SectionStyle, Margin = new Thickness(0, 2, 0, 10) };

    /// <summary>Placa vista no PCI sem o driver do fabricante: diz o que está acontecendo e oferece o caminho para instalar.</summary>
    Border CartaoPlacaSemDriver(PlacaVideoPci p)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = p.Nome, FontSize = 16, FontWeight = FontWeights.SemiBold });
        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        status.Children.Add(new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Warning, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        status.Children.Add(new TextBlock
        {
            Text = p.Estado == EstadoDriverPlaca.Ausente ? "Sem driver instalado" : "Usando o driver básico da Microsoft",
            Foreground = Warning,
            FontSize = 12.5,
        });
        sp.Children.Add(status);
        sp.Children.Add(new TextBlock
        {
            Text = p.Estado == EstadoDriverPlaca.Ausente
                ? "O Windows encontrou a placa, mas nenhum driver está instalado nela. Até instalar, os jogos rodam sem aceleração e os ajustes desta tela ficam indisponíveis. Costuma acontecer depois de remover o driver com o DDU ou uma limpeza."
                : "A imagem funciona, mas o driver básico do Windows não tem aceleração para jogos. Instale o driver do fabricante.",
            Foreground = Secondary,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 14),
        });

        var acoes = new StackPanel { Orientation = Orientation.Horizontal };
        if (p.Fabricante == FabricanteGpu.Nvidia)
        {
            var instalar = new Button { Style = PriButton, Content = "Instalar driver com o Auto NVIDIA Debloat", Padding = new Thickness(18, 9, 18, 9) };
            instalar.Click += (_, _) => AbrirModalDebloat(p.Nome, semDriver: true);
            acoes.Children.Add(instalar);
        }
        else if (DriverGpu.UrlOficialDriver(p.Fabricante) is { } url)
        {
            var abrir = new Button { Style = SecButton, Content = $"Abrir a página oficial de drivers {PlacasVideoPci.NomeFabricante(p.Fabricante)}", Padding = new Thickness(16, 8, 16, 8) };
            abrir.Click += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            acoes.Children.Add(abrir);
        }
        acoes.Children.Add(new TextBlock
        {
            Text = $"ID do dispositivo: {(p.Instancia.Length > 21 ? p.Instancia[4..21] : p.Instancia)}",
            Foreground = Muted,
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        });
        sp.Children.Add(acoes);
        return new Border { Style = CardStyle, Child = sp, Margin = new Thickness(0, 0, 0, 16) };
    }
    static Tweak? Tw(string id) => CatalogoTweaks.Todos.FirstOrDefault(t => t.Id == id);

    // ─────────────────────────── Coluna Esquerda: Ajustes do Driver ───────────────────────────

    Border CartaoAjustesDriver(IReadOnlyList<InfoGpu> gpus)
    {
        var sp = new StackPanel();
        bool temNvidia = gpus.Any(g => g.Fabricante == FabricanteGpu.Nvidia);
        bool temAmd = gpus.Any(g => g.Fabricante == FabricanteGpu.Amd);

        if (temNvidia)
        {
            var gruposNv = GpuDriverTweaks.CriarGruposNvidia(_chavesNvidia);
            // Subtítulos curtos fiéis à especificação da tela GPU
            var subtitulosCurto = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["nv-economia-energia"] = "Menos estados ociosos no driver",
                ["nv-power-gating"] = "Mantém blocos da GPU acordados",
                ["nv-driver-perf"] = "Máscara de desempenho no driver",
            };

            for (int i = 0; i < gruposNv.Count; i++)
            {
                var grp = gruposNv[i];
                var sub = subtitulosCurto.GetValueOrDefault(grp.Id, grp.Descricao);
                sp.Children.Add(LinhaToggleGrupo(grp, sub, mostrarDivisor: true));
            }

            // Linha Auto NVIDIA Debloat -> botão Abrir
            sp.Children.Add(LinhaAutoNvidiaDebloat(habilitado: true));
        }
        else if (temAmd)
        {
            var avisoLock = new Border
            {
                Background = Surface2,
                BorderBrush = BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = "Somente as opções para AMD ficam disponíveis. Os controles da NVIDIA permanecem bloqueados.",
                    Foreground = Secondary,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            sp.Children.Add(avisoLock);

            var gruposAmd = GpuDriverTweaks.CriarGruposAmd(_chavesAmd);
            for (int i = 0; i < gruposAmd.Count; i++)
                sp.Children.Add(LinhaToggleGrupo(gruposAmd[i], gruposAmd[i].Descricao, mostrarDivisor: i < gruposAmd.Count - 1));
        }
        else
        {
            sp.Children.Add(new TextBlock
            {
                Text = "Nenhum adaptador NVIDIA ou AMD dedicado detectado para ajustes específicos de classe de driver.",
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
            });
        }

        // Ajustes globais Windows GraphicsDrivers (HAGS / TDR Delay)
        var sep = new Border { Height = 1, Background = BorderDefault, Margin = new Thickness(0, 10, 0, 8) };
        sp.Children.Add(sep);
        sp.Children.Add(new TextBlock { Text = "Sistema gráfico do Windows", Style = SectionStyle, Margin = new Thickness(0, 2, 0, 4) });
        if (Tw("hags") is { } hags) sp.Children.Add(LinhaToggleUnico(hags));
        if (Tw("tdr-delay") is { } tdr) sp.Children.Add(LinhaToggleUnico(tdr));

        return new Border { Style = CardStyle, Child = sp };
    }

    Border LinhaToggleGrupo(GrupoDriverGpu grupo, string subtitulo, bool mostrarDivisor)
    {
        var raiz = new StackPanel();
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var esq = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        esq.Children.Add(new TextBlock { Text = grupo.Nome, FontSize = 14, FontWeight = FontWeights.SemiBold });
        esq.Children.Add(new TextBlock
        {
            Text = subtitulo,
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(esq, 0);
        g.Children.Add(esq);

        var sw = new ToggleButton
        {
            Style = Switch,
            IsChecked = GpuDriverTweaks.GrupoAplicado(_motor, grupo),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = grupo.Descricao,
        };
        sw.Click += (_, _) => AlternarGrupoDriver(sw, grupo);
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);

        raiz.Children.Add(g);
        if (mostrarDivisor)
            raiz.Children.Add(new Border { Height = 1, Background = BorderDefault });
        return new Border { Child = raiz };
    }

    void AlternarGrupoDriver(ToggleButton sw, GrupoDriverGpu grupo)
    {
        try
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso($"GPU: {grupo.Nome}");
            bool exigeAdmin = grupo.Tweaks.Any(t => t.ExigeAdmin);
            if (sw.IsChecked == true)
            {
                if (exigeAdmin && !ExigirAdmin()) { sw.IsChecked = false; return; }
                var msg = $"Aplicar '{grupo.Nome}' ({grupo.Tweaks.Count} parâmetro(s) no registro do driver)?\n\n"
                        + $"{grupo.Descricao}\n\n"
                        + "Efeito: reduz estados de economia profunda/oscilação de clock; pode elevar consumo em repouso. Requer reinício para valer.\n"
                        + "Reversão: desligue esta chave para restaurar os valores originais salvos no backup.";
                if (MessageBox.Show(msg, $"Confirmação — {grupo.Nome}", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    sw.IsChecked = false;
                    return;
                }
                int n = GpuDriverTweaks.AplicarGrupo(_motor, grupo);
                NotificacoesStore.Padrao.Adicionar("GPU", $"{grupo.Nome} ativado", $"{n} valor(es) aplicados (reversível).");
                Toasts.Mostrar(this, TipoToast.Sucesso, $"{grupo.Nome} aplicado", $"{n} valor(es) gravados com backup. Reinicie depois.",
                    () => { GpuDriverTweaks.DesativarGrupo(_motor, grupo); sw.IsChecked = false; });
            }
            else
            {
                if (exigeAdmin && !ExigirAdmin()) { sw.IsChecked = true; return; }
                int desfeitos = GpuDriverTweaks.DesativarGrupo(_motor, grupo);
                if (desfeitos == 0)
                {
                    sw.IsChecked = GpuDriverTweaks.GrupoAplicado(_motor, grupo);
                    Toasts.Mostrar(this, TipoToast.Aviso, "Alteração bloqueada", $"O Windows/driver manteve '{grupo.Nome}' ativo.");
                }
                else
                {
                    NotificacoesStore.Padrao.Adicionar("GPU", $"{grupo.Nome} desativado", $"{desfeitos} valor(es) restaurados.");
                    Toasts.Mostrar(this, TipoToast.Sucesso, $"{grupo.Nome} desativado", $"{desfeitos} valor(es) restaurados ao estado anterior/padrão.");
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = GpuDriverTweaks.GrupoAplicado(_motor, grupo);
            Toasts.Mostrar(this, TipoToast.Erro, "Sem permissão", "O sistema protegeu esta chave de classe do driver.");
        }
        catch (Exception ex)
        {
            sw.IsChecked = GpuDriverTweaks.GrupoAplicado(_motor, grupo);
            Toasts.Mostrar(this, TipoToast.Erro, "Erro ao gravar ajuste", ex.Message);
        }
        finally
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso(null);
        }
    }

    Border LinhaAutoNvidiaDebloat(bool habilitado)
    {
        var g = new Grid { Margin = new Thickness(0, 10, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var esq = new StackPanel { Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        esq.Children.Add(new TextBlock { Text = "Auto NVIDIA Debloat", FontSize = 14, FontWeight = FontWeights.SemiBold });
        esq.Children.Add(new TextBlock
        {
            Text = "Remove bloat e instala driver limpo",
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(esq, 0);
        g.Children.Add(esq);

        var btnAbrir = new Button
        {
            Style = SecButton,
            Content = "Abrir",
            Padding = new Thickness(16, 7, 16, 7),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = habilitado,
        };
        btnAbrir.Click += (_, _) => AbrirModalDebloat();
        Grid.SetColumn(btnAbrir, 1);
        g.Children.Add(btnAbrir);

        return new Border { Child = g };
    }

    Border LinhaToggleUnico(Tweak t)
    {
        var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        left.Children.Add(new TextBlock { Text = t.Nome, FontSize = 13.5 });
        left.Children.Add(new TextBlock
        {
            Text = t.Descricao,
            Foreground = Muted,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        var sw = new ToggleButton { Style = Switch, IsChecked = _motor.Aplicado(t), VerticalAlignment = VerticalAlignment.Center };
        sw.Click += (_, _) => AplicarToggleUnico(sw, t);
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return new Border { Child = g };
    }

    void AplicarToggleUnico(ToggleButton sw, Tweak t)
    {
        try
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso($"GPU: {t.Nome}");
            if (sw.IsChecked == true)
            {
                if (t.ExigeAdmin && !ExigirAdmin()) { sw.IsChecked = false; return; }
                if (MessageBox.Show(_motor.Previa(t) + "\n\nAplicar agora?", "Confirmação — GPU", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                {
                    sw.IsChecked = false;
                    return;
                }
                _motor.Aplicar(t);
                Toasts.Mostrar(this, TipoToast.Sucesso, "Ajuste aplicado", t.Nome, () => { if (_motor.Desativar(t)) sw.IsChecked = false; });
            }
            else if (!_motor.Desativar(t))
            {
                sw.IsChecked = _motor.Aplicado(t);
                Toasts.Mostrar(this, TipoToast.Aviso, "Alteração bloqueada", $"Não foi possível desativar '{t.Nome}'.");
            }
            else
            {
                NotificacoesStore.Padrao.Adicionar("GPU", $"'{t.Nome}' desativado", "Valor anterior ou padrão do Windows restaurado.");
                Toasts.Mostrar(this, TipoToast.Sucesso, "Ajuste desativado", t.Nome);
            }
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = _motor.Aplicado(t);
            Toasts.Mostrar(this, TipoToast.Erro, "Permissão insuficiente", "Chave protegida pelo Windows.");
        }
        catch (Exception ex)
        {
            sw.IsChecked = _motor.Aplicado(t);
            Toasts.Mostrar(this, TipoToast.Erro, "Falha no ajuste", ex.Message);
        }
        finally
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso(null);
        }
    }

    // ─────────────────────────── Coluna Direita: 7 Perfis Gráficos Exclusivos ───────────────────────────

    Border CartaoPerfilGrafico(string? versaoDriverAtual)
    {
        var sp = new StackPanel();
        var estadoInicial = GpuDriverTweaks.LerEstadoPerfil();
        string? perfilAtivoId = estadoInicial.PerfilId;

        var listaCards = new List<(Border Card, System.Windows.Shapes.Ellipse RadioDot, PerfilGraficoGpu Perfil)>();
        var txtStatus = new TextBlock
        {
            Foreground = Secondary,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var btnRemover = new Button
        {
            Style = SecButton,
            Content = "Remover perfil",
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
        };
        var txtNvapiReal = new TextBlock
        {
            Foreground = Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var gpuNvidia = _gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Nvidia);

        int leituraDrs = 0;
        async Task LerDrsAsync(string? idAtivo)
        {
            int minha = ++leituraDrs;
            txtNvapiReal.Text = "Lendo o perfil do driver NVIDIA…";
            txtNvapiReal.Foreground = Muted;
            var (resumo, ver) = await Task.Run(() => (NvidiaDrs.ResumoEstadoRealBaseProfile(),
                idAtivo is null ? null : NvidiaDrs.VerificarBaseProfile(idAtivo, gpuNvidia)));
            if (minha != leituraDrs) return; // o usuário já trocou de perfil: vale a leitura mais nova
            txtNvapiReal.Text = ver is null ? $"Driver NVIDIA (Base Profile): {resumo}" : $"{ver.Resumo} · {resumo}";
            txtNvapiReal.Foreground = ver is null ? Muted : ver.TodosConferem ? Success : Warning;
        }

        void AtualizarVisualPerfis()
        {
            foreach (var (card, dot, p) in listaCards)
            {
                bool selecionado = string.Equals(p.Id, perfilAtivoId, StringComparison.OrdinalIgnoreCase);
                card.BorderBrush = selecionado ? Brand500 : BorderStrong;
                card.BorderThickness = new Thickness(selecionado ? 1.8 : 1);
                card.Background = selecionado ? BrandTint : Surface2;
                dot.Fill = selecionado ? Brand400 : Brushes.Transparent;
            }

            var ativo = GpuDriverTweaks.ObterPerfil(perfilAtivoId);
            if (ativo is not null)
            {
                txtStatus.Text = ativo.TextoStatusAplicado;
                btnRemover.IsEnabled = true;
            }
            else
            {
                txtStatus.Text = "Nenhum perfil gráfico ativo · selecione uma opção acima.";
                btnRemover.IsEnabled = GpuDriverTweaks.PerfisGraficos
                    .SelectMany(p => p.TweakIds)
                    .Distinct()
                    .Any(id => _motor.TemBackup(id));
            }

            if (gpuNvidia is not null) _ = LerDrsAsync(ativo?.Id);
            else
            {
                txtNvapiReal.Text = "GPU não NVIDIA: perfis aplicam apenas ajustes de sistema e manifesto .nip.";
                txtNvapiReal.Foreground = Muted;
            }
        }

        foreach (var perfil in GpuDriverTweaks.PerfisGraficos)
        {
            var pLocal = perfil;
            var card = new Border
            {
                Background = Surface2,
                BorderBrush = BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            var linha = new Grid();
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var anel = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            anel.Children.Add(new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Stroke = BorderStrong, StrokeThickness = 1.5 });
            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            anel.Children.Add(dot);
            Grid.SetColumn(anel, 0);
            linha.Children.Add(anel);

            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock { Text = pLocal.Nome, FontSize = 13.5, FontWeight = FontWeights.SemiBold });
            textos.Children.Add(new TextBlock { Text = pLocal.Subtitulo, Foreground = Muted, FontSize = 11.5, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(textos, 1);
            linha.Children.Add(textos);

            var badgeRt = new Border
            {
                Background = Surface3,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = pLocal.RayTracingDisponivel ? "RT ON" : "RT OFF",
                    Foreground = pLocal.RayTracingDisponivel ? Success : Warning,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                },
            };
            Grid.SetColumn(badgeRt, 2);
            linha.Children.Add(badgeRt);

            card.Child = linha;
            card.MouseLeftButtonUp += (_, _) =>
            {
                SelecionarEAplicarPerfil(pLocal, versaoDriverAtual, idNovo =>
                {
                    perfilAtivoId = idNovo;
                    AtualizarVisualPerfis();
                });
            };

            sp.Children.Add(card);
            listaCards.Add((card, dot, pLocal));
        }

        // Rodapé do card de perfis: Status dinâmico + botão Remover perfil
        var rodape = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        rodape.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rodape.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(txtStatus, 0);
        rodape.Children.Add(txtStatus);

        btnRemover.Click += (_, _) =>
        {
            int restaurados = RemoverTodosPerfis();
            var drs = _gpus.Any(g => g.Fabricante == FabricanteGpu.Nvidia)
                ? NvidiaDrs.RestaurarPadroesBaseProfile()
                : new ResultadoDrsNvidia(false, false, 0, 0, "");
            perfilAtivoId = null;
            GpuDriverTweaks.SalvarEstadoPerfil(new EstadoPerfilGpu(null, versaoDriverAtual, null));
            AtualizarVisualPerfis();
            var msg = drs.Sucesso
                ? $"Base Profile NVIDIA restaurado ao padrão ({drs.Aplicados} parâmetros) + {restaurados} ajuste(s) de sistema revertidos."
                : $"{restaurados} ajuste(s) revertidos.";
            NotificacoesStore.Padrao.Adicionar("GPU", "Perfil gráfico removido", msg);
            Toasts.Mostrar(this, TipoToast.Sucesso, "Perfil removido", msg);
        };
        Grid.SetColumn(btnRemover, 1);
        rodape.Children.Add(btnRemover);

        sp.Children.Add(rodape);
        sp.Children.Add(txtNvapiReal);
        AtualizarVisualPerfis();
        return new Border { Style = CardStyle, Child = sp };
    }

    void SelecionarEAplicarPerfil(PerfilGraficoGpu perfil, string? versaoDriverAtual, Action<string?> aoAtualizar)
    {
        var tweaks = perfil.TweakIds.Select(Tw).Where(t => t is not null).Cast<Tweak>().ToList();
        if (tweaks.Any(t => t.ExigeAdmin) && !ExigirAdmin()) return;

        var gpuNvidia = _gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Nvidia);
        bool temNvidia = gpuNvidia is not null;
        var drsParams = NvidiaDrs.ObterParametrosPerfil(perfil.Id, gpuNvidia);
        var resumoDrs = temNvidia
            ? $"• Aplica {drsParams.Count} parâmetros 3D adaptados à GPU ({gpuNvidia!.Adaptador}) direto no Base Profile (visível no NVIDIA Profile Inspector)\n"
            : "";

        var msg = $"Aplicar o perfil gráfico '{perfil.Nome}' ({perfil.Subtitulo})?\n\n"
                + $"• {perfil.StatusRayTracing}\n"
                + resumoDrs
                + $"• {tweaks.Count} ajuste(s) de sistema aplicados com backup automático\n"
                + $"• Manifesto .nip salvo em %LOCALAPPDATA%\\Sevenzin\\PerfisGpu\\{perfil.ArquivoNip}\n\n"
                + "O perfil anterior será revertido antes de aplicar o novo. Continuar?";
        if (MessageBox.Show(msg, $"Confirmação — Perfil {perfil.Nome}", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        try
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso($"Perfil GPU: {perfil.Nome}");
            RemoverTodosPerfis();
            foreach (var t in tweaks) _motor.Aplicar(t);
            var caminhoNip = GpuDriverTweaks.ExportarNipLocal(perfil, null, gpuNvidia);
            var resDrs = temNvidia
                ? NvidiaDrs.AplicarNoBaseProfile(perfil.Id, gpuNvidia)
                : new ResultadoDrsNvidia(false, false, 0, 0, "");
            var verDrs = temNvidia
                ? NvidiaDrs.VerificarBaseProfile(perfil.Id, gpuNvidia)
                : null;

            GpuDriverTweaks.SalvarEstadoPerfil(new EstadoPerfilGpu(perfil.Id, versaoDriverAtual, DateTime.UtcNow));
            aoAtualizar(perfil.Id);

            var detalheToast = resDrs.Sucesso
                ? $"{verDrs?.Resumo ?? $"Driver NVIDIA atualizado ({resDrs.Aplicados} params)"} · {perfil.StatusRayTracing}"
                : perfil.StatusRayTracing;
            NotificacoesStore.Padrao.Adicionar("GPU", $"Perfil {perfil.Nome} aplicado",
                $"{detalheToast} ({System.IO.Path.GetFileName(caminhoNip)})");
            Toasts.Mostrar(this, TipoToast.Sucesso, $"Aplicado: {perfil.Nome}", detalheToast);
        }
        catch (Exception ex)
        {
            Toasts.Mostrar(this, TipoToast.Erro, "Falha ao aplicar perfil", ex.Message);
        }
        finally
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso(null);
        }
    }

    int RemoverTodosPerfis()
    {
        int n = 0;
        var todosIds = GpuDriverTweaks.PerfisGraficos.SelectMany(p => p.TweakIds).Distinct();
        foreach (var id in todosIds)
            if (Tw(id) is { } t && _motor.TemBackup(id) && _motor.Desfazer(t))
                n++;
        return n;
    }

    // ─────────────────────────── Modal: Auto NVIDIA Debloat (NvClean) ───────────────────────────

    void AbrirModalDebloat(string? nomePlaca = null, bool semDriver = false)
    {
        ModalDebloatConteudo.Children.Clear();
        var gpuNv = _gpus.FirstOrDefault(g => g.Fabricante == FabricanteGpu.Nvidia);
        var nomeGpu = nomePlaca ?? gpuNv?.Adaptador ?? "NVIDIA GPU";
        bool ehNotebook = NvidiaDebloat.EhNotebookGpu(nomeGpu);

        System.Windows.Threading.DispatcherTimer? timerReinicio = null;

        // Cabeçalho do modal
        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "Auto NVIDIA Debloat", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        var btnFechar = new Button { Style = SecButton, Content = "✕", Padding = new Thickness(10, 4, 10, 4) };
        btnFechar.Click += (_, _) =>
        {
            if (_ctsDebloat is not null)
            {
                if (MessageBox.Show("Há uma operação em andamento. Deseja cancelá-la e fechar?", "Cancelar Debloat", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                _ctsDebloat.Cancel();
            }
            if (timerReinicio is not null)
            {
                timerReinicio.Stop();
                timerReinicio = null;
                PosDebloatNvidiaStore.CancelarReinicio();
            }
            Movimento.Sair(ModalDebloatOverlay, () => { ModalDebloatOverlay.Visibility = Visibility.Collapsed; ModalDebloatOverlay.IsHitTestVisible = true; });
        };
        Grid.SetColumn(btnFechar, 1);
        header.Children.Add(btnFechar);
        ModalDebloatConteudo.Children.Add(header);

        // Card de Identificação de Hardware (Modelo + DESKTOP/NOTEBOOK + GPU detectada)
        var cardHw = new Border
        {
            Background = Surface2,
            BorderBrush = BorderStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
        };
        var gridHw = new Grid();
        gridHw.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridHw.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var spHw = new StackPanel();
        var linhaModelo = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        linhaModelo.Children.Add(new TextBlock { Text = nomeGpu, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
        linhaModelo.Children.Add(new Border
        {
            Background = Surface3,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 2, 7, 2),
            Child = new TextBlock { Text = ehNotebook ? "Notebook" : "Desktop", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brand400 },
        });
        spHw.Children.Add(linhaModelo);
        var linhaDet = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        linhaDet.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = semDriver ? Warning : Success, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        linhaDet.Children.Add(new TextBlock
        {
            Text = semDriver ? "Placa encontrada sem driver: o Debloat baixa e instala o driver oficial" : "GPU detectada",
            Foreground = semDriver ? Warning : Success,
            FontSize = 11.5,
        });
        spHw.Children.Add(linhaDet);
        cardHw.Child = spHw;
        ModalDebloatConteudo.Children.Add(cardHw);

        // 1. FONTE DO DRIVER
        ModalDebloatConteudo.Children.Add(new TextBlock { Text = "Fonte do driver", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 6) });
        bool usarInstaladorLocal = false;
        string caminhoInstaladorLocal = "";

        var txtCaminhoLocal = new TextBlock { Text = "Escolher .exe baixado", Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
        var btnProcurar = new Button { Style = SecButton, Content = "procurar...", Padding = new Thickness(10, 4, 10, 4), FontSize = 11.5, Margin = new Thickness(8, 0, 0, 0) };

        var cardFonteAuto = CriarCardSelecao("Baixar o mais recente", "Da NVIDIA para esta GPU (HTTPS oficial + verificação Authenticode)");
        var cardFonteLocal = CriarCardSelecao("Já tenho o instalador", txtCaminhoLocal, btnProcurar);

        // 2. CANAL
        var painelCanal = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        painelCanal.Children.Add(new TextBlock { Text = "Canal", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 6) });
        var comboCanal = new ComboBox { Style = DarkComboStyle };
        comboCanal.Items.Add(new ComboBoxItem { Content = "Game Ready — Para jogos – novos lançamentos", Tag = CanalDriverNvidia.GameReady });
        comboCanal.Items.Add(new ComboBoxItem { Content = "Studio Driver — Para criação de conteúdo e estabilidade", Tag = CanalDriverNvidia.Studio });
        comboCanal.SelectedIndex = 0;
        painelCanal.Children.Add(comboCanal);

        void AtualizarSelecaoFonte()
        {
            EstilizarCardSelecao(cardFonteAuto, !usarInstaladorLocal);
            EstilizarCardSelecao(cardFonteLocal, usarInstaladorLocal);
            painelCanal.Visibility = usarInstaladorLocal ? Visibility.Collapsed : Visibility.Visible;
        }

        cardFonteAuto.MouseLeftButtonUp += (_, _) => { usarInstaladorLocal = false; AtualizarSelecaoFonte(); };
        cardFonteLocal.MouseLeftButtonUp += (_, _) => { usarInstaladorLocal = true; AtualizarSelecaoFonte(); };
        btnProcurar.Click += (_, e) =>
        {
            e.Handled = true;
            usarInstaladorLocal = true;
            var dlg = new OpenFileDialog
            {
                Title = "Selecionar instalador oficial do driver NVIDIA (.exe)",
                Filter = "Instalador NVIDIA (*.exe)|*.exe",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() == true)
            {
                caminhoInstaladorLocal = dlg.FileName;
                txtCaminhoLocal.Text = System.IO.Path.GetFileName(caminhoInstaladorLocal);
                txtCaminhoLocal.Foreground = PrimaryText;
            }
            AtualizarSelecaoFonte();
        };

        var gridFonte = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 8) };
        cardFonteAuto.Margin = new Thickness(0, 0, 5, 0);
        cardFonteLocal.Margin = new Thickness(5, 0, 0, 0);
        gridFonte.Children.Add(cardFonteAuto);
        gridFonte.Children.Add(cardFonteLocal);
        ModalDebloatConteudo.Children.Add(gridFonte);
        ModalDebloatConteudo.Children.Add(painelCanal);
        AtualizarSelecaoFonte();

        // 3. PERFIL DE LIMPEZA (Seguro vs Avançado selecionado por padrão)
        ModalDebloatConteudo.Children.Add(new TextBlock { Text = "Perfil", Style = SectionStyle, Margin = new Thickness(0, 4, 0, 6) });
        var perfilDebloat = PerfilDebloatNvidia.Avancado;
        var cardSeguro = CriarCardSelecao("Seguro", "Tira GeForce Experience, NVIDIA App, Update, Telemetria.");
        var cardAvancado = CriarCardSelecao("Avançado", "Remove tudo exceto driver e Painel de Controle.");
        cardSeguro.Margin = new Thickness(0, 0, 5, 0);
        cardAvancado.Margin = new Thickness(5, 0, 0, 0);

        void AtualizarSelecaoPerfilDebloat()
        {
            EstilizarCardSelecao(cardSeguro, perfilDebloat == PerfilDebloatNvidia.Seguro);
            EstilizarCardSelecao(cardAvancado, perfilDebloat == PerfilDebloatNvidia.Avancado);
        }
        cardSeguro.MouseLeftButtonUp += (_, _) => { perfilDebloat = PerfilDebloatNvidia.Seguro; AtualizarSelecaoPerfilDebloat(); };
        cardAvancado.MouseLeftButtonUp += (_, _) => { perfilDebloat = PerfilDebloatNvidia.Avancado; AtualizarSelecaoPerfilDebloat(); };
        AtualizarSelecaoPerfilDebloat();

        var gridPerfil = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 12) };
        gridPerfil.Children.Add(cardSeguro);
        gridPerfil.Children.Add(cardAvancado);
        ModalDebloatConteudo.Children.Add(gridPerfil);

        // 4. REMOVER TAMBÉM (Checkboxes opcionais)
        ModalDebloatConteudo.Children.Add(new TextBlock { Text = "Remover também", Style = SectionStyle, Margin = new Thickness(0, 2, 0, 6) });
        var wrapExtras = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var chkDlss = new CheckBox { Content = "DLSS (NGX)", Margin = new Thickness(0, 0, 22, 6), ToolTip = "Remove o módulo NGX/DLSS. Mantenha desmarcado se usa DLSS em jogos." };
        var chkPhysX = new CheckBox { Content = "PhysX", Margin = new Thickness(0, 0, 22, 6), ToolTip = "Remove o motor NVIDIA PhysX legado." };
        var chkHdAudio = new CheckBox { Content = "HD Audio", Margin = new Thickness(0, 0, 0, 6), ToolTip = "Remove o driver de áudio HDMI/DisplayPort da GPU." };
        wrapExtras.Children.Add(chkDlss);
        wrapExtras.Children.Add(chkPhysX);
        wrapExtras.Children.Add(chkHdAudio);
        ModalDebloatConteudo.Children.Add(wrapExtras);

        // 5. Avisos + Progresso + Log
        var boxAvisos = new Border
        {
            Background = Surface2,
            BorderBrush = BorderDefault,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var spAvisos = new StackPanel();
        spAvisos.Children.Add(new TextBlock { Text = "• Instala automaticamente e apaga tudo no fim.", Foreground = Secondary, FontSize = 12 });
        spAvisos.Children.Add(new TextBlock { Text = "• Vai instalar o driver. Reinicie depois.", Foreground = Warning, FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });
        boxAvisos.Child = spAvisos;
        ModalDebloatConteudo.Children.Add(boxAvisos);

        var barraProgresso = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8),
        };
        var caixaLog = new TextBox
        {
            Style = DarkBoxStyle,
            Height = 135,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12),
        };
        ModalDebloatConteudo.Children.Add(barraProgresso);
        ModalDebloatConteudo.Children.Add(caixaLog);

        // Banner de contagem regressiva (10 segundos) pós-instalação limpa
        var txtReinicio = new TextBlock
        {
            Text = "Instalação limpa concluída! O computador será reiniciado em 10 segundos...",
            Foreground = Warning,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var txtSubReinicio = new TextBlock
        {
            Text = "Após o reinício, os ajustes do NVIDIA Profile Inspector e o Painel de Controle NVIDIA abrirão automaticamente.",
            Foreground = Secondary,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 10),
        };
        var btnReiniciarAgora = new Button { Style = PriButton, Content = "Reiniciar agora", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        var btnCancelarReinicio = new Button { Style = SecButton, Content = "Cancelar reinício", Padding = new Thickness(12, 7, 12, 7) };
        var painelReinicio = new Border
        {
            Background = Surface2,
            BorderBrush = Warning,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed,
            Child = new StackPanel
            {
                Children =
                {
                    txtReinicio,
                    txtSubReinicio,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { btnReiniciarAgora, btnCancelarReinicio } }
                }
            },
        };
        ModalDebloatConteudo.Children.Add(painelReinicio);

        // Botão primário: Instalar limpo
        var btnInstalar = new Button
        {
            Style = PriButton,
            Content = "Instalar limpo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 11, 16, 11),
        };

        void DispararReinicioAgora()
        {
            timerReinicio?.Stop();
            timerReinicio = null;
            RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
            if (!PosDebloatNvidiaStore.ExecutarReinicio(0))
                Toasts.Mostrar(this, TipoToast.Erro, "Falha ao reiniciar", "Execute o reinício manualmente pelo menu Iniciar.");
        }

        btnReiniciarAgora.Click += (_, _) => DispararReinicioAgora();
        btnCancelarReinicio.Click += async (_, _) =>
        {
            timerReinicio?.Stop();
            timerReinicio = null;
            PosDebloatNvidiaStore.CancelarReinicio();
            painelReinicio.Visibility = Visibility.Collapsed;
            btnInstalar.Visibility = Visibility.Visible;
            caixaLog.AppendText("[!] Reinício automático cancelado pelo usuário. Os ajustes pós-boot continuam agendados para depois do próximo reinício." + Environment.NewLine);
            await RecarregarTelaAsync();
        };

        btnInstalar.Click += async (_, _) =>
        {
            if (!ExigirAdmin()) return;
            if (usarInstaladorLocal && string.IsNullOrWhiteSpace(caminhoInstaladorLocal))
            {
                Toasts.Mostrar(this, TipoToast.Aviso, "Selecione o instalador", "Clique em 'procurar...' e escolha o arquivo .exe oficial da NVIDIA.");
                return;
            }

            var canalEscolhido = (comboCanal.SelectedItem as ComboBoxItem)?.Tag is CanalDriverNvidia c
                ? c : CanalDriverNvidia.GameReady;
            var opcoes = new OpcoesNvidiaDebloat(
                InstaladorLocal: usarInstaladorLocal ? caminhoInstaladorLocal : null,
                Canal: canalEscolhido,
                Perfil: perfilDebloat,
                RemoverDlssNgx: chkDlss.IsChecked == true,
                RemoverPhysX: chkPhysX.IsChecked == true,
                RemoverHdAudio: chkHdAudio.IsChecked == true);

            var resumoComp = NvidiaDebloat.MontarListaComponentesRemover(opcoes);
            var perfilPos = GpuDriverTweaks.ObterPerfil(GpuDriverTweaks.LerEstadoPerfil().PerfilId) ?? GpuDriverTweaks.ObterPerfil("fps2")!;
            var confirmMsg = $"Iniciar o Auto NVIDIA Debloat e instalar o driver limpo?\n\n"
                           + $"• Fonte: {(usarInstaladorLocal ? System.IO.Path.GetFileName(caminhoInstaladorLocal) : $"Download oficial NVIDIA ({canalEscolhido})")}\n"
                           + $"• Perfil: {(perfilDebloat == PerfilDebloatNvidia.Avancado ? "Avançado" : "Seguro")} ({resumoComp.Count} componente(s) de bloat removidos)\n"
                           + "• Protegidos (mantidos): Display.Driver, NvContainer, NVI2, Control Panel\n"
                           + "• Comando de instalação: setup.exe -s -noreboot -clean -nofinish -passive\n\n"
                           + $"Ao concluir, o PC será reiniciado em 10 segundos (dá para cancelar; salve seus trabalhos antes). Depois do reinício, o perfil gráfico '{perfilPos.Nome}' é aplicado no driver e o Painel de Controle NVIDIA abre. O perfil pode ser removido na tela GPU. Continuar?";
            if (MessageBox.Show(confirmMsg, "Confirmação — Auto NVIDIA Debloat", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            btnInstalar.IsEnabled = false;
            barraProgresso.Visibility = Visibility.Visible;
            caixaLog.Visibility = Visibility.Visible;
            caixaLog.Clear();
            // O driver é trocado: ponto de restauração antes (cobre drivers; o Windows volta ao driver anterior).
            if (!await PontoAntes.GarantirAsync(this, "instalar o driver NVIDIA limpo", t => caixaLog.AppendText(t + Environment.NewLine)))
            {
                btnInstalar.IsEnabled = true;
                caixaLog.AppendText("Cancelado: o driver não foi alterado." + Environment.NewLine);
                return;
            }

            _ctsDebloat = new CancellationTokenSource();
            int codigo;
            try
            {
                codigo = await NvidiaDebloat.ExecutarAsync(
                    nomeGpu,
                    opcoes,
                    linha => Dispatcher.Invoke(() =>
                    {
                        caixaLog.AppendText(linha + Environment.NewLine);
                        caixaLog.ScrollToEnd();
                    }),
                    prog => Dispatcher.Invoke(() =>
                    {
                        if (prog.Percentual >= 0) barraProgresso.Value = prog.Percentual;
                        btnInstalar.Content = prog.Concluido ? "Instalar limpo" : $"Etapa {prog.Etapa}/6 — {prog.Detalhe}";
                    }),
                    _ctsDebloat.Token);
            }
            finally
            {
                _ctsDebloat.Dispose();
                _ctsDebloat = null;
                btnInstalar.IsEnabled = true;
                btnInstalar.Content = "Instalar limpo";
            }

            if (codigo == 0 && !opcoes.SomenteExtrair)
            {
                PosDebloatNvidiaStore.Padrao.AgendarPosReinicio(Environment.ProcessPath, perfilPos.Id, nomeGpu);
                caixaLog.AppendText($"==> Pós-reinício agendado: perfil '{perfilPos.Nome}' (NVIDIA Profile Inspector) + Painel de Controle NVIDIA." + Environment.NewLine);
                caixaLog.ScrollToEnd();

                NotificacoesStore.Padrao.Adicionar("GPU", "Driver NVIDIA limpo instalado",
                    $"PC reiniciando em 10s. Após o boot, será aplicado o perfil '{perfilPos.Nome}' e aberto o Painel de Controle NVIDIA.");
                Toasts.Mostrar(this, TipoToast.Aviso, "Reiniciando em 10 segundos",
                    "Instalação limpa concluída! O PC será reiniciado para finalizar o driver.");

                int restantes = 10;
                txtReinicio.Text = $"Instalação limpa concluída! O PC será reiniciado em {restantes} segundos...";
                painelReinicio.Visibility = Visibility.Visible;
                btnInstalar.Visibility = Visibility.Collapsed;

                timerReinicio?.Stop();
                timerReinicio = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timerReinicio.Tick += (_, _) =>
                {
                    restantes--;
                    if (restantes <= 0)
                    {
                        txtReinicio.Text = "Reiniciando o computador agora...";
                        DispararReinicioAgora();
                    }
                    else
                    {
                        txtReinicio.Text = $"Instalação limpa concluída! O PC será reiniciado em {restantes} {(restantes == 1 ? "segundo" : "segundos")}...";
                    }
                };
                timerReinicio.Start();
            }
            else if (codigo == 0)
            {
                Toasts.Mostrar(this, TipoToast.Sucesso, "Extração concluída", "Pacote limpo pronto na pasta temporária.");
            }
            else if (codigo != -2)
            {
                Toasts.Mostrar(this, TipoToast.Erro, "Debloat não concluído", "Consulte o registro detalhado exibido na janela.");
            }
        };

        ModalDebloatConteudo.Children.Add(btnInstalar);
        ModalDebloatOverlay.BeginAnimation(OpacityProperty, null);
        ModalDebloatOverlay.IsHitTestVisible = true;
        ModalDebloatOverlay.Visibility = Visibility.Visible;
        Movimento.AbrirModal(ModalDebloatOverlay, ModalDebloatOverlay.Children[0]);
    }

    Border CriarCardSelecao(string titulo, string subtitulo) =>
        CriarCardSelecao(titulo, new TextBlock { Text = subtitulo, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }, null);

    Border CriarCardSelecao(string titulo, UIElement subElemento, UIElement? acaoDireita)
    {
        var sp = new StackPanel();
        var topo = new Grid();
        topo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topo.Children.Add(new TextBlock { Text = titulo, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (acaoDireita is not null)
        {
            Grid.SetColumn(acaoDireita, 1);
            topo.Children.Add(acaoDireita);
        }
        sp.Children.Add(topo);
        var wrapSub = new Border { Margin = new Thickness(0, 4, 0, 0), Child = subElemento };
        sp.Children.Add(wrapSub);

        var card = new Border
        {
            Background = Surface2,
            BorderBrush = BorderStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 10, 12, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = sp,
            Focusable = true,
            FocusVisualStyle = (Style)Application.Current.Resources["FocoVisual"],
        };
        System.Windows.Automation.AutomationProperties.SetName(card, titulo);
        card.KeyDown += (_, e) =>
        {
            if (e.OriginalSource != card || e.Key is not (System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space)) return;
            e.Handled = true;
            card.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = MouseLeftButtonUpEvent });
        };
        return card;
    }

    void EstilizarCardSelecao(Border card, bool ativo)
    {
        card.BorderBrush = ativo ? Brand500 : BorderStrong;
        card.BorderThickness = new Thickness(ativo ? 1.8 : 1);
        card.Background = ativo ? BrandTint : Surface2;
    }

    bool ExigirAdmin()
    {
        if (Elevacao.EhAdmin()) return true;
        if (MessageBox.Show("Este ajuste altera configurações de máquina (HKLM / driver) e exige administrador.\n\nReabrir o Sevenzin como administrador?",
                "Elevação necessária", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
            Elevacao.ReabrirComoAdmin();
        return false;
    }

    // ─────────────────────────── Overclock (NVIDIA) ───────────────────────────

    Border CartaoOverclock(IReadOnlyList<InfoGpu> gpus, bool nvidiaSmi, GpuTelemetria? leituraInicial)
    {
        var sp = new StackPanel();
        bool temAmd = gpus.Any(g => g.Fabricante == FabricanteGpu.Amd);
        if (!nvidiaSmi)
        {
            var aviso = temAmd
                ? "Overclock por nvidia-smi disponível apenas com GPU NVIDIA. Para GPU AMD detectada, use os controles de Ajustes básicos / Power Gating / UMD na seção 'Ajustes do Driver' acima."
                : "Disponível apenas com GPU NVIDIA e driver (nvidia-smi) instalado.";
            sp.Children.Add(new TextBlock { Text = aviso, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
        }

        sp.Children.Add(new TextBlock
        {
            Text = "Overclock dentro do envelope de fábrica (via nvidia-smi oficial): travar o clock sustenta o boost e o Power Limit ajusta o teto térmico/energético permitido pela VBIOS. Exige administrador, confirmação explícita e é 100% reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });

        var tele = new TextBlock { Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        var plInfo = new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        var plBox = new TextBox { Style = DarkBoxStyle, Width = 90, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        GpuTelemetria? ultimaLeitura = null;

        async void AtualizarTele(GpuTelemetria? pronta = null)
        {
            var t = pronta ?? await Task.Run(GpuOverclock.Ler);
            ultimaLeitura = t;
            if (t is null)
            {
                tele.Text = "Não foi possível ler a GPU (nvidia-smi indisponível).";
                plInfo.Text = "";
                return;
            }
            tele.Text = $"{t.Nome}\nClock: {t.ClockMHz} MHz (boost máx {t.ClockMaxMHz}) · Memória: {t.MemClockMHz}/{t.MemMaxMHz} MHz\nTemperatura: {t.TempC:0.#} °C · Uso: {t.UsoPct:0.#}% · Power: {t.PowerW:0.#} W / limite {t.PowerLimitW:0.#} W (padrão {t.PowerPadraoW:0.#} W · mín {t.PowerMinW:0.#} W · máx {t.PowerMaxW:0.#} W) · VRAM: {t.MemUsadaMB}/{t.MemTotalMB} MB";
            if (string.IsNullOrWhiteSpace(plBox.Text))
                plBox.Text = t.PowerLimitW.ToString("0", CultureInfo.InvariantCulture);
            bool limiteMax = Math.Abs(t.PowerMaxW - t.PowerLimitW) < 0.5;
            plInfo.Text = limiteMax
                ? $"Power limit atual ({t.PowerLimitW:0.#} W) já está no teto de fábrica da VBIOS ({t.PowerMaxW:0.#} W); é possível reduzir (eficiência) ou restaurar o padrão ({t.PowerPadraoW:0.#} W)."
                : $"Faixa permitida pela VBIOS desta placa: {t.PowerMinW:0.#} W até {t.PowerMaxW:0.#} W (padrão {t.PowerPadraoW:0.#} W).";
        }
        AtualizarTele(leituraInicial);
        sp.Children.Add(tele);

        var botoes = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        var att = new Button { Style = SecButton, Content = "Atualizar leitura", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        att.Click += (_, _) => AtualizarTele();

        var travar = new Button { Style = PriButton, Content = "Travar boost (sustentado)", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        travar.Click += (_, _) =>
        {
            if (!ExigirAdmin()) return;
            var t = GpuOverclock.Ler();
            if (t is null) { Toasts.Mostrar(this, TipoToast.Erro, "GPU indisponível", "Não foi possível ler a GPU."); return; }
            int mx = Math.Max(1000, t.ClockMaxMHz), mn = Math.Max(500, mx - 600);
            if (MessageBox.Show($"Travar a faixa de clock da GPU ({t.Nome}) em {mn}–{mx} MHz?\n\nEfeito: sustenta o boost máximo durante jogos, aumentando consumo e temperatura em repouso.\nReversão: clique em 'Resetar clock' ou 'Restaurar padrão de fábrica'.\n\nContinuar?",
                    "Confirmação — Boost sustentado (GPU)", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            var r = GpuOverclock.TravarClock(mn, mx);
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("GPU", "Clock de boost travado", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Clock travado" : "Falha", r.Mensagem,
                r.Ok ? (Action?)(() => { GpuOverclock.ResetarClock(); AtualizarTele(); }) : null);
            AtualizarTele();
        };

        var resetar = new Button { Style = SecButton, Content = "Resetar clock", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        resetar.Click += (_, _) =>
        {
            if (!ExigirAdmin()) return;
            var r = GpuOverclock.ResetarClock();
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("GPU", "Clock resetado", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, "Clock resetado", r.Mensagem);
            AtualizarTele();
        };

        var restaurar = new Button { Style = SecButton, Content = "Restaurar padrão de fábrica", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        restaurar.Click += (_, _) =>
        {
            if (!ExigirAdmin()) return;
            var padrao = ultimaLeitura?.PowerPadraoW ?? 0;
            if (MessageBox.Show($"Restaurar clock dinâmico (-rgc) e power limit de fábrica ({(padrao > 0 ? $"{padrao:0.#} W" : "padrão")})?",
                    "Confirmação — Restaurar GPU", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                return;
            var r = GpuOverclock.RestaurarPadrao(padrao);
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("GPU", "Overclock restaurado", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Padrão restaurado" : "Falha", r.Mensagem);
            plBox.Text = "";
            AtualizarTele();
        };

        botoes.Children.Add(att);
        botoes.Children.Add(travar);
        botoes.Children.Add(resetar);
        botoes.Children.Add(restaurar);
        sp.Children.Add(botoes);

        sp.Children.Add(plInfo);
        var linhaPl = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        linhaPl.Children.Add(new TextBlock { Text = "Power limit (W):", Foreground = Secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        linhaPl.Children.Add(plBox);
        var aplicarPl = new Button { Style = SecButton, Content = "Aplicar Power Limit", Padding = new Thickness(12, 7, 12, 7) };
        aplicarPl.Click += (_, _) =>
        {
            if (!ExigirAdmin()) return;
            var t = ultimaLeitura ?? GpuOverclock.Ler();
            if (t is null) { Toasts.Mostrar(this, TipoToast.Erro, "GPU indisponível", "Não foi possível ler a GPU."); return; }
            if (!double.TryParse(plBox.Text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double watts))
            {
                Toasts.Mostrar(this, TipoToast.Aviso, "Valor inválido", "Informe o limite em watts (ex.: 160).");
                return;
            }
            var erro = GpuOverclock.ValidarPowerLimit(watts, t);
            if (erro is not null) { Toasts.Mostrar(this, TipoToast.Aviso, "Fora da faixa", erro); return; }
            double anterior = t.PowerLimitW;
            if (MessageBox.Show($"Ajustar o Power Limit de '{t.Nome}' de {anterior:0.#} W para {watts:0.#} W?\n\nReversível em 'Restaurar padrão de fábrica' ({t.PowerPadraoW:0.#} W).",
                    "Confirmação — Power Limit", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            var r = GpuOverclock.DefinirPowerLimit(watts);
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("GPU", $"Power limit ajustado ({watts:0.#} W)", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Power Limit aplicado" : "Falha", r.Mensagem,
                r.Ok ? (Action?)(() => { GpuOverclock.DefinirPowerLimit(anterior); AtualizarTele(); }) : null);
            AtualizarTele();
        };
        linhaPl.Children.Add(aplicarPl);
        sp.Children.Add(linhaPl);

        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 14), Child = sp };
    }

    // ─────────────────────────── Adaptadores e Modos de Vídeo ───────────────────────────

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
        esq.Children.Add(new TextBlock { Text = g.Adaptador, FontSize = 15.5, FontWeight = FontWeights.SemiBold });
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
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 12), Child = sp };
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

    Border CartaoModosVideo(IReadOnlyList<(DispositivoVideo d, IReadOnlyList<ModoVideo> modos, ModoVideo? atual)> lidos)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Modos de vídeo suportados (somente leitura)", FontSize = 15.5, FontWeight = FontWeights.SemiBold });

        foreach (var (d, modos, atual) in lidos)
        {

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

        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 8), Child = sp };
    }

    static void AbrirUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro ao abrir", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
