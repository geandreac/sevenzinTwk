using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Itens que entraram pelo estudo dos materiais (docs/estudo-otimizacao-materiais.md).</summary>
public class EstudoMateriaisTests
{
    static Tweak T(string id) => CatalogoTweaks.Todos.Single(t => t.Id == id);

    [Theory]
    [InlineData("game-mode-on", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, false)]
    [InlineData("power-throttling-off", @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, true)]
    [InlineData("apps-segundo-plano-off", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, false)]
    public void Itens_novos_tem_valor_risco_e_admin_coerentes(string id, string caminho, string valor, int otimizado, bool admin)
    {
        var t = T(id);
        Assert.Equal((caminho, valor, otimizado), (t.Caminho, t.NomeValor, t.ValorOtimizado));
        Assert.Equal(admin, t.ExigeAdmin);
        Assert.Equal(t.ExigeAdmin, t.Colmeia == Colmeia.MaquinaLocal);
        Assert.False(t.ExigeConfirmacao);
        Assert.False(string.IsNullOrWhiteSpace(t.Risco));
        // sem backup, desativar volta ao padrão do Windows = apagar o valor
        Assert.Null(MotorTweaks.PadraoDesativadoWindows(t).Dword);
    }

    [Fact]
    public void Prontidao_inclui_modo_de_jogo_como_necessario_e_throttling_como_recomendado()
    {
        Assert.Contains(ProntidaoJogos.TweaksDoCatalogo, x => x.Id == "game-mode-on" && x.Nivel == NivelProntidao.Necessario);
        Assert.Contains(ProntidaoJogos.TweaksDoCatalogo, x => x.Id == "power-throttling-off" && x.Nivel == NivelProntidao.Recomendado);
    }

    [Fact]
    public void Cache_grande_avisa_que_e_configuracao_de_servidor() =>
        Assert.Contains("servidores", T("large-cache-on").Risco);
}
