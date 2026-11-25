namespace Sevenzin.Diagnostics;

/// <summary>Retângulo em pixels de tela (esquerda, topo, direita, baixo).</summary>
public readonly record struct Retangulo(int Esq, int Topo, int Dir, int Baixo);

/// <summary>Área que uma janela maximizada pode ocupar num monitor.</summary>
public static class AreaMaximizada
{
    /// <summary>A área de trabalho, menos 2 px em cada borda do monitor onde há barra de tarefas auto-oculta: com
    /// ela, o Windows informa a área de trabalho como o monitor inteiro e a janela cobriria a barra para sempre.</summary>
    public static Retangulo Calcular(Retangulo monitor, Retangulo trabalho, (bool Esq, bool Topo, bool Dir, bool Baixo) autoOculta)
    {
        const int folga = 2;
        return new Retangulo(
            trabalho.Esq + (autoOculta.Esq && trabalho.Esq == monitor.Esq ? folga : 0),
            trabalho.Topo + (autoOculta.Topo && trabalho.Topo == monitor.Topo ? folga : 0),
            trabalho.Dir - (autoOculta.Dir && trabalho.Dir == monitor.Dir ? folga : 0),
            trabalho.Baixo - (autoOculta.Baixo && trabalho.Baixo == monitor.Baixo ? folga : 0));
    }
}
