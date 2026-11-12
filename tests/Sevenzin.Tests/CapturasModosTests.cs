using System.Drawing;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Modos de captura (F2.1): enumeração real de janelas/monitores/área virtual e desenho de
/// anotações (F2.2). Capturas de pixel exigem tela; os testes cobrem enumeração consistente e o
/// render do editor sobre um bitmap sintético.</summary>
public class CapturasModosTests
{
    [Fact]
    public void Area_tela_inteira_e_positiva_e_monitores_tem_limites_validos()
    {
        var area = CapturaTela.AreaTelaInteira();
        Assert.True(area.Width > 0 && area.Height > 0);

        var monitores = CapturaTela.Monitores();
        Assert.NotEmpty(monitores);
        Assert.All(monitores, m => Assert.True(m.Width > 0 && m.Height > 0));
    }

    [Fact]
    public void Janelas_visiveis_nao_lancam_e_cada_entrada_tem_titulo()
    {
        var janelas = CapturaTela.JanelasVisiveis();
        Assert.All(janelas, j => Assert.False(string.IsNullOrWhiteSpace(j.Titulo)));
        // Janelas com título igual são deduplicadas na UI; aqui só garantimos integridade.
        Assert.Equal(janelas.Select(j => j.Hwnd).Distinct().Count(), janelas.Count);
    }
}

public class EditorCapturaTests
{
    static Bitmap Base(int tam = 100)
    {
        var b = new Bitmap(tam, tam);
        using var g = Graphics.FromImage(b);
        g.Clear(Color.Black);
        return b;
    }

    [Fact]
    public void Renderizar_desenha_retangulo_vermelho_e_nao_altera_a_base()
    {
        using var baseImg = Base();
        var anotacao = new Anotacao(TipoAnotacao.Retangulo, new() { new Point(10, 10), new Point(50, 50) }, Color.Red, 3f, null);

        using var saida = EditorCaptura.Renderizar(baseImg, new[] { anotacao });

        Assert.Equal(Color.Red.ToArgb(), saida.GetPixel(10, 10).ToArgb());   // borda desenhada
        Assert.Equal(Color.Red.ToArgb(), saida.GetPixel(50, 50).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), saida.GetPixel(80, 80).ToArgb()); // fora do traço
        Assert.Equal(Color.Black.ToArgb(), baseImg.GetPixel(10, 10).ToArgb()); // base intacta
    }

    [Fact]
    public void Renderizar_lapis_seta_e_texto_nao_lancam()
    {
        using var baseImg = Base();
        var anotacoes = new[]
        {
            new Anotacao(TipoAnotacao.Lapis, new() { new Point(5, 5), new Point(20, 30) }, Color.Yellow, 2f, null),
            new Anotacao(TipoAnotacao.Seta, new() { new Point(5, 60), new Point(90, 60) }, Color.Cyan, 3f, null),
            new Anotacao(TipoAnotacao.Texto, new() { new Point(20, 20) }, Color.White, 2f, "A"),
            new Anotacao(TipoAnotacao.Lapis, new() { new Point(70, 70) }, Color.White, 2f, null), // ponto único
        };
        using var saida = EditorCaptura.Renderizar(baseImg, anotacoes);
        Assert.True(saida.GetPixel(90, 60).ToArgb() != Color.Black.ToArgb()); // ponta da seta visível
    }

    [Fact]
    public void Pilha_anotacoes_desfaz_na_ordem_inversa_e_limpa()
    {
        var pilha = new PilhaAnotacoes();
        Assert.False(pilha.Desfazer()); // vazia: nada a desfazer

        pilha.Adicionar(new(TipoAnotacao.Lapis, new(), Color.Red, 2f, null));
        pilha.Adicionar(new(TipoAnotacao.Texto, new(), Color.White, 2f, "x"));
        Assert.Equal(2, pilha.Contagem);

        Assert.True(pilha.Desfazer());
        Assert.Single(pilha.Itens);
        Assert.Equal(TipoAnotacao.Lapis, pilha.Itens[0].Tipo);

        pilha.Limpar();
        Assert.Empty(pilha.Itens);
    }
}
