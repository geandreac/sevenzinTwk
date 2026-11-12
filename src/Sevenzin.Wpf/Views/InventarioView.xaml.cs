using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

public partial class InventarioView : UserControl
{
    public InventarioView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var linhas = (await Task.Run(Relatorio.Coletar)).Select(i => new LinhaInv(i)).ToList();
            var view = new ListCollectionView(linhas);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LinhaInv.Categoria)));
            Grade.ItemsSource = view;
        };
    }
}

/// <summary>Projeção de um item para a grade, com a cor da confiança resolvida.</summary>
public sealed class LinhaInv
{
    public string Categoria { get; }
    public string Nome { get; }
    public string Valor { get; }
    public string Tipo { get; }
    public string Fonte { get; }
    public string Obs { get; }
    public Brush Cor { get; }

    public LinhaInv(ItemInventario i)
    {
        Categoria = i.Categoria;
        Nome = i.Nome;
        Valor = i.Valor ?? "—";
        Fonte = i.Fonte;
        Obs = i.Observacao ?? "";
        (Tipo, var chave) = i.Tipo switch
        {
            Confianca.Fato => ("Fato", "SuccessBrush"),
            Confianca.Inferencia => ("Inferência", "WarningBrush"),
            _ => ("Hipótese", "TextMutedBrush"),
        };
        Cor = (Brush)Application.Current.Resources[chave];
    }
}
