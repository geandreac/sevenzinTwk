using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "BIOS/UEFI": estado da plataforma (só leitura) + instruções seguras. Nenhuma ação altera a BIOS.
/// Toda alteração de firmware é manual pelo usuário no setup do fabricante — aqui só há informação e orientação.</summary>
public partial class BiosView : UserControl
{
    static Brush Muted => (Brush)Application.Current.Resources["TextMutedBrush"];
    static Brush Secondary => (Brush)Application.Current.Resources["TextSecondaryBrush"];
    static Brush Warning => (Brush)Application.Current.Resources["WarningBrush"];
    static Brush Success => (Brush)Application.Current.Resources["SuccessBrush"];
    static Brush Primary => (Brush)Application.Current.Resources["TextPrimaryBrush"];
    static Brush Surface2 => (Brush)Application.Current.Resources["Surface2Brush"];
    static Brush Borda => (Brush)Application.Current.Resources["BorderDefaultBrush"];
    static Style CardStyle => (Style)Application.Current.Resources["Card"];
    static Style SectionStyle => (Style)Application.Current.Resources["SectionLabel"];

    public BiosView()
    {
        InitializeComponent();
        Montar();
    }

    void Montar()
    {
        var itens = Relatorio.Coletar();
        string Val(string n) => itens.FirstOrDefault(i => i.Nome == n)?.Valor ?? "—";

        // Layout em 2 colunas (adaptado do original): etapas numeradas à esquerda, card de specs à direita.
        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });

        var esq = new StackPanel();
        esq.Children.Add(new TextBlock { Text = "ETAPAS (AÇÕES MANUAIS DO USUÁRIO — VARIAM POR FABRICANTE)", Style = SectionStyle });
        var passos = new StackPanel();
        passos.Children.Add(Passo(1, "Entrar no setup da BIOS/UEFI", "Reinicie e pressione a tecla do fabricante no POST (comum: Del, F2, F10 ou F12). A tecla aparece na tela inicial.", false));
        passos.Children.Add(Passo(2, "Revisar ordem de boot e Secure Boot", "Confira no setup; mantenha o Secure Boot ligado por segurança. ⚠ Alto risco desligar — só com motivo claro.", true));
        passos.Children.Add(Passo(3, "Configurar perfil de memória (XMP / EXPO / DOCP)", "Procure o perfil de memória e selecione o do seu kit. Melhora a frequência da RAM. ⚠ Potencialmente disruptivo: pode não dar boot se o kit/placa não suportar — anote os valores antes.", true));
        passos.Children.Add(Passo(4, "Atualizar a BIOS", "Baixe a versão SÓ do site oficial do fabricante da placa e siga o procedimento dele. ⚠ Alto risco: queda de energia durante o flash pode inutilizar a placa. Use no-break se possível.", true));
        esq.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 16), Child = passos });
        esq.Children.Add(Aviso());
        Grid.SetColumn(esq, 0);
        grade.Children.Add(esq);

        var specs = new StackPanel();
        specs.Children.Add(new TextBlock { Text = "ESTADO DA PLATAFORMA (SOMENTE LEITURA)", Style = SectionStyle, Margin = new Thickness(0, 0, 0, 6) });
        specs.Children.Add(Spec("BIOS / UEFI", Val("BIOS/UEFI"), null));
        specs.Children.Add(Spec("Placa-mãe", Val("Placa-mãe"), null));
        specs.Children.Add(SpecEstado("Secure Boot", SegurancaSistema.SecureBootAtivo(), "Ativo", "Desativado", "Indeterminado"));
        specs.Children.Add(SpecEstado("TPM", SegurancaSistema.TpmHabilitado(), "Habilitado", "Desabilitado", "Ausente"));
        var cardSpecs = new Border { Style = CardStyle, Child = specs, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(cardSpecs, 2);
        grade.Children.Add(cardSpecs);

        Raiz.Children.Add(grade);
    }

    // Etapa com selo numerado "01".."04" à esquerda (adaptado do original).
    static UIElement Passo(int n, string titulo, string corpo, bool risco)
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var num = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
            Background = Surface2, BorderBrush = Borda, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = n.ToString("00"), Foreground = Secondary, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, FontSize = 13 },
        };
        g.Children.Add(num);

        var txt = new StackPanel();
        var topo = new StackPanel { Orientation = Orientation.Horizontal };
        topo.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (risco)
        {
            var b = new Border { Background = new SolidColorBrush(Color.FromArgb(40, 245, 184, 65)), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            b.Child = new TextBlock { Text = "atenção", Foreground = Warning, FontSize = 10, FontWeight = FontWeights.SemiBold };
            topo.Children.Add(b);
        }
        txt.Children.Add(topo);
        txt.Children.Add(new TextBlock { Text = corpo, Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(txt, 1);
        g.Children.Add(txt);
        return g;
    }

    // Linha de spec com valor alinhado à direita (card de specs).
    static UIElement Spec(string rotulo, string valor, Brush? cor)
    {
        var g = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = rotulo, Foreground = Muted, FontSize = 13, VerticalAlignment = VerticalAlignment.Top });
        var v = new TextBlock { Text = valor, Foreground = cor ?? Primary, FontWeight = FontWeights.SemiBold, FontSize = 13, TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }

    static UIElement SpecEstado(string rotulo, bool? estado, string sim, string nao, string indef)
    {
        var (txt, cor) = estado switch { true => (sim, Success), false => (nao, Warning), _ => (indef, Muted) };
        return Spec(rotulo, txt, cor);
    }

    static UIElement Aviso()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Salvaguardas", FontWeight = FontWeights.SemiBold, Foreground = Success });
        sp.Children.Add(new TextBlock { Text = "O Sevenzin não executa flash de BIOS, não muda Secure Boot/TPM/boot/voltagem/clock e não eleva privilégio de forma oculta. Opções variam por hardware; na dúvida, consulte o manual da sua placa.", Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        return new Border { Style = CardStyle, Child = sp };
    }
}
