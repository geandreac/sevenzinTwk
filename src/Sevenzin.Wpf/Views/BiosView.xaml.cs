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

        Raiz.Children.Add(new TextBlock { Text = "ESTADO (SOMENTE LEITURA)", Style = SectionStyle });
        var linhas = new StackPanel();
        linhas.Children.Add(Linha("BIOS / UEFI", Val("BIOS/UEFI")));
        linhas.Children.Add(Linha("Placa-mãe", Val("Placa-mãe")));
        linhas.Children.Add(LinhaEstado("Secure Boot", SegurancaSistema.SecureBootAtivo(), "ativo", "desativado", "indeterminado (BIOS legada/sem UEFI)"));
        linhas.Children.Add(LinhaEstado("TPM", SegurancaSistema.TpmHabilitado(), "habilitado", "presente, desabilitado", "ausente/indeterminado"));
        Raiz.Children.Add(new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = linhas });

        Raiz.Children.Add(new TextBlock { Text = "INSTRUÇÕES (AÇÕES MANUAIS DO USUÁRIO — VARIAM POR FABRICANTE)", Style = SectionStyle });
        Raiz.Children.Add(Instrucoes());

        Raiz.Children.Add(Aviso());
    }

    static UIElement Instrucoes()
    {
        var sp = new StackPanel();
        sp.Children.Add(Passo("Entrar no setup da BIOS/UEFI", "Reinicie e pressione a tecla do fabricante no POST (comum: Del, F2, F10 ou F12). A tecla aparece na tela inicial. · informativo", false));
        sp.Children.Add(Passo("Ativar perfil de memória (XMP / EXPO / DOCP)", "No setup, procure o perfil de memória e selecione o do seu kit. Melhora a frequência da RAM. ⚠ Potencialmente disruptivo: pode não dar boot se o kit/placa não suportar — anote os valores antes.", true));
        sp.Children.Add(Passo("Verificar ordem de boot e Secure Boot", "Confira no setup; mantenha o Secure Boot ligado por segurança. ⚠ Alto risco desligar — só com motivo claro.", true));
        sp.Children.Add(Passo("Atualizar a BIOS", "Baixe a versão SÓ do site oficial do fabricante da placa e siga o procedimento dele. ⚠ Alto risco: queda de energia durante o flash pode inutilizar a placa. Use no-break se possível.", true));
        return new Border { Style = CardStyle, Margin = new Thickness(0, 0, 0, 24), Child = sp };
    }

    static UIElement Passo(string titulo, string corpo, bool risco)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        var topo = new StackPanel { Orientation = Orientation.Horizontal };
        topo.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold });
        if (risco)
        {
            var b = new Border { Background = new SolidColorBrush(Color.FromArgb(40, 245, 184, 65)), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            b.Child = new TextBlock { Text = "atenção", Foreground = Warning, FontSize = 10, FontWeight = FontWeights.SemiBold };
            topo.Children.Add(b);
        }
        sp.Children.Add(topo);
        sp.Children.Add(new TextBlock { Text = corpo, Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        return sp;
    }

    static UIElement Aviso()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Salvaguardas", FontWeight = FontWeights.SemiBold, Foreground = Success });
        sp.Children.Add(new TextBlock { Text = "O Sevenzin não executa flash de BIOS, não muda Secure Boot/TPM/boot/voltagem/clock e não eleva privilégio de forma oculta. Opções variam por hardware; na dúvida, consulte o manual da sua placa.", Foreground = Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        return new Border { Style = CardStyle, Child = sp };
    }

    static UIElement Linha(string rotulo, string valor)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var r = new TextBlock { Text = rotulo, Foreground = Muted };
        var v = new TextBlock { Text = valor, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(v, 1);
        g.Children.Add(r); g.Children.Add(v);
        return g;
    }

    static UIElement LinhaEstado(string rotulo, bool? estado, string sim, string nao, string indef)
    {
        var (txt, cor) = estado switch { true => (sim, Success), false => (nao, Warning), _ => (indef, Muted) };
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = rotulo, Foreground = Muted });
        var v = new TextBlock { Text = txt, Foreground = cor, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }
}
