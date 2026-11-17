using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Rede global (TCP): parser puro da saída do netsh (EN/PT) e valores literais do original.</summary>
public class RedeGlobalTests
{
    [Fact]
    public void AnalisarSaida_parseia_em_ingles()
    {
        const string saida = """
        Receive-Side Scaling State          : enabled
        Receive Window Auto-Tuning Level    : normal
        Congestion Control Provider         : default
        """;
        var (at, cong) = RedeGlobal.AnalisarSaida(saida);
        Assert.Equal("normal", at);
        Assert.Equal("default", cong);
    }

    [Fact]
    public void AnalisarSaida_parseia_em_portugues()
    {
        const string saida = """
        Nível de Ajuste Automático da Janela de Recebimento : restricted
        Provedor de Controle de Congestionamento            : ctcp
        """;
        var (at, cong) = RedeGlobal.AnalisarSaida(saida);
        Assert.Equal("restricted", at);
        Assert.Equal("ctcp", cong);
    }

    [Fact]
    public void AnalisarSaida_devolve_null_quando_nao_identifica()
    {
        var (at, cong) = RedeGlobal.AnalisarSaida("erro ao consultar o netsh");
        Assert.Null(at);
        Assert.Null(cong);
    }
}
