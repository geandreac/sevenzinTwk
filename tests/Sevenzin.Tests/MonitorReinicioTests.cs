using System;
using System.IO;
using System.Linq;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class MonitorReinicioTests
{
    // Formato real do wevtutil (eventos soltos, sem raiz), como lido deste Windows.
    const string Xml1074 =
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='User32'/><EventID Qualifiers='32768'>1074</EventID>" +
        "<TimeCreated SystemTime='2026-11-16T04:03:15.6461981Z'/></System><EventData><Data Name='param1'>C:\\WINDOWS\\Explorer.EXE (GEANDRE)</Data>" +
        "<Data Name='param2'>GEANDRE</Data><Data Name='param3'>Outro (não planejada)</Data><Data Name='param4'>0x0</Data>" +
        "<Data Name='param5'>Desligado</Data><Data Name='param6'></Data><Data Name='param7'>geandre\\geandreac</Data></EventData></Event>";
    const string Xml41 =
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Microsoft-Windows-Kernel-Power'/>" +
        "<EventID>41</EventID><TimeCreated SystemTime='2026-11-17T10:00:00.000Z'/></System><EventData><Data Name='BugcheckCode'>0</Data></EventData></Event>";
    const string XmlBsod =
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Microsoft-Windows-WER-SystemErrorReporting'/>" +
        "<EventID Qualifiers='16384'>1001</EventID><TimeCreated SystemTime='2026-11-17T10:01:00.000Z'/></System>" +
        "<EventData><Data Name='param1'>0x0000009f (0x0000000000000003, 0xffff)</Data></EventData></Event>";

    static readonly DateTime Boot = new(2026, 11, 17, 10, 2, 0, DateTimeKind.Utc);

    [Fact]
    public void ParseWevtutil_le_id_hora_provedor_e_dados()
    {
        var e = MonitorReinicio.ParseWevtutil(Xml1074 + Xml41).ToList();
        Assert.Equal(2, e.Count);
        Assert.Equal(1074, e[0].Id);
        Assert.Equal("User32", e[0].Provedor);
        Assert.Equal(new DateTime(2026, 11, 16, 4, 3, 15, DateTimeKind.Utc), e[0].Utc.AddTicks(-(e[0].Utc.Ticks % TimeSpan.TicksPerSecond)));
        Assert.Equal("Outro (não planejada)", e[0].Dados[2]);
        Assert.Empty(MonitorReinicio.ParseWevtutil(""));
    }

    [Fact]
    public void Tela_azul_tem_prioridade_e_mostra_o_codigo()
    {
        var n = MonitorReinicio.Interpretar(MonitorReinicio.ParseWevtutil(Xml1074 + Xml41 + XmlBsod), reiniciou: true, Boot);
        Assert.Equal("Tela azul (BSOD) detectada", n!.Value.Titulo);
        Assert.Contains("0x0000009f", n.Value.Detalhe);
    }

    [Fact]
    public void Desligamento_sem_encerrar_vira_inesperado() =>
        Assert.Equal("Desligamento inesperado do PC", MonitorReinicio.Interpretar(MonitorReinicio.ParseWevtutil(Xml1074 + Xml41), true, Boot)!.Value.Titulo);

    [Fact]
    public void Pedido_de_desligamento_mostra_quem_e_o_motivo()
    {
        var n = MonitorReinicio.Interpretar(MonitorReinicio.ParseWevtutil(Xml1074), true, Boot)!.Value;
        Assert.Equal("O PC foi desligado ou reiniciado", n.Titulo);
        Assert.Contains("Explorer.EXE", n.Detalhe);
        Assert.Contains("Outro (não planejada)", n.Detalhe);
        Assert.Contains("Desligado", n.Detalhe);
    }

    [Fact]
    public void Sem_eventos_e_sem_reinicio_nao_notifica() =>
        Assert.Null(MonitorReinicio.Interpretar(Array.Empty<EventoSistema>(), reiniciou: false, Boot));

    [Fact]
    public void Primeira_abertura_so_guarda_estado_e_depois_detecta_reinicio()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sevenzin_mr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new NotificacoesStore(Path.Combine(dir, "notif.json"));
            var estado = Path.Combine(dir, "estado.json");

            MonitorReinicio.VerificarENotificar(store, estado, _ => Xml1074, () => Boot.AddDays(-1));
            Assert.Empty(store.Todos); // 1ª vez: não despeja histórico

            MonitorReinicio.VerificarENotificar(store, estado, _ => "", () => Boot.AddDays(-1));
            Assert.Empty(store.Todos); // mesmo boot, nada novo

            MonitorReinicio.VerificarENotificar(store, estado, _ => "", () => DateTime.UtcNow);
            Assert.Equal("O PC foi reiniciado", store.Todos.Single().Titulo); // boot mudou
            Assert.Equal("Sistema", store.Todos.Single().Origem);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

public class NotificacoesDuplicataTests
{
    static NotificacoesStore Novo() => new(Path.Combine(Path.GetTempPath(), "sevenzin_nd_" + Guid.NewGuid().ToString("N") + ".json"));

    [Fact]
    public void Repeticao_identica_em_seguida_vira_uma_so()
    {
        var s = Novo();
        for (int i = 0; i < 5; i++) s.Adicionar("Sistema", "Falha", "mesmo motivo");
        Assert.Single(s.Todos);
    }

    [Fact]
    public void Registro_automatico_nao_duplica_erro_que_a_tela_ja_registrou()
    {
        var s = Novo();
        s.Adicionar("Instaladores", "Falha no download de 'X'", "timeout");
        s.AdicionarSeNovo("Erro", "Falha no download", "timeout");
        Assert.Single(s.Todos);
        s.AdicionarSeNovo("Erro", "Outra falha", "motivo diferente");
        Assert.Equal(2, s.Todos.Count);
    }
}
