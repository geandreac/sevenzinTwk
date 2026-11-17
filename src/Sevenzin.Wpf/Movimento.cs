using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sevenzin.Wpf;

/// <summary>Movimento do app num lugar só: mesmas curvas e tempos em todas as telas. Cada movimento tem causa
/// (abrir tela, abrir/fechar modal, trocar etapa) — nada se mexe sozinho. Entrada: fade + subida curta com curva
/// de "chegada" (0.25,0.8,0.25,1), ~200ms. Respeita "Mostrar animações no Windows" (ClientAreaAnimation):
/// desligado, tudo aparece no estado final, sem movimento.</summary>
static class Movimento
{
    public static bool Ativo => SystemParameters.ClientAreaAnimation;

    static readonly KeySpline Chegada = Congelar(new KeySpline(0.25, 0.8, 0.25, 1));
    static KeySpline Congelar(KeySpline k) { k.Freeze(); return k; }

    static DoubleAnimationUsingKeyFrames Anim(double de, double para, int ms, int atrasoMs = 0)
    {
        var a = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(atrasoMs) };
        a.KeyFrames.Add(new DiscreteDoubleKeyFrame(de, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(para, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), Chegada));
        return a;
    }

    /// <summary>Entrada de conteúdo (tela, etapa, painel): fade + sobe <paramref name="subida"/> px.</summary>
    public static void Entrar(UIElement el, double subida = 8, int ms = 200, int atrasoMs = 0)
    {
        if (!Ativo) { el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 1; el.RenderTransform = null; return; }
        var tt = new TranslateTransform(0, subida);
        el.RenderTransform = tt;
        el.Opacity = 0; // fica invisível durante o atraso (stagger)
        el.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, ms, atrasoMs));
        tt.BeginAnimation(TranslateTransform.YProperty, Anim(subida, 0, ms + 60, atrasoMs));
    }

    /// <summary>Abre um modal: véu em fade e painel crescendo de 96% (sem "pulo" de tamanho).</summary>
    public static void AbrirModal(UIElement veu, UIElement painel)
    {
        if (!Ativo) return;
        veu.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 160));
        painel.RenderTransformOrigin = new Point(0.5, 0.5);
        var st = new ScaleTransform(0.96, 0.96);
        painel.RenderTransform = st;
        painel.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 180));
        st.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.96, 1, 220));
        st.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.96, 1, 220));
    }

    /// <summary>Saída curta (fade 120ms) e depois <paramref name="aoTerminar"/>. Não recebe cliques enquanto some.</summary>
    public static void Sair(UIElement el, Action aoTerminar, int ms = 120)
    {
        if (!Ativo) { aoTerminar(); return; }
        el.IsHitTestVisible = false;
        var a = Anim(el.Opacity, 0, ms);
        a.Completed += (_, _) => aoTerminar();
        el.BeginAnimation(UIElement.OpacityProperty, a);
    }
}
