using System.Text.Json;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class RelatorioTests
{
    static List<ItemInventario> Amostra() => new()
    {
        new() { Categoria = "CPU", Nome = "Modelo", Valor = "Ryzen 7 5800X", Fonte = "WMI", Tipo = Confianca.Fato },
        new() { Categoria = "CPU", Nome = "SMT", Valor = "ativo", Fonte = "derivado", Tipo = Confianca.Inferencia },
        new() { Categoria = "GPU", Nome = "VRAM", Valor = null, Fonte = "WMI", Tipo = Confianca.Hipotese, Observacao = "satura acima de 4 GB" },
    };

    [Fact]
    public void Markdown_tem_colunas_grupos_e_marcas()
    {
        var md = Relatorio.Markdown(Amostra());
        Assert.Contains("Confiança", md);
        Assert.Contains("Fato", md);
        Assert.Contains("Inferência", md);
        Assert.Contains("Hipótese", md);
        Assert.Contains("## CPU", md);
        Assert.Contains("## GPU", md);
        Assert.Contains("| — |", md);               // valor nulo vira travessão
        Assert.Contains("somente leitura", md);     // deixa claro que não altera nada
    }

    [Fact]
    public void Json_valido_com_enum_textual_e_flag_somente_leitura()
    {
        var json = Relatorio.Json(Amostra());
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("somenteLeitura").GetBoolean());
        var itens = doc.RootElement.GetProperty("itens");
        Assert.Equal(3, itens.GetArrayLength());
        Assert.Equal("Inferencia", itens[1].GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, itens[2].GetProperty("valor").ValueKind);
    }

    [Fact]
    public void Coletar_retorna_itens_reais_sem_identificadores_pessoais()
    {
        var itens = Relatorio.Coletar();
        Assert.NotEmpty(itens);
        Assert.All(itens, i =>
        {
            Assert.False(string.IsNullOrEmpty(i.Categoria));
            Assert.False(string.IsNullOrEmpty(i.Nome));
            Assert.False(string.IsNullOrEmpty(i.Fonte));
        });
        var texto = string.Join("\n", itens.Select(i => $"{i.Valor} {i.Observacao}"));
        foreach (var id in new[] { Environment.MachineName, Environment.UserName, Environment.UserDomainName }.Where(s => s.Length > 2))
            Assert.DoesNotContain(id, texto, StringComparison.OrdinalIgnoreCase);
    }
}
