using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F7.1: parsers puros da saída do DISM (Get-WimInfo, Get-AppxProvisionedPackages,
/// Get-WindowsOptionalFeature) com amostras no formato real da ferramenta.</summary>
public class WindowsMediaTests
{
    [Fact]
    public void WimInfo_parseia_edicoes_com_indice_nome_tamanho()
    {
        const string saida = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1150

        Details for image : E:\sources\install.wim

        Index : 1
        Name : Windows 11 Home
        Description : Windows 11 Home
        Size : 16.536.445.386 bytes

        Index : 2
        Name : Windows 11 Pro
        Description : Windows 11 Pro
        Size : 16.789.012.345 bytes

        The operation completed successfully.
        """;
        var edicoes = AnalisadorImagem.AnalisarSaidaWimInfo(saida);

        Assert.Equal(2, edicoes.Count);
        Assert.Equal(1, edicoes[0].Indice);
        Assert.Equal("Windows 11 Home", edicoes[0].Nome);
        Assert.Equal(16536445386, edicoes[0].TamanhoBytes);
        Assert.Equal("Windows 11 Pro", edicoes[1].Nome);
    }

    [Fact]
    public void WimInfo_ignora_linhas_soltas_e_devolve_vazio_sem_edicoes()
    {
        Assert.Empty(AnalisadorImagem.AnalisarSaidaWimInfo("erro do dism\nsem blocos de índice"));
        Assert.Empty(AnalisadorImagem.AnalisarSaidaWimInfo(""));
    }

    [Fact]
    public void Appx_parseia_os_pacotes_provisionados()
    {
        const string saida = """
        DisplayName : Microsoft.StorePurchaseApp
        Version : 1.0.0.0
        PackageName : Microsoft.StorePurchaseApp_12002.1.0.0_neutral_split.scale-100_8wekyb3d8bbwe

        DisplayName : Microsoft.WindowsStore
        PackageName : Microsoft.WindowsStore_12002.1.0.0_neutral_8wekyb3d8bbwe
        """;
        var apps = AnalisadorImagem.AnalisarSaidaAppx(saida);

        Assert.Equal(2, apps.Count);
        Assert.Contains(apps, a => a.Pacote.Contains("Microsoft.StorePurchaseApp"));
        Assert.Contains(apps, a => a.Pacote.Contains("Microsoft.WindowsStore"));
    }

    [Fact]
    public void Features_parseia_pares_nome_estado()
    {
        const string saida = """
        Feature Name : NetFx3
        State : Disabled

        Feature Name : Microsoft-Windows-Subsystem-Linux
        State : Enabled
        """;
        var features = AnalisadorImagem.AnalisarSaidaFeatures(saida);

        Assert.Equal(2, features.Count);
        Assert.Equal("NetFx3", features[0].Nome);
        Assert.Equal("Disabled", features[0].Estado);
        Assert.Equal("Enabled", features[1].Estado);
    }

    [Fact]
    public void ValidarIso_aceita_apenas_arquivo_iso_existente()
    {
        Assert.Equal("Escolha um arquivo .iso.", ExecutorDism.ValidarIso(null));
        Assert.Equal("O arquivo precisa ter extensão .iso.", ExecutorDism.ValidarIso(@"C:\x\arquivo.zip"));
        Assert.Equal("Arquivo não encontrado.", ExecutorDism.ValidarIso(@"C:\x\nao-existe.iso"));

        var iso = Path.Combine(Path.GetTempPath(), $"sevenzin-{Guid.NewGuid():N}.iso");
        try
        {
            File.WriteAllText(iso, "fake");
            Assert.Null(ExecutorDism.ValidarIso(iso)); // válido
        }
        finally { File.Delete(iso); }
    }
}
