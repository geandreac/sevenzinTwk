using System;
using System.Net.Http;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class NvidiaPainelControleTests
{
    [Fact]
    public void Processo_que_imprime_muito_nao_trava()
    {
        // ~200 KB na saída: com a saída redirecionada e não lida, isto travava para sempre (extração do 7-Zip).
        var t = System.Threading.Tasks.Task.Run(() =>
            NvidiaDebloat.RodarProcesso("cmd.exe", "/c for /L %i in (1,1,3000) do @echo linha de progresso do extrator %i", default));
        Assert.True(t.Wait(TimeSpan.FromSeconds(60)), "o processo travou");
        Assert.Equal(0, t.Result);
    }

    [Fact]
    public void Prefere_o_classico_depois_o_da_loja_e_null_sem_nenhum()
    {
        Assert.EndsWith("nvcplui.exe", Sevenzin.Diagnostics.NvidiaDebloat.ComandoPainelControle(true, true)!.Value.Exe);
        var loja = Sevenzin.Diagnostics.NvidiaDebloat.ComandoPainelControle(false, true)!.Value;
        Assert.Equal("explorer.exe", loja.Exe);
        Assert.StartsWith(@"shell:AppsFolder\NVIDIACorp.NVIDIAControlPanel_", loja.Args);
        Assert.Null(Sevenzin.Diagnostics.NvidiaDebloat.ComandoPainelControle(false, false));
    }
}


public class NvidiaRedeTests
{
    static HttpRequestException SemNome() => new(HttpRequestError.NameResolutionError, "Este host não é reconhecido. (www.nvidia.com:443)");

    [Fact]
    public void Nome_nao_resolvido_vira_mensagem_de_DNS()
    {
        var msg = NvidiaDebloat.MensagemFalhaRede(SemNome(), conteudoHosts: "");
        Assert.NotNull(msg);
        Assert.Contains("DNS", msg);
        Assert.Contains("Já tenho o instalador", msg);
    }

    [Fact]
    public void Erro_embrulhado_tambem_e_reconhecido() =>
        Assert.NotNull(NvidiaDebloat.MensagemFalhaRede(new InvalidOperationException("falhou", SemNome()), null));

    [Fact]
    public void Sem_conexao_vira_mensagem_de_conexao() =>
        Assert.Contains("conectar", NvidiaDebloat.MensagemFalhaRede(new HttpRequestException(HttpRequestError.ConnectionError), null));

    [Fact]
    public void Hosts_bloqueando_nvidia_e_apontado_como_causa()
    {
        var hosts = "127.0.0.1 localhost\r\n0.0.0.0 www.nvidia.com\r\n";
        Assert.Contains("arquivo hosts", NvidiaDebloat.MensagemFalhaRede(SemNome(), hosts));
    }

    [Fact]
    public void Linha_comentada_no_hosts_nao_conta() =>
        Assert.DoesNotContain("arquivo hosts", NvidiaDebloat.MensagemFalhaRede(SemNome(), "# 0.0.0.0 www.nvidia.com"));

    [Fact]
    public void Erro_que_nao_e_de_rede_mantem_a_mensagem_original() =>
        Assert.Null(NvidiaDebloat.MensagemFalhaRede(new InvalidOperationException("outra coisa"), null));
}
