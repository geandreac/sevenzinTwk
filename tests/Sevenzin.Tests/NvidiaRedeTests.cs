using System;
using System.Net.Http;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

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
