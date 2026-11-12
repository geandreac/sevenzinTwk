using System.Text.Json;
using System.Text.Json.Serialization;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Timer de alta resolução: máquina de estados com delegates falsos (nunca chama a API real).
/// Garante a regra de segurança: a resolução sobe UMA vez na 1ª referência e baixa na última.</summary>
public class TimerAltaResolucaoTests
{
    [Fact]
    public void Iniciar_sobe_uma_vez_e_parar_baixa_na_ultima_referencia()
    {
        int begins = 0, ends = 0;
        using var t = new TimerAltaResolucao(begin: _ => { begins++; return 0; }, end: _ => { ends++; return 0; });

        t.Iniciar(); t.Iniciar(); t.Iniciar();               // 3 jogos usando
        Assert.True(t.Ativo);
        Assert.Equal(3, t.Referencias);
        Assert.Equal(1, begins);                             // timeBeginPeriod chamado só na 1ª

        t.Parar(); t.Parar();
        Assert.True(t.Ativo);
        Assert.Equal(0, ends);                               // ainda há referência

        t.Parar();
        Assert.False(t.Ativo);
        Assert.Equal(1, ends);                               // timeEndPeriod só na última
    }

    [Fact]
    public void Parar_extra_nao_chama_end_e_dispose_restaura()
    {
        int begins = 0, ends = 0;
        var t = new TimerAltaResolucao(begin: _ => { begins++; return 0; }, end: _ => { ends++; return 0; });

        t.Parar();                                           // sem referência: ignorado
        Assert.Equal(0, ends);

        t.Iniciar(); t.Iniciar();
        t.Dispose();                                         // encerramento abrupto do app
        Assert.False(t.Ativo);
        Assert.Equal(1, begins);
        Assert.Equal(1, ends);                               // sistema restaurado
    }

    [Fact]
    public void Periodo_e_limitado_ao_intervalo_do_windows()
    {
        int begins = 0; uint pedido = 0;
        using var t = new TimerAltaResolucao(periodoMs: 99, begin: p => { pedido = p; begins++; return 0; }, end: _ => 0);

        t.Iniciar();
        Assert.InRange(pedido, 1u, 16u);                    // timeBeginPeriod só aceita 1..16
        Assert.Equal(1, begins);
    }

    [Fact]
    public void PerfilJogo_serializa_os_novos_campos_e_tolera_json_antigo()
    {
        var opts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        var novo = new PerfilJogo("Teste", @"C:\jogo.exe", 0, PrioridadeJogo.Alta, TrimWorkingSet: true, TimerAltaResolucao: true);
        var json = JsonSerializer.Serialize(novo, opts);
        var volta = JsonSerializer.Deserialize<PerfilJogo>(json, opts);
        Assert.NotNull(volta);
        Assert.True(volta!.TrimWorkingSet);
        Assert.True(volta.TimerAltaResolucao);

        // jogos.json gravado antes destes campos continua abrindo com os opt-ins desligados (padrão seguro)
        var antigo = """{"Nome":"Antigo","Executavel":"C:\\old.exe","MascaraAfinidade":0,"Prioridade":"Normal"}""";
        var lido = JsonSerializer.Deserialize<PerfilJogo>(antigo, opts);
        Assert.NotNull(lido);
        Assert.False(lido!.TrimWorkingSet);
        Assert.False(lido.TimerAltaResolucao);
    }
}
