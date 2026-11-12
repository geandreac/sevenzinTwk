using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class AuditoriaTests
{
    [Fact]
    public void Ler_interpreta_as_linhas_do_log_de_tweaks()
    {
        var arq = Path.Combine(Path.GetTempPath(), "sevenzin-aud-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(arq,
            "2026-11-11T18:30:00.0000000Z\taplicar\tgamedvr-off\t1->0\n" +
            "2026-11-11T18:31:00.0000000Z\tdesfazer\tgamedvr-off\t0->1\n" +
            "\n" + // linha vazia ignorada
            "linha-invalida\n");
        try
        {
            var eventos = LeitorAuditoria.Ler(arq);
            Assert.Equal(2, eventos.Count);
            Assert.Equal("aplicar", eventos[0].Acao);
            Assert.Equal("gamedvr-off", eventos[0].Tweak);
            Assert.Equal("1", eventos[0].De);
            Assert.Equal("0", eventos[0].Para);
            Assert.Equal("desfazer", eventos[1].Acao);
            Assert.Equal(2026, eventos[0].Utc.Year);
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Ler_retorna_vazio_quando_nao_ha_arquivo()
    {
        var inexistente = Path.Combine(Path.GetTempPath(), "sevenzin-aud-nao-existe-" + Guid.NewGuid().ToString("N") + ".log");
        Assert.Empty(LeitorAuditoria.Ler(inexistente));
    }
}
