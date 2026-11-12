using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F9: fidelidade dos novos itens do catálogo aos VALORES LITERAIS do original
/// (docs/otimizacoes-originais/01_registro_setvalues.txt).</summary>
public class CatalogoOriginalTests
{
    static Tweak T(string id) => CatalogoTweaks.Todos.First(t => t.Id == id);

    [Fact]
    public void Mmcss_games_reproduz_os_valores_literais_do_original()
    {
        Assert.Equal(8, T("mmcss-games-gpu").ValorOtimizado);
        Assert.Equal("GPU Priority", T("mmcss-games-gpu").NomeValor);

        Assert.Equal(2, T("mmcss-games-prio").ValorOtimizado);

        Assert.Equal(TipoValor.String, T("mmcss-games-sfio").Tipo);
        Assert.Equal("Normal", T("mmcss-games-sfio").ValorTexto);

        Assert.Equal(TipoValor.String, T("mmcss-games-sched").Tipo);
        Assert.Equal("Medium", T("mmcss-games-sched").ValorTexto);
    }

    [Fact]
    public void Valores_de_gpu_rede_memoria_e_visual_batem_com_o_original()
    {
        Assert.Equal(8, T("tdr-delay").ValorOtimizado);
        Assert.Equal(1, T("amd-pstate-off").ValorOtimizado);
        Assert.Equal(0, T("amd-chill-off").ValorOtimizado);
        Assert.Equal(10, T("rede-throttle-off").ValorOtimizado);
        Assert.Equal(0, T("rm-throttle-off").ValorOtimizado);
        Assert.Equal(1, T("paginacao-off").ValorOtimizado);
        Assert.Equal(1, T("large-cache-on").ValorOtimizado);

        Assert.Equal(TipoValor.String, T("menu-delay-0").Tipo);
        Assert.Equal("0", T("menu-delay-0").ValorTexto);
        Assert.Equal(TipoValor.String, T("arrastar-janela-off").Tipo);
        Assert.Equal("0", T("arrastar-janela-off").ValorTexto);
        Assert.Equal(0, T("transparencia-off").ValorOtimizado);
        Assert.Equal(0, T("aero-peek-off").ValorOtimizado);
        Assert.Equal(0, T("notificacoes-off").ValorOtimizado);
    }

    [Fact]
    public void Itens_de_maquina_e_de_usuario_estao_na_colmeia_correta()
    {
        // HKLM (admin) para GPU/rede/memória; HKCU (sem admin) para visual/cosmético.
        foreach (var id in new[] { "tdr-delay", "amd-pstate-off", "rede-throttle-off", "rm-throttle-off", "paginacao-off", "large-cache-on" })
        {
            Assert.Equal(Colmeia.MaquinaLocal, T(id).Colmeia);
            Assert.True(T(id).ExigeAdmin);
        }
        foreach (var id in new[] { "amd-chill-off", "transparencia-off", "taskbar-transparencia-off", "aero-peek-off", "arrastar-janela-off", "menu-delay-0", "notificacoes-off", "copilot-off" })
        {
            Assert.Equal(Colmeia.UsuarioAtual, T(id).Colmeia);
            Assert.False(T(id).ExigeAdmin);
        }
    }
}
