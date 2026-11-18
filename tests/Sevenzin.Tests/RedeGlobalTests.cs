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

    [Fact]
    public void AnalisarSaida_parseia_supplemental_portugues()
    {
        const string saida = "Provedor de controle de congestionamento : ctcp";
        var (_, cong) = RedeGlobal.AnalisarSaida(saida);
        Assert.Equal("ctcp", cong);
    }

    [Fact]
    public void AnalisarSaida_ignora_janela_de_congestionamento_inicial()
    {
        // Saída real do Windows 11 (PT e EN): a linha da janela inicial vem ANTES do provedor.
        const string pt = """
        RTO mínimo (msec)                  : 300
        Janela de congestionamento inicial (MSS)     : 10
        Provedor de controle de congestionamento         : ctcp
        """;
        const string en = """
        Minimum RTO (msec)                    : 300
        Initial Congestion Window (MSS)       : 10
        Congestion Control Provider           : ctcp
        """;
        Assert.Equal("ctcp", RedeGlobal.AnalisarSaida(pt).Congestionamento);
        Assert.Equal("ctcp", RedeGlobal.AnalisarSaida(en).Congestionamento);
    }

    [Fact]
    public void AnalisarSaida_reconhece_congestao_legada_global()
    {
        const string saida = "Provedor de Controle de Congestão de Complementos : default";
        var (_, cong) = RedeGlobal.AnalisarSaida(saida);
        Assert.Equal("default", cong);
    }
}
