using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class NvidiaModoImagemTests
{
    [Fact]
    public void Todo_perfil_deixa_o_painel_em_configuracoes_avancadas_3d()
    {
        Assert.NotEmpty(GpuDriverTweaks.PerfisGraficos);
        foreach (var perfil in GpuDriverTweaks.PerfisGraficos)
        {
            var p = Assert.Single(NvidiaDrs.ObterParametrosPerfil(perfil.Id), x => x.SettingId == NvidiaDrs.IdModoImagemPainel);
            Assert.Equal(0u, p.Valor); // 0 = "Use as configurações de imagens avançadas em 3D"
        }
    }

    [Fact]
    public void Remover_o_perfil_tambem_devolve_o_modo_da_pagina_de_visualizacao() =>
        Assert.Contains(NvidiaDrs.IdModoImagemPainel, NvidiaDrs.TodosSettingIdsGerenciados);
}
