using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class InstaladoresTests
{
    const string HashAbc = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"; // sha256("abc")

    static string ArqTemp(string conteudo)
    {
        var p = Path.Combine(Path.GetTempPath(), "sevenzin-inst-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllText(p, conteudo);
        return p;
    }

    [Fact]
    public void Sha256_calcula_o_hash_do_arquivo()
    {
        var arq = ArqTemp("abc");
        try { Assert.Equal(HashAbc, VerificadorDownload.Sha256(arq), ignoreCase: true); }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Verificar_ok_quando_hash_confere_e_assinatura_bate()
    {
        var arq = ArqTemp("abc");
        try
        {
            var u = new Utilitario("X", "d", "https://exemplo.com", "https://exemplo.com/x.exe", "CPUID", HashAbc);
            var r = VerificadorDownload.Verificar(arq, u, _ => "CN=CPUID, O=CPUID");
            Assert.Equal(ResultadoVerificacao.Ok, r);
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Verificar_rejeita_hash_diferente()
    {
        var arq = ArqTemp("conteudo diferente");
        try
        {
            var u = new Utilitario("X", "d", "https://exemplo.com", null, "CPUID", HashAbc);
            Assert.Equal(ResultadoVerificacao.HashNaoConfere, VerificadorDownload.Verificar(arq, u, _ => "CN=CPUID"));
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Verificar_rejeita_sem_assinatura()
    {
        var arq = ArqTemp("abc");
        try
        {
            var u = new Utilitario("X", "d", "https://exemplo.com", null, "CPUID", HashAbc);
            Assert.Equal(ResultadoVerificacao.SemAssinatura, VerificadorDownload.Verificar(arq, u, _ => null));
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Verificar_rejeita_publisher_diferente()
    {
        var arq = ArqTemp("abc");
        try
        {
            var u = new Utilitario("X", "d", "https://exemplo.com", null, "CPUID", HashAbc);
            Assert.Equal(ResultadoVerificacao.PublisherDiferente, VerificadorDownload.Verificar(arq, u, _ => "CN=Outra Empresa"));
        }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void AssinaturaPublisher_e_nula_para_arquivo_nao_assinado()
    {
        var arq = ArqTemp("não sou um PE assinado");
        try { Assert.Null(VerificadorDownload.AssinaturaPublisher(arq)); }
        finally { File.Delete(arq); }
    }

    [Fact]
    public void Catalogo_so_tem_urls_https_e_oficiais()
    {
        Assert.NotEmpty(CatalogoUtilitarios.Todos);
        Assert.All(CatalogoUtilitarios.Todos, u =>
        {
            Assert.StartsWith("https://", u.UrlOficial);
            if (u.UrlDownload is { } d) Assert.StartsWith("https://", d);
            Assert.False(string.IsNullOrEmpty(u.Nome));
        });
    }
}
