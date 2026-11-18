using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Ajustes" (Figma) — tweaks reversíveis por seção, backed pelo MotorTweaks real.
/// Toggle liga = Aplicar (prévia no efeito descrito), desliga = Desfazer. Todo item tem prévia,
/// backup, desfazer e auditoria.</summary>
public partial class AjustesView : UserControl
{
    readonly MotorTweaks _motor = new(new RegistroWindows());
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush SuccessBg => new SolidColorBrush((Color)Application.Current.Resources["SuccessBg"]);
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Style Switch => (Style)Application.Current.Resources["ToggleSwitch"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];

    sealed record LinhaRef(Tweak T, Border Chip); // liga o toggle ao seu chip "Aplicado" para atualizar em lugar

    public AjustesView()
    {
        InitializeComponent();
        foreach (var grupo in CatalogoTweaks.Todos.GroupBy(t => t.Categoria))
        {
            Lista.Children.Add(new TextBlock { Text = grupo.Key.ToUpperInvariant(), Style = SectionStyle });
            Lista.Children.Add(Cartao(grupo.Select(LinhaTweak)));
        }
        // Rede por adaptador (dinâmica): 5 tweaks por interface, valores do original, reversíveis.
        Lista.Children.Add(new TextBlock { Text = "REDE (POR ADAPTADOR)", Style = SectionStyle, Margin = new Thickness(0, 6, 0, 10) });
        Lista.Children.Add(CartaoRede());
        // Rede global (TCP): autotuning normal + CTCP, valores do original (netsh), reversível.
        Lista.Children.Add(new TextBlock { Text = "REDE GLOBAL (TCP)", Style = SectionStyle, Margin = new Thickness(0, 6, 0, 10) });
        Lista.Children.Add(CartaoRedeGlobal());
        // Memória do sistema (MMAgent: Compressão de memória + Application PreLaunch) — reversível.
        Lista.Children.Add(new TextBlock { Text = "MEMÓRIA DO SISTEMA (MMAGENT)", Style = SectionStyle, Margin = new Thickness(0, 6, 0, 10) });
        Lista.Children.Add(CartaoMmAgent());
        // Offloads e economia de energia da placa de rede (NetAdapter cmdlets) — reversível.
        Lista.Children.Add(new TextBlock { Text = "REDE AVANÇADA (OFFLOADS E ECONOMIA DO ADAPTADOR)", Style = SectionStyle, Margin = new Thickness(0, 6, 0, 10) });
        Lista.Children.Add(CartaoOffloadsRede());
        AtualizarContador();
    }

    Border CartaoMmAgent()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Controle de Compressão de Memória e Application PreLaunch do Windows (MMAgent, comando literal em 03_comandos_sistema.txt). Em PCs com ≥16 GB de RAM pode reduzir micro-uso de CPU; em pouca RAM pode aumentar paginação em disco. Exige administrador e é reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        var status = new TextBlock { Text = "Lendo estado do MMAgent…", Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(status);

        async Task AtualizarAsync()
        {
            var est = await Task.Run(OtimizacoesSistema.LerMmAgent);
            string S(bool? b) => b switch { true => "Ligado", false => "Desligado", _ => "—" };
            status.Text = $"Estado atual: Compressão de memória {S(est.CompressaoMemoria)} · Application PreLaunch {S(est.PreLaunch)}.";
        }
        _ = AtualizarAsync();

        var botoes = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var desativar = new Button { Style = (Style)Application.Current.Resources["PrimaryButton"], Content = "Desativar compressão e PreLaunch", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        desativar.Click += async (_, _) =>
        {
            if (!Elevacao.EhAdmin())
            {
                if (MessageBox.Show("Alterar o MMAgent exige administrador.\n\nReabrir o Sevenzin como administrador?", "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    Elevacao.ReabrirComoAdmin();
                return;
            }
            if (MessageBox.Show("Desativar Compressão de Memória e Application PreLaunch do Windows?\n\nEfeito: libera ciclos de CPU usados na compactação de RAM, mas aumenta o uso direto de memória física.\nReversão: clique em 'Restaurar padrão do Windows'.",
                    "Confirmação — MMAgent", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            var r = await Task.Run(OtimizacoesSistema.DesativarMmAgent);
            await AtualizarAsync();
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Ajustes", "MMAgent desativado", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "MMAgent atualizado" : "Falha", r.Mensagem);
        };
        var restaurar = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Restaurar padrão do Windows", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        restaurar.Click += async (_, _) =>
        {
            if (!Elevacao.EhAdmin()) { MessageBox.Show("Execute como administrador para restaurar o MMAgent.", "Elevação", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var r = await Task.Run(OtimizacoesSistema.RestaurarMmAgent);
            await AtualizarAsync();
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Ajustes", "MMAgent restaurado", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "MMAgent restaurado" : "Falha", r.Mensagem);
        };
        botoes.Children.Add(desativar);
        botoes.Children.Add(restaurar);
        sp.Children.Add(botoes);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    Border CartaoOffloadsRede()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Desativa offloads de pacote (LSO, RSC, Checksum, IPsec) e economia de energia no adaptador ativo (cmdlets literais do original). Pode reduzir variação de latência em jogos competitivos, mas eleva levemente o uso de CPU em downloads pesados. Reversível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        });

        var combo = new ComboBox { Style = (Style)Application.Current.Resources["DarkCombo"], MinWidth = 240, Margin = new Thickness(0, 0, 8, 6) };
        async Task CarregarAdaptadoresAsync()
        {
            var nomes = await Task.Run(OtimizacoesSistema.ListarNomesAdaptadoresAtivos);
            combo.Items.Clear();
            foreach (var n in nomes) combo.Items.Add(n);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        _ = CarregarAdaptadoresAsync();

        var linha = new WrapPanel();
        linha.Children.Add(combo);
        var aplicar = new Button { Style = (Style)Application.Current.Resources["PrimaryButton"], Content = "Desativar offloads e economia", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        aplicar.Click += async (_, _) =>
        {
            if (combo.SelectedItem is not string nome || !OtimizacoesSistema.NomeAdaptadorValido(nome))
            {
                Toasts.Mostrar(this, TipoToast.Aviso, "Adaptador ausente", "Selecione um adaptador de rede ativo.");
                return;
            }
            if (!Elevacao.EhAdmin())
            {
                if (MessageBox.Show("Alterar propriedades avançadas do adaptador exige administrador.\n\nReabrir como administrador?", "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    Elevacao.ReabrirComoAdmin();
                return;
            }
            if (MessageBox.Show($"Desativar LSO, RSC, Checksum/IPsec Offload e Economia de Energia no adaptador '{nome}'?\n\nA conexão pode reconectar por 1–2 segundos. Reversível clicando em 'Restaurar offloads'.",
                    "Confirmação — Offloads de Rede", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var r = await Task.Run(() => OtimizacoesSistema.DesativarOffloadsRede(nome));
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Rede", $"Offloads desativados ({nome})", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Offloads desativados" : "Falha", r.Mensagem);
        };
        var restaurar = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Restaurar offloads", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6) };
        restaurar.Click += async (_, _) =>
        {
            if (combo.SelectedItem is not string nome || !OtimizacoesSistema.NomeAdaptadorValido(nome)) return;
            if (!Elevacao.EhAdmin()) { MessageBox.Show("Execute como administrador para restaurar os offloads.", "Elevação", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var r = await Task.Run(() => OtimizacoesSistema.RestaurarOffloadsRede(nome));
            if (r.Ok) NotificacoesStore.Padrao.Adicionar("Rede", $"Offloads restaurados ({nome})", r.Mensagem);
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "Offloads restaurados" : "Falha", r.Mensagem);
        };
        linha.Children.Add(aplicar);
        linha.Children.Add(restaurar);
        sp.Children.Add(linha);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    Border CartaoRedeGlobal()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Ajustes GLOBAIS de TCP do original (netsh): autotuninglevel=normal e congestionprovider=ctcp. Exige administrador; reversível. Observação: CTCP é legado — o CUBIC (padrão atual do Windows) pode ser melhor em redes modernas.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        var status = new TextBlock { Foreground = Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        void Atualizar()
        {
            var (at, cong) = RedeGlobal.EstadoAtual();
            status.Text = $"Estado atual: autotuning {(at ?? "?")} · congestionamento {(cong ?? "?")}.";
        }
        Atualizar();
        sp.Children.Add(status);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var aplicar = new Button { Style = (Style)Application.Current.Resources["PrimaryButton"], Content = "Aplicar (normal + CTCP)", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        aplicar.Click += (_, _) =>
        {
            if (MessageBox.Show("Aplicar os ajustes globais de TCP do original (autotuning normal + congestion provider ctcp)?\n\nExige administrador e é reversível.",
                    "Confirmação — TCP global", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
            var r = RedeGlobal.Aplicar();
            Atualizar();
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "TCP global aplicado" : "Falha no TCP global", r.Mensagem);
        };
        var restaurar = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Restaurar padrão", Padding = new Thickness(12, 7, 12, 7) };
        restaurar.Click += (_, _) =>
        {
            var r = RedeGlobal.Restaurar();
            Atualizar();
            Toasts.Mostrar(this, r.Ok ? TipoToast.Sucesso : TipoToast.Erro, r.Ok ? "TCP restaurado" : "Falha ao restaurar", r.Mensagem);
        };
        botoes.Children.Add(aplicar);
        botoes.Children.Add(restaurar);
        sp.Children.Add(botoes);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    Border CartaoRede()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Ajustes de rede por adaptador (valores do original: ACK imediato, NoDelay, DelAck, NetBIOS off, DNS multicast off). Exige administrador; cada aplicação pede confirmação e tudo é desfazível.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        var lista = new StackPanel();
        sp.Children.Add(lista);
        var atualizar = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Atualizar adaptadores", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7) };
        atualizar.Click += (_, _) => MontarRede(lista);
        sp.Children.Add(atualizar);
        MontarRede(lista);
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    void MontarRede(StackPanel host)
    {
        host.Children.Clear();
        var interfaces = RedePorInterface.Listar();
        if (interfaces.Count == 0)
        {
            host.Children.Add(new TextBlock { Text = "Nenhum adaptador IP ativo.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });
            return;
        }
        foreach (var i in interfaces)
        {
            var tweaks = RedePorInterface.TweaksPara(i.Guid);
            int aplicados = tweaks.Count(_motor.Aplicado);

            var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = i.Descricao, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            info.Children.Add(new TextBlock { Text = $"{aplicados} de {tweaks.Count} ajustes aplicados · reversível e auditado", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });

            var botoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var aplicar = new Button { Style = (Style)Application.Current.Resources["PrimaryButton"], Content = "Aplicar os 5", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 7, 12, 7) };
            aplicar.Click += (_, _) =>
            {
                if (MessageBox.Show($"Aplicar os 5 ajustes de rede no adaptador:\n\n{i.Descricao}\n\nRisco médio — meça A/B. O estado anterior fica salvo para desfazer.",
                        "Confirmação — rede por adaptador", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
                try
                {
                    foreach (var t in tweaks) _motor.Aplicar(t);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
                MontarRede(host);
            };
            var desfazer = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Content = "Desfazer", Padding = new Thickness(12, 7, 12, 7) };
            desfazer.Click += (_, _) =>
            {
                try
                {
                    foreach (var t in tweaks) _motor.Desativar(t);
                    Toasts.Mostrar(this, TipoToast.Sucesso, "Rede restaurada", $"Ajustes desativados em {i.Descricao}.");
                }
                catch (Exception ex) { Toasts.Mostrar(this, TipoToast.Erro, "Falha ao restaurar rede", ex.Message); }
                MontarRede(host);
            };
            botoes.Children.Add(aplicar);
            botoes.Children.Add(desfazer);

            var linha = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(info, 0);
            Grid.SetColumn(botoes, 1);
            linha.Children.Add(info);
            linha.Children.Add(botoes);
            host.Children.Add(linha);
        }
    }

    static Border Cartao(IEnumerable<UIElement> linhas)
    {
        var sp = new StackPanel();
        bool primeiro = true;
        foreach (var l in linhas)
        {
            if (!primeiro) sp.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["BorderDefaultBrush"], Margin = new Thickness(0, 4, 0, 4) });
            sp.Children.Add(l);
            primeiro = false;
        }
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 20), Child = sp };
    }

    UIElement LinhaTweak(Tweak t)
    {
        var desc = t.ExigeAdmin ? t.Descricao + "  ·  requer administrador" : t.Descricao;
        if (t.ExigeConfirmacao) desc += $"  ·  risco: {t.Risco}";
        var g = LinhaBase(t.Nome, desc, out _);

        var sw = new ToggleButton { Style = Switch, VerticalAlignment = VerticalAlignment.Center, IsChecked = _motor.Aplicado(t) };
        var chip = ChipAplicado(sw, t);
        sw.Tag = new LinhaRef(t, chip);
        sw.Click += Toggle_Click;

        var direita = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        direita.Children.Add(chip);
        direita.Children.Add(sw);
        Grid.SetColumn(direita, 1);
        g.Children.Add(direita);
        return g;
    }

    // Chip "● Aplicado · Desfazer" (adaptado do original): visível só quando o item está aplicado; "Desfazer"
    // chama o mesmo fluxo de reversão do toggle. O toggle continua sendo o controle principal.
    Border ChipAplicado(ToggleButton sw, Tweak t)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "● Aplicado", Foreground = Success, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var des = new TextBlock { Text = "Desfazer", Foreground = Secondary, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, TextDecorations = TextDecorations.Underline };
        des.MouseLeftButtonUp += (_, _) => { sw.IsChecked = false; Toggle_Click(sw, new RoutedEventArgs()); };
        sp.Children.Add(des);
        return new Border { Background = SuccessBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = sp, Visibility = _motor.Aplicado(t) ? Visibility.Visible : Visibility.Collapsed };
    }

    static Grid LinhaBase(string titulo, string descricao, out StackPanel left)
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(new TextBlock { Text = titulo, FontSize = 14 });
        left.Children.Add(new TextBlock { Text = descricao, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(left, 0);
        g.Children.Add(left);
        return g;
    }

    void Toggle_Click(object sender, RoutedEventArgs e)
    {
        var sw = (ToggleButton)sender;
        var r = (LinhaRef)sw.Tag;
        var t = r.T;

        // Elevação sob demanda: ajustes de máquina (HKLM) exigem administrador.
        if (t.ExigeAdmin && !Elevacao.EhAdmin())
        {
            sw.IsChecked = _motor.Aplicado(t); // mantém o estado real
            if (MessageBox.Show("Esta otimização mexe no sistema (HKLM) e exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        // Itens de risco ALTO/CRÍTICO pedem confirmação explícita antes de aplicar (regra de segurança nº 2).
        if (t.ExigeConfirmacao && sw.IsChecked == true)
        {
            sw.IsChecked = _motor.Aplicado(t);
            if (MessageBox.Show($"{t.Nome}\n\nRisco: {t.Risco}.\n\nVocê pode desfazer depois (o valor anterior fica salvo).\nAplicar mesmo assim?",
                    "Confirmação — ajuste de segurança", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            sw.IsChecked = true;
        }
        try
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso($"Ajuste: {t.Nome}");
            if (sw.IsChecked == true)
            {
                _motor.Aplicar(t);
                bool confirmado = _motor.Aplicado(t); // lê o estado real antes de declarar sucesso
                NotificacoesStore.Padrao.Adicionar("Ajustes", $"'{t.Nome}' aplicado", $"Novo valor: {(t.Tipo == TipoValor.String ? t.ValorTexto : t.ValorOtimizado.ToString())}. Desfazer restaura o anterior.");
                Toasts.Mostrar(this, confirmado ? TipoToast.Sucesso : TipoToast.Aviso, confirmado ? "Ajuste aplicado" : "Ajuste não confirmado",
                    confirmado ? t.Nome : $"Não foi possível confirmar '{t.Nome}' no registro — verifique permissões.",
                    confirmado ? (Action?)(() => { if (_motor.Desativar(t)) { sw.IsChecked = false; AtualizarLinha(r); AtualizarContador(); } }) : null);
            }
            else if (!_motor.Desativar(t))
            {
                sw.IsChecked = _motor.Aplicado(t);
                Toasts.Mostrar(this, TipoToast.Aviso, "Alteração bloqueada", $"O Windows manteve '{t.Nome}' protegido nesta chave.");
            }
            else
            {
                NotificacoesStore.Padrao.Adicionar("Ajustes", $"'{t.Nome}' desativado", "Valor anterior ou padrão do Windows restaurado.");
                Toasts.Mostrar(this, TipoToast.Sucesso, "Ajuste desativado", t.Nome);
            }
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = _motor.Aplicado(t);
            var msg = t.Caminho.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase)
                ? "O Windows bloqueia a gravação nesta chave do Defender (Proteção contra adulteração ativa). Desative-a em Segurança do Windows → Proteção contra vírus e ameaças → Configurações de proteção contra adulteração e tente de novo."
                : "O Windows protegeu esta chave contra gravação direta (TrustedInstaller / política de sistema).";
            NotificacoesStore.Padrao.Adicionar("Ajustes", $"Acesso bloqueado em '{t.Nome}'", msg);
            Toasts.Mostrar(this, TipoToast.Erro, "Chave protegida", msg);
            MessageBox.Show(msg, "Permissão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            sw.IsChecked = _motor.Aplicado(t);
            NotificacoesStore.Padrao.Adicionar("Ajustes", $"Erro em '{t.Nome}'", ex.Message);
            Toasts.Mostrar(this, TipoToast.Erro, "Falha no ajuste", ex.Message);
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RecuperacaoFalhaStore.Padrao.DefinirOperacaoEmCurso(null);
        }
        AtualizarLinha(r);
        AtualizarContador();
    }

    void AtualizarLinha(LinhaRef r) =>
        r.Chip.Visibility = _motor.Aplicado(r.T) ? Visibility.Visible : Visibility.Collapsed;

    void AtualizarContador()
    {
        int total = CatalogoTweaks.Todos.Count;
        int ativos = CatalogoTweaks.Todos.Count(_motor.Aplicado);
        bool tudo = ativos == total && total > 0;
        ResumoTxt.Text = (tudo ? "Otimizado · " : "") + $"{ativos} de {total} ajustes ativos";
        ResumoTxt.Foreground = tudo ? Success : Secondary;
        ResumoPill.Background = tudo ? SuccessBg : Surface2;
        Contador.Text = "Reversível e auditado · máquina (HKLM) pede admin · meça A/B antes de confiar no ganho";
    }
}
