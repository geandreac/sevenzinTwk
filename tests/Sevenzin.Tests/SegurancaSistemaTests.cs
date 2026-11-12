using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Estado de segurança da plataforma. Os leitores dependem do registro da máquina, então os testes
/// cobrem a função pura de interpretação e a consistência dos leitores reais (sem exceção, tipo correto).</summary>
public class SegurancaSistemaTests
{
    [Fact]
    public void Interpretar_mapeia_dword_ausente_zero_e_um()
    {
        Assert.Null(SegurancaSistema.Interpretar(null, 1));   // chave ausente → indeterminado
        Assert.True(SegurancaSistema.Interpretar(1, 1));      // valor ativo
        Assert.False(SegurancaSistema.Interpretar(0, 1));     // valor inativo
    }

    [Fact]
    public void Leitores_de_seguranca_nao_lancam_e_sao_deterministicos()
    {
        // Em qualquer máquina Windows os leitores respondem sem exceção; duas leituras
        // consecutivas do mesmo estado têm de coincidir (leitura pura, sem efeito colateral).
        Consistente(SegurancaSistema.UacHabilitado);
        Consistente(SegurancaSistema.DefenderTempoRealAtivo);
        Consistente(SegurancaSistema.SmartScreenAtivo);
        Consistente(SegurancaSistema.HvciAtivo);
        Consistente(SegurancaSistema.MitigacoesAtivas);
        Consistente(SegurancaSistema.AtualizacoesAutomaticasAtivas);
        Consistente(SegurancaSistema.SecureBootAtivo);
        Consistente(SegurancaSistema.TpmHabilitado);
    }

    static void Consistente(Func<bool?> leitor) => Assert.Equal(leitor(), leitor());

    [Fact]
    public void Cada_mecanismo_da_tela_protecao_tem_tweak_correspondente_no_catalogo()
    {
        var ids = CatalogoTweaks.Todos.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in new[] { "uac-off", "defender-rt-off", "defender-antispyware-off", "smartscreen-off", "hvci-off", "mitigacoes-off", "wu-pausar" })
            Assert.Contains(id, ids); // a ProtecaoView resolve o tweak por Id — nunca pode quebrar o vínculo
    }
}
