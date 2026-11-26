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
    public void Categorias_seguem_a_ordem_por_tema_e_nenhuma_fica_fora_da_ordem()
    {
        var cats = CatalogoUtilitarios.ContagemPorCategoria().Select(c => c.Categoria).ToList();
        Assert.Equal(new[] { "Jogos e comunidade", "Desempenho e diagnóstico", "Drivers e componentes", "Aplicativos essenciais", "Sistema, criação e desenvolvimento" }, cats);
        Assert.All(cats, c => Assert.Contains(c, CatalogoUtilitarios.OrdemCategorias)); // nenhum tema "solto"
        Assert.Equal(cats, CatalogoUtilitarios.OrdemCategorias.Where(cats.Contains).ToList());
    }

    [Theory]
    [InlineData("ExitLag", "Jogos e comunidade")]
    [InlineData("vibranceGUI", "Jogos e comunidade")]
    [InlineData("Mozilla Firefox", "Aplicativos essenciais")]
    [InlineData("Process Lasso", "Desempenho e diagnóstico")]
    [InlineData("CRU (Custom Resolution Utility)", "Desempenho e diagnóstico")]
    [InlineData("MeasureSleep", "Jogos e comunidade")]
    [InlineData("WinRAR", "Aplicativos essenciais")]
    [InlineData("Geek Uninstaller", "Aplicativos essenciais")]
    [InlineData("Temp Cleaner (Sordum)", "Aplicativos essenciais")]
    [InlineData("Lightshot", "Aplicativos essenciais")]
    [InlineData("Stremio", "Sistema, criação e desenvolvimento")]
    [InlineData("AnyDesk", "Sistema, criação e desenvolvimento")]
    [InlineData("RustDesk", "Sistema, criação e desenvolvimento")]
    [InlineData("Windows Update Blocker (Sordum)", "Sistema, criação e desenvolvimento")]
    public void Apps_da_pasta_sev7nzin_estao_no_tema_certo(string nome, string categoria) =>
        Assert.Equal(categoria, CatalogoUtilitarios.Todos.Single(u => u.Nome == nome).Categoria);

    [Fact]
    public void Sem_assinatura_ou_de_risco_so_abrem_a_pagina_oficial()
    {
        var soPagina = CatalogoUtilitarios.Todos.Where(u => u.SoPagina is not null).Select(u => u.Nome).ToHashSet();
        Assert.Equal(new[] { "CRU (Custom Resolution Utility)", "ExitLag", "MeasureSleep", "Windows Update Blocker (Sordum)", "vibranceGUI" }.ToHashSet(), soPagina);
        Assert.All(CatalogoUtilitarios.Todos.Where(u => u.SoPagina is not null), u => Assert.False(string.IsNullOrWhiteSpace(u.SoPagina)));
    }

    [Fact]
    public void Busca_ignora_acentos_e_maiusculas_exige_todas_as_palavras_e_respeita_categoria()
    {
        var itens = new[]
        {
            new Utilitario("Discord", "Chat de voz", "https://x", null, "Discord", null, "Comunicação"),
            new Utilitario("Steam", "Plataforma de jogos", "https://x", null, "Valve", null, "Plataformas de jogos"),
        };
        Assert.Equal(new[] { "Discord" }, CatalogoUtilitarios.Filtrar(itens, "COMUNICACAO", null).Select(u => u.Nome));
        Assert.Equal(new[] { "Steam" }, CatalogoUtilitarios.Filtrar(itens, "valve jogos", null).Select(u => u.Nome));
        Assert.Empty(CatalogoUtilitarios.Filtrar(itens, "valve chat", null));
        Assert.Equal(2, CatalogoUtilitarios.Filtrar(itens, "   ", null).Count);
        Assert.Empty(CatalogoUtilitarios.Filtrar(itens, "discord", "Plataformas de jogos"));
    }
}
