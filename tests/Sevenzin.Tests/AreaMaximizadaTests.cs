using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class AreaMaximizadaTests
{
    static readonly Retangulo Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Barra_fixa_embaixo_fica_fora_da_janela()
    {
        var trabalho = new Retangulo(0, 0, 1920, 1032); // barra de 48 px embaixo
        Assert.Equal(trabalho, AreaMaximizada.Calcular(Monitor, trabalho, (false, false, false, false)));
    }

    [Fact]
    public void Barra_auto_oculta_ganha_2px_livres_so_na_borda_dela()
    {
        // auto-oculta: o Windows devolve o monitor inteiro como área de trabalho
        Assert.Equal(new Retangulo(0, 0, 1920, 1078), AreaMaximizada.Calcular(Monitor, Monitor, (false, false, false, true)));
        Assert.Equal(new Retangulo(2, 0, 1920, 1080), AreaMaximizada.Calcular(Monitor, Monitor, (true, false, false, false)));
    }

    [Fact]
    public void Segundo_monitor_e_barra_em_cima_respeitam_as_coordenadas_dele()
    {
        var m2 = new Retangulo(1920, -200, 3840, 880);
        var trabalho = new Retangulo(1920, -152, 3840, 880); // barra de 48 px em cima
        Assert.Equal(trabalho, AreaMaximizada.Calcular(m2, trabalho, (false, false, false, false)));
        Assert.Equal(new Retangulo(1920, -152, 3840, 878), AreaMaximizada.Calcular(m2, trabalho, (false, false, false, true)));
    }
}
