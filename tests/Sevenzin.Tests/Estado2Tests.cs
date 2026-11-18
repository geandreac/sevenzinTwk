using System.Diagnostics;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Estado 2 — motores avançados: parsers e validadores puros (sem tocar hardware/registro).</summary>
public class Estado2Tests
{
    [Fact]
    public void GpuOverclock_parseia_csv_nvidia()
    {
        const string linha = "NVIDIA GeForce RTX 4060 Ti, 615, 3105, 405, 9001, 43.19, 28.19, 160.00, 160.00, 16, 1234, 16384";
        var t = GpuOverclock.AnalisarCsv(linha);
        Assert.NotNull(t);
        Assert.Equal("NVIDIA GeForce RTX 4060 Ti", t!.Nome);
        Assert.Equal(615, t.ClockMHz);
        Assert.Equal(3105, t.ClockMaxMHz);
        Assert.Equal(43.19, t.TempC, 3);
        Assert.Equal(160.0, t.PowerLimitW, 2);
        Assert.Equal(160.0, t.PowerMaxW, 2);
        Assert.Equal(16, t.UsoPct, 2);
        Assert.Equal(1234, t.MemUsadaMB);
        Assert.Equal(16384, t.MemTotalMB);
    }

    [Fact]
    public void GpuOverclock_csv_invalido_retorna_null()
    {
        Assert.Null(GpuOverclock.AnalisarCsv(""));
        Assert.Null(GpuOverclock.AnalisarCsv("a,b,c"));
        Assert.Null(GpuOverclock.AnalisarCsv("x, n, n, n, n, n, n, n, n, n, n, n")); // não numérico
    }

    [Fact]
    public void Licenciamento_parseia_dlv_portugues()
    {
        const string saida = """
            Versão do Serviço de Licenciamento de Software: 10.0.22621.1
            Nome: Windows(R), Professional edition
            Descrição: Windows(R) Operating System, RETAIL channel
            Status da Licença: Licenciado
            Canal do Produto: Retail
            Nome do Computador KMS: kms.empresa.local
            """;
        var e = Licenciamento.AnalisarDlv(saida);
        Assert.Equal("Licenciado", e.Estado);
        Assert.Equal("Retail", e.Canal);
        Assert.Equal("kms.empresa.local", e.KmsHost);
    }

    [Fact]
    public void Licenciamento_dlv_sem_kms_devolve_null()
    {
        const string saida = "Status da Licença: Licenciado\nCanal do Produto: OEM:DM\n";
        var e = Licenciamento.AnalisarDlv(saida);
        Assert.Equal("Licenciado", e.Estado);
        Assert.Equal("OEM:DM", e.Canal);
        Assert.Null(e.KmsHost);
    }

    [Fact]
    public void Dpc_classifica_carga()
    {
        Assert.Equal("baixa (normal)", DpcDiagnostico.Classificar(new DpcMetrica(1, 1)));
        Assert.Equal("moderada — observe drivers em uso intenso", DpcDiagnostico.Classificar(new DpcMetrica(8, 2)));
        Assert.Equal("alta — algum driver pode estar monopolizando o processador", DpcDiagnostico.Classificar(new DpcMetrica(15, 5)));
    }

    [Fact]
    public void Prioridade_mapeia_classes()
    {
        Assert.Equal(ProcessPriorityClass.RealTime, PrioridadeProcesso.Mapear(NivelPrioridade.TempoReal));
        Assert.Equal(ProcessPriorityClass.High, PrioridadeProcesso.Mapear(NivelPrioridade.Alto));
        Assert.Equal(ProcessPriorityClass.Idle, PrioridadeProcesso.Mapear(NivelPrioridade.Ocioso));
        Assert.Equal(NivelPrioridade.TempoReal, PrioridadeProcesso.Demapear(ProcessPriorityClass.RealTime));
    }

    [Fact]
    public void Prioridade_bloqueia_tempo_real_em_critico()
    {
        Assert.True(PrioridadeProcesso.EhCritico("csrss"));
        Assert.True(PrioridadeProcesso.EhCritico("svchost.exe"));
        Assert.True(PrioridadeProcesso.EhCritico("System"));
        Assert.False(PrioridadeProcesso.EhCritico("game.exe"));

        Assert.NotNull(PrioridadeProcesso.Validar(NivelPrioridade.TempoReal, "csrss"));
        Assert.Null(PrioridadeProcesso.Validar(NivelPrioridade.TempoReal, "game.exe"));
        Assert.Null(PrioridadeProcesso.Validar(NivelPrioridade.Alto, "csrss"));
    }
}
