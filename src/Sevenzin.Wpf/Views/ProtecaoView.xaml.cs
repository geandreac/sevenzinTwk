using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Proteção" (paridade com a tela 19 do original, 03_telas.md): mostra o estado REAL de cada
/// mecanismo de segurança (UAC, Defender, SmartScreen, HVCI, mitigações, Windows Update, Secure Boot, TPM) e
/// oferece o ajuste correspondente — risco ALTO/CRÍTICO, com confirmação explícita e desfazer pelo MotorTweaks.
/// Nada é alterado sem o usuário confirmar.</summary>
public partial class ProtecaoView : UserControl
{
    readonly MotorTweaks _motor = new(new RegistroWindows());
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static Style Switch => (Style)Application.Current.Resources["ToggleSwitch"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];

    public ProtecaoView()
    {
        InitializeComponent();
        Montar();
    }

    void Montar()
    {
        Lista.Children.Clear();

        Lista.Children.Add(new TextBlock { Text = "Mecanismos de segurança", Style = SectionStyle });
        var linhas = new List<UIElement>();
        linhas.Add(Linha("Controle de Conta de Usuário (UAC)", SegurancaSistema.UacHabilitado(), "uac-off",
            "Pede confirmação antes de programas elevarem privilégios.", requerReinicio: true));
        linhas.Add(Linha("Proteção em tempo real (Defender)", SegurancaSistema.DefenderTempoRealAtivo(), "defender-rt-off",
            "Monitora arquivos e programas em busca de ameaças."));
        linhas.Add(Linha("Microsoft Defender (anti-spyware)", SegurancaSistema.DefenderTempoRealAtivo(), "defender-antispyware-off",
            "Mecanismo antivírus do Windows.", observacaoEstado: "estado da proteção em tempo real"));
        linhas.Add(Linha("SmartScreen", SegurancaSistema.SmartScreenAtivo(), "smartscreen-off",
            "Avisa antes de executar apps/arquivos não reconhecidos."));
        linhas.Add(Linha("Isolamento de Núcleo (HVCI)", SegurancaSistema.HvciAtivo(), "hvci-off",
            "Protege o kernel com virtualização de integridade de memória.", requerReinicio: true));
        linhas.Add(Linha("Mitigações Spectre/Meltdown", SegurancaSistema.MitigacoesAtivas(), "mitigacoes-off",
            "Mitigações de hardware contra execução especulativa.", requerReinicio: true));
        linhas.Add(Linha("Atualizações automáticas", SegurancaSistema.AtualizacoesAutomaticasAtivas(), "wu-pausar",
            "Baixa e instala atualizações de segurança automaticamente."));
        Lista.Children.Add(Ui.Grupo(linhas));

        Lista.Children.Add(new TextBlock { Text = "Estado da plataforma", Style = SectionStyle });
        Lista.Children.Add(Ui.Grupo(new UIElement[]
        {
            CartaoInformativo("Secure Boot", SegurancaSistema.SecureBootAtivo(), "verifica a assinatura do sistema ao iniciar"),
            CartaoInformativo("TPM", SegurancaSistema.TpmHabilitado(), "chip de segurança usado por BitLocker/Windows Hello"),
        }));

        // Write-cache por disco (paridade 05 §7): tweak dinâmico por disco, mesmo motor reversível.
        var discos = CacheEscrita.Listar();
        if (discos.Count > 0)
        {
            Lista.Children.Add(new TextBlock { Text = "Cache de escrita por disco", Style = SectionStyle });
            Lista.Children.Add(Ui.Grupo(discos.Select(LinhaDisco)));
        }

        Lista.Children.Add(new TextBlock
        {
            Text = "Desligar mecanismos de segurança deixa o PC vulnerável a malware. Todos os itens acima são reversíveis (desfazer restaura o valor anterior) e exigem administrador. Itens marcados com * só valem após reiniciar. O write-cache forçado arrisca perda de dados em queda de energia.",
            Foreground = Warning, FontSize = 12, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
    }

    /// <summary>Linha de write-cache por disco: estado atual + toggle do tweak dinâmico (mesma confirmação).</summary>
    UIElement LinhaDisco(DiscoCache d)
    {
        var t = CacheEscrita.TweakParaDisco(d);
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var esq = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        esq.Children.Add(new TextBlock { Text = d.Modelo, FontSize = 14 });
        esq.Children.Add(new TextBlock
        {
            Text = $"Cache de escrita: {(d.CacheForcadoAtual == 1 ? "forçado (CacheIsPowerProtected=1)" : "padrão do Windows")}  ·  risco: {t.Risco}.",
            Foreground = d.CacheForcadoAtual == 1 ? Warning : Muted,
            FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(esq, 0);
        g.Children.Add(esq);

        var sw = new ToggleButton { Style = Switch, VerticalAlignment = VerticalAlignment.Center, Tag = t, IsChecked = _motor.Aplicado(t) };
        sw.Click += Toggle_Click;
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        return g;
    }

    /// <summary>Linha com estado real + toggle do tweak correspondente (confirmação explícita ao ligar).</summary>
    UIElement Linha(string titulo, bool? ativo, string tweakId, string descricao, bool requerReinicio = false, string? observacaoEstado = null)
    {
        var t = CatalogoTweaks.Todos.First(x => x.Id == tweakId);
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var esq = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        esq.Children.Add(new TextBlock { Text = titulo + (requerReinicio ? " *" : ""), FontSize = 14, FontWeight = FontWeights.SemiBold });
        esq.Children.Add(new TextBlock
        {
            Text = descricao,
            Foreground = Secondary,
            FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        esq.Children.Add(new TextBlock { Text = $"Risco de desligar: {t.Risco}.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(esq, 0);
        g.Children.Add(esq);

        var sw = new ToggleButton { Style = Switch, VerticalAlignment = VerticalAlignment.Center, Tag = t, IsChecked = _motor.Aplicado(t) };
        sw.Click += Toggle_Click;
        Grid.SetColumn(sw, 1);
        g.Children.Add(sw);
        // O interruptor liga o AJUSTE (que desliga a proteção): o estado real da proteção fica escrito ao lado.
        g.ColumnDefinitions.Insert(1, new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(sw, 2);
        var estado = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0), MaxWidth = 190 };
        estado.Children.Add(new TextBlock { Text = ativo switch { true => "Proteção ativa", false => "Proteção desligada", _ => "Padrão do Windows" },
            Foreground = ativo switch { true => Success, false => Warning, _ => Muted }, FontSize = 12, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right });
        estado.Children.Add(new TextBlock { Text = observacaoEstado ?? (sw.IsChecked == true ? "ajuste aplicado" : "sem ajuste"), Foreground = Muted, FontSize = 12, TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap });
        estado.ToolTip = NomeEstado(ativo);
        Grid.SetColumn(estado, 1);
        g.Children.Add(estado);
        System.Windows.Automation.AutomationProperties.SetName(sw, t.Nome);
        return g;
    }

    static string NomeEstado(bool? ativo) => ativo switch
    {
        true => "ativo",
        false => "desligado",
        null => "indeterminado (padrão do Windows: ativo)",
    };

    UIElement CartaoInformativo(string titulo, bool? ativo, string descricao)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = titulo, FontSize = 14, FontWeight = FontWeights.SemiBold });
        var estado = new TextBlock
        {
            Text = NomeEstado(ativo),
            Foreground = ativo == true ? Success : ativo == false ? Warning : Muted,
            FontSize = 12, Margin = new Thickness(0, 3, 0, 0),
        };
        sp.Children.Add(estado);
        sp.Children.Add(new TextBlock { Text = descricao + ". Ajuste pelo setup da BIOS/UEFI.", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        return sp;
    }

    void Toggle_Click(object sender, RoutedEventArgs e)
    {
        var sw = (ToggleButton)sender;
        var t = (Tweak)sw.Tag;

        if (t.ExigeAdmin && !Elevacao.EhAdmin())
        {
            sw.IsChecked = _motor.Aplicado(t);
            if (MessageBox.Show("Este ajuste mexe no sistema (HKLM) e exige administrador.\n\nReabrir o SevenzinTwks como administrador?",
                    "Elevação", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                Elevacao.ReabrirComoAdmin();
            return;
        }
        if (sw.IsChecked == true)
        {
            sw.IsChecked = _motor.Aplicado(t);
            if (MessageBox.Show($"{t.Nome}\n\nRisco: {t.Risco}.\n\nO valor anterior fica salvo e pode ser desfeito.\nAplicar mesmo assim?",
                    "Confirmação — ajuste de segurança", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            sw.IsChecked = true;
        }
        try
        {
            if (sw.IsChecked == true)
            {
                _motor.Aplicar(t);
                bool confirmado = _motor.Aplicado(t);
                NotificacoesStore.Padrao.Adicionar("Proteção", $"'{t.Nome}' aplicado", "Confirmação explícita feita. Desfazer restaura o valor anterior.");
                Toasts.Mostrar(this, confirmado ? TipoToast.Sucesso : TipoToast.Aviso, confirmado ? "Alteração aplicada" : "Alteração não confirmada", t.Nome,
                    confirmado ? (Action?)(() => { if (_motor.Desfazer(t)) Montar(); }) : null);
            }
            else if (!_motor.Desfazer(t))
            {
                sw.IsChecked = true; // mantém "ligado": não havia backup (valor aplicado fora do SevenzinTwks)
                Toasts.Mostrar(this, TipoToast.Aviso, "Sem backup para desfazer", $"'{t.Nome}' já estava aplicado antes do SevenzinTwks — não há valor anterior salvo para restaurar.");
            }
            else
            {
                NotificacoesStore.Padrao.Adicionar("Proteção", $"'{t.Nome}' desfeito", "Valor anterior restaurado.");
                Toasts.Mostrar(this, TipoToast.Sucesso, "Alteração desfeita", t.Nome);
            }
            Montar();
        }
        catch (UnauthorizedAccessException)
        {
            sw.IsChecked = _motor.Aplicado(t);
            var msg = t.Caminho.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase)
                ? "O Windows bloqueia a gravação nesta chave do Defender (Proteção contra adulteração ativa). Desative-a em Segurança do Windows → Proteção contra vírus e ameaças → Configurações de proteção contra adulteração e tente de novo."
                : "Sem permissão para gravar nesta chave do registro. Feche e reabra o SevenzinTwks como administrador (botão direito → Executar como administrador).";
            Toasts.Mostrar(this, TipoToast.Erro, "Chave protegida", msg);
            MessageBox.Show(msg, "Permissão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            sw.IsChecked = _motor.Aplicado(t);
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
