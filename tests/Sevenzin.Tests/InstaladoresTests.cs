using System.Net;
using System.Net.Http;
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

    [Fact]
    public void Catalogo_tem_downloads_estaveis_apenas_onde_a_url_oficial_e_documentada()
    {
        // URLs "latest" documentadas pelos fornecedores; os demais itens ficam sem download direto.
        var chrome = CatalogoUtilitarios.Todos.Single(u => u.Nome == "Google Chrome");
        Assert.Equal("https://dl.google.com/chrome/install/latest/chrome_installer.exe", chrome.UrlDownload);
        var brave = CatalogoUtilitarios.Todos.Single(u => u.Nome == "Brave Browser");
        Assert.Equal("https://laptop-updates.brave.com/latest/winx64", brave.UrlDownload);
        var vscode = CatalogoUtilitarios.Todos.Single(u => u.Nome == "Visual Studio Code");
        Assert.StartsWith("https://code.visualstudio.com/sha/download?", vscode.UrlDownload);
        // Nomes únicos (sem duplicatas no catálogo).
        var nomes = CatalogoUtilitarios.Todos.Select(u => u.Nome).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    sealed class FakeDownloadHandler : HttpMessageHandler
    {
        readonly byte[] _conteudo;
        public FakeDownloadHandler(byte[] conteudo) => _conteudo = conteudo;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_conteudo),
            };
            resp.Content.Headers.ContentLength = _conteudo.Length;
            return Task.FromResult(resp);
        }
    }

    [Fact]
    public async Task BaixarAsync_usa_staging_e_move_atomicamente_no_sucesso()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "sevenzin-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var destino = Path.Combine(pasta, "arquivo.bin");
        try
        {
            var bytes = new byte[200_000];
            new Random(7).NextBytes(bytes);
            await DownloaderInstalador.BaixarAsync("https://exemplo.com/x", destino, handler: new FakeDownloadHandler(bytes));

            Assert.True(File.Exists(destino));
            Assert.False(File.Exists(destino + ".part")); // staging removido após o move atômico
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destino));
        }
        finally { try { Directory.Delete(pasta, recursive: true); } catch { } }
    }

    [Fact]
    public async Task BaixarAsync_em_falha_nao_corrompe_o_destino_existente()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "sevenzin-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var destino = Path.Combine(pasta, "arquivo.bin");
        await File.WriteAllTextAsync(destino, "original intacto");
        try
        {
            var handler = new FakeDownloadHandler(new byte[] { 1, 2, 3 })
            {
                // falha no meio: um handler que lança ao ler o corpo
            };
            await Assert.ThrowsAnyAsync<Exception>(() =>
                DownloaderInstalador.BaixarAsync("https://exemplo.com/x", destino, handler: new FailingDownloadHandler()));

            Assert.Equal("original intacto", await File.ReadAllTextAsync(destino)); // destino preservado
        }
        finally { try { Directory.Delete(pasta, recursive: true); } catch { } }
    }

    sealed class FailingDownloadHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new FailingContent(),
            };
            resp.Content.Headers.ContentLength = 1000;
            return await Task.FromResult(resp);
        }

        sealed class FailingContent : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new IOException("conexão caiu");
            protected override bool TryComputeLength(out long length) { length = 1000; return true; }
        }
    }

    [Fact]
    public void EspacoSuficiente_respeita_o_drive_informado()
    {
        Assert.True(DownloaderInstalador.EspacoSuficiente(Path.GetTempPath(), 1024));          // pouco espaço: cabe
        Assert.False(DownloaderInstalador.EspacoSuficiente(Path.GetTempPath(), long.MaxValue)); // impossível: não cabe
    }
}
