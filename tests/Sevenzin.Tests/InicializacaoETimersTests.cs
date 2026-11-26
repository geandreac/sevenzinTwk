using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class InicializacaoTests
{
    [Fact]
    public void Formato_do_StartupApproved_igual_ao_Gerenciador_de_Tarefas()
    {
        Assert.True(Inicializacao.EstaHabilitado(null));                       // sem valor = ligado
        Assert.True(Inicializacao.EstaHabilitado(new byte[] { 0x02, 0, 0, 0 }));
        Assert.True(Inicializacao.EstaHabilitado(new byte[] { 0x06, 0, 0, 0 }));
        Assert.False(Inicializacao.EstaHabilitado(new byte[] { 0x03, 0, 0, 0 }));
        Assert.False(Inicializacao.EstaHabilitado(new byte[] { 0x07, 0, 0, 0 }));

        var quando = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        var off = Inicializacao.ValorDesabilitado(quando);
        Assert.Equal(12, off.Length);
        Assert.False(Inicializacao.EstaHabilitado(off));
        Assert.Equal(quando, DateTime.FromFileTimeUtc(BitConverter.ToInt64(off, 4)));
        Assert.True(Inicializacao.EstaHabilitado(Inicializacao.ValorHabilitado()));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Steam\\steam.exe\" -silent", "steam.exe")]
    [InlineData("C:\\Apps\\Discord\\Update.exe --processStart Discord.exe", "Update.exe")]
    [InlineData("C:\\Users\\a\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\OneNote.lnk", "OneNote.lnk")]
    public void Mostra_o_executavel_do_comando(string comando, string esperado) =>
        Assert.Equal(esperado, Inicializacao.Executavel(comando));

    [Fact]
    public void Escopo_de_maquina_exige_admin()
    {
        Assert.False(new ItemInicializacao("a", "x", OrigemInicializacao.RunUsuario, true).ExigeAdmin);
        Assert.True(new ItemInicializacao("a", "x", OrigemInicializacao.RunMaquina32, true).ExigeAdmin);
        Assert.True(new ItemInicializacao("a", "x", OrigemInicializacao.PastaComum, true).ExigeAdmin);
    }
}

public class TempoBootTests
{
    const string Ev =
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><EventID>100</EventID>" +
        "<TimeCreated SystemTime='{0}'/></System><EventData><Data Name='BootTime'>{1}</Data>" +
        "<Data Name='MainPathBootTime'>{2}</Data><Data Name='BootPostBootTime'>{3}</Data></EventData></Event>";

    [Fact]
    public void Le_os_tempos_por_nome_e_ordena_do_mais_recente()
    {
        var xml = string.Format(Ev, "2026-11-20T10:00:00.000Z", 41000, 21000, 20000) + string.Format(Ev, "2026-11-25T09:00:00.000Z", 30000, 18000, 12000);
        var m = TempoBoot.Parse(xml);
        Assert.Equal(2, m.Count);
        Assert.Equal(30000, m[0].TotalMs);
        Assert.Equal((18000, 12000), (m[0].NucleoMs, m[0].PosLoginMs));
        Assert.Empty(TempoBoot.Parse(""));
    }
}

public class TimersBootTests
{
    const string Saida = """
        Carregador de Inicialização do Windows
        --------------------------------
        identificador           {current}
        description             honestOS
        nx                      OptIn
        tscsyncpolicy           Legacy
        hypervisorlaunchtype    Auto
        useplatformclock        No
        disabledynamictick      Yes
        """;

    [Fact]
    public void So_os_timers_conhecidos_e_presentes_estao_fora_do_padrao()
    {
        var bcd = TimersBoot.Parse(Saida);
        Assert.Equal(3, bcd.Count); // nx, description e hypervisor não são timers
        var alt = TimersBoot.Alterados(bcd);
        Assert.Equal(new[] { "useplatformclock", "disabledynamictick", "tscsyncpolicy" }.OrderBy(x => x), alt.Select(a => a.Chave).OrderBy(x => x));
        Assert.Equal("Legacy", alt.Single(a => a.Chave == "tscsyncpolicy").Valor);
        Assert.Empty(TimersBoot.Alterados(TimersBoot.Parse("description  Windows 11\nnx  OptIn")));
    }
}
