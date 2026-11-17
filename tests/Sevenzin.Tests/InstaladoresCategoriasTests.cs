using System.Linq;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class InstaladoresCategoriasTests
{
    [Fact]
    public void Soma_das_categorias_cobre_todo_o_catalogo()
    {
        var total = CatalogoUtilitarios.ContagemPorCategoria().Sum(c => c.Qtd);
        Assert.Equal(CatalogoUtilitarios.Todos.Count, total);
    }

    [Fact]
    public void Todo_utilitario_tem_categoria_nao_vazia()
    {
        Assert.All(CatalogoUtilitarios.Todos, u => Assert.False(string.IsNullOrWhiteSpace(u.Categoria)));
    }

    [Fact]
    public void Ordem_segue_a_do_original_com_sistema_primeiro()
    {
        var cats = CatalogoUtilitarios.ContagemPorCategoria().Select(c => c.Categoria).ToList();
        Assert.Equal("Sistema", cats[0]); // 1ª categoria na ordem de exibição
        foreach (var esperada in new[] { "Sistema", "Drivers", "Plataformas", "Jogos", "Monitoramento" })
            Assert.Contains(esperada, cats);
    }
}
