using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class PontoRestauracaoTests
{
    static readonly DateTime Agora = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
    static EstadoProtecao Ok(PontoRestauracaoInfo? ultimo = null, int freq = 1440) =>
        new(Admin: true, ProtecaoAtiva: true, BloqueadoPorPolitica: false, VssDesativado: false, LivreBytes: 50L << 30, ultimo, freq);
    static PontoRestauracaoInfo P(uint seq, DateTime utc) => new(seq, "x", utc);

    [Fact]
    public void Tudo_certo_permite_criar()
    {
        Assert.Equal(MotivoPonto.Nenhum, PontoRestauracao.Avaliar(Ok(), Agora));
        Assert.Equal(MotivoPonto.Nenhum, PontoRestauracao.Avaliar(Ok(P(5, Agora.AddHours(-25))), Agora));
    }

    [Fact]
    public void Cada_impedimento_tem_seu_motivo_na_ordem_de_gravidade()
    {
        Assert.Equal(MotivoPonto.SemAdmin, PontoRestauracao.Avaliar(Ok() with { Admin = false, ProtecaoAtiva = false }, Agora));
        Assert.Equal(MotivoPonto.BloqueadoPorPolitica, PontoRestauracao.Avaliar(Ok() with { BloqueadoPorPolitica = true }, Agora));
        Assert.Equal(MotivoPonto.ProtecaoDesligada, PontoRestauracao.Avaliar(Ok() with { ProtecaoAtiva = false }, Agora));
        Assert.Equal(MotivoPonto.VssDesativado, PontoRestauracao.Avaliar(Ok() with { VssDesativado = true }, Agora));
        Assert.Equal(MotivoPonto.PoucoEspaco, PontoRestauracao.Avaliar(Ok() with { LivreBytes = 10L << 20 }, Agora));
        Assert.Equal(MotivoPonto.Nenhum, PontoRestauracao.Avaliar(Ok() with { ProtecaoAtiva = null, LivreBytes = null }, Agora)); // não lido ≠ impedimento
    }

    [Fact]
    public void Limite_de_24h_avisa_antes_e_frequencia_zero_libera()
    {
        var recente = P(7, Agora.AddHours(-3));
        Assert.Equal(MotivoPonto.LimiteDeFrequencia, PontoRestauracao.Avaliar(Ok(recente), Agora));
        Assert.Equal(MotivoPonto.Nenhum, PontoRestauracao.Avaliar(Ok(recente, freq: 0), Agora));
        Assert.Contains("24 h", PontoRestauracao.Explicar(MotivoPonto.LimiteDeFrequencia, Ok(recente)));
    }

    [Fact]
    public void So_confirma_ponto_novo_de_verdade()
    {
        var antes = new[] { P(10, Agora.AddDays(-2)), P(11, Agora.AddDays(-1)) };
        Assert.Null(PontoRestauracao.Confirmar(antes, antes, Agora));                            // nada novo
        Assert.Null(PontoRestauracao.Confirmar(antes, antes.Append(P(12, Agora.AddHours(-5))).ToList(), Agora)); // novo, mas antigo
        var novo = PontoRestauracao.Confirmar(antes, antes.Append(P(12, Agora.AddSeconds(40))).ToList(), Agora);
        Assert.Equal(12u, novo!.Sequencia);
        Assert.Equal(1u, PontoRestauracao.Confirmar(Array.Empty<PontoRestauracaoInfo>(), new[] { P(1, Agora) }, Agora)!.Sequencia); // primeiro ponto do PC
    }
}
