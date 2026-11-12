using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F4: guias (conteúdo só de recursos implementados) e notificações (histórico local).</summary>
public class GuiasENotificacoesTests
{
    [Fact]
    public void Guias_tem_titulos_unicos_e_passos_nao_vazios()
    {
        Assert.NotEmpty(CatalogoGuias.Todos);
        var titulos = CatalogoGuias.Todos.Select(g => g.Titulo).ToList();
        Assert.Equal(titulos.Count, titulos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(CatalogoGuias.Todos, g =>
        {
            Assert.False(string.IsNullOrWhiteSpace(g.Resumo));
            Assert.NotEmpty(g.Passos);
            Assert.All(g.Passos, p => Assert.False(string.IsNullOrWhiteSpace(p)));
        });
    }

    [Fact]
    public void Notificacoes_persistem_ordenam_e_limpam()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"sevenzin-notif-{Guid.NewGuid():N}.json");
        try
        {
            var store = new NotificacoesStore(caminho);
            Assert.Empty(store.Todos);

            store.Adicionar("Ajustes", "Aplicado", "x");
            store.Adicionar("Capturas", "Salvo", "y");

            var recarregada = new NotificacoesStore(caminho);
            Assert.Equal(2, recarregada.Todos.Count);
            Assert.Equal("Salvo", recarregada.Todos[0].Titulo); // mais recente primeiro

            recarregada.Limpar();
            Assert.Empty(new NotificacoesStore(caminho).Todos);
        }
        finally
        {
            try { File.Delete(caminho); File.Delete(caminho + ".tmp"); } catch { }
        }
    }

    [Fact]
    public void Catalogo_tem_tweaks_de_mouse_como_string_e_de_localizacao_confirmados()
    {
        var mouse = CatalogoTweaks.Todos.Where(t => t.Categoria == "Mouse").ToList();
        Assert.Equal(3, mouse.Count);
        Assert.All(mouse, t => Assert.Equal(TipoValor.String, t.Tipo)); // o original gravava REG_SZ em Control Panel\Mouse
        Assert.All(mouse, t => Assert.Equal(Colmeia.UsuarioAtual, t.Colmeia));
        Assert.All(mouse, t => Assert.Equal(@"Control Panel\Mouse", t.Caminho));

        var loc = CatalogoTweaks.Todos.Where(t => t.Categoria == "Localização").ToList();
        Assert.Equal(2, loc.Count);
        Assert.Contains(loc, t => t.Id == "localizacao-ocultar" && t.NomeValor == "HideSCALocation");
        Assert.Contains(loc, t => t.Id == "localizacao-servico-off" && t.NomeValor == "Start" && t.ValorOtimizado == 4);
    }
}
