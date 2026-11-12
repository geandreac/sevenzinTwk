using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sevenzin.Diagnostics;

/// <summary>Tipo de anotação do editor de capturas (paridade 04_funcionalidades §9/06_fluxos F11:
/// pencil, rectangle, annotation-arrow, text, undo — os mesmos recursos do original).</summary>
public enum TipoAnotacao { Lapis, Retangulo, Seta, Texto }

/// <summary>Uma anotação: tipo, pontos (no sistema da imagem), cor, espessura e texto (só para Texto).</summary>
public sealed record Anotacao(TipoAnotacao Tipo, List<Point> Pontos, Color Cor, float Espessura, string? Texto);

/// <summary>Editor de anotações sobre a captura. Renderiza a lista de anotações em CIMA da imagem original
/// (nunca destrói o original: cada render parte da base). Desfazer = remover a última anotação.</summary>
public static class EditorCaptura
{
    /// <summary>Renderiza a base + anotações numa cópia nova. Função pura em relação à base (testável).</summary>
    public static Bitmap Renderizar(Bitmap baseImagem, IEnumerable<Anotacao> anotacoes)
    {
        var saida = new Bitmap(baseImagem);
        using var g = Graphics.FromImage(saida);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var a in anotacoes) Desenhar(g, a);
        return saida;
    }

    /// <summary>Desenha uma anotação sobre um Graphics. Função pura (testável por pixels).</summary>
    public static void Desenhar(Graphics g, Anotacao a)
    {
        using var caneta = new Pen(a.Cor, Math.Max(1f, a.Espessura)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var pincel = new SolidBrush(a.Cor);
        var pts = a.Pontos;
        if (pts.Count == 0) return;

        switch (a.Tipo)
        {
            case TipoAnotacao.Lapis when pts.Count == 1:
                g.DrawRectangle(caneta, pts[0].X, pts[0].Y, 1, 1);
                break;
            case TipoAnotacao.Lapis:
                g.DrawLines(caneta, pts.ToArray());
                break;
            case TipoAnotacao.Retangulo:
                var r = Limites(pts);
                g.DrawRectangle(caneta, r);
                break;
            case TipoAnotacao.Seta:
                DesenharSeta(g, caneta, pts[0], pts[^1]);
                break;
            case TipoAnotacao.Texto when !string.IsNullOrEmpty(a.Texto):
                using (var fonte = new Font("Segoe UI", Math.Max(10f, a.Espessura * 4f), FontStyle.Bold))
                    g.DrawString(a.Texto, fonte, pincel, pts[0].X, pts[0].Y);
                break;
        }
    }

    static Rectangle Limites(List<Point> pts)
    {
        int x1 = pts.Min(p => p.X), y1 = pts.Min(p => p.Y), x2 = pts.Max(p => p.X), y2 = pts.Max(p => p.Y);
        return Rectangle.FromLTRB(x1, y1, x2, y2);
    }

    static void DesenharSeta(Graphics g, Pen caneta, Point de, Point para)
    {
        if (de == para) return;
        g.DrawLine(caneta, de, para);
        const int comp = 14;
        double angulo = Math.Atan2(para.Y - de.Y, para.X - de.X);
        var p1 = new PointF((float)(para.X - comp * Math.Cos(angulo - Math.PI / 6)), (float)(para.Y - comp * Math.Sin(angulo - Math.PI / 6)));
        var p2 = new PointF((float)(para.X - comp * Math.Cos(angulo + Math.PI / 6)), (float)(para.Y - comp * Math.Sin(angulo + Math.PI / 6)));
        using var preench = new SolidBrush(caneta.Color);
        g.DrawLine(caneta, para, Point.Round(p1));
        g.DrawLine(caneta, para, Point.Round(p2));
        g.FillPolygon(preench, new[] { para, Point.Round(p1), Point.Round(p2) });
    }
}

/// <summary>Pilha de anotações com desfazer (o estado de edição da tela; serializável se preciso).</summary>
public sealed class PilhaAnotacoes
{
    readonly List<Anotacao> _itens = new();

    public IReadOnlyList<Anotacao> Itens => _itens;
    public int Contagem => _itens.Count;

    public void Adicionar(Anotacao a) => _itens.Add(a);

    /// <summary>Remove a última anotação. False se não havia nada (botão de desfazer desabilitado).</summary>
    public bool Desfazer()
    {
        if (_itens.Count == 0) return false;
        _itens.RemoveAt(_itens.Count - 1);
        return true;
    }

    public void Limpar() => _itens.Clear();
}
