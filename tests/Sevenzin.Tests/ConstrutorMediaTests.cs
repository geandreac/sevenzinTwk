using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F7.2: parser do Get-Disk, filtro SafeUsb (réplica do original), cópia verificada e
/// validação do pedido de construção de ISO — tudo puro/isolado, sem tocar em disco real.</summary>
public class ConstrutorMediaTests : IDisposable
{
    readonly string _raiz = Path.Combine(Path.GetTempPath(), "sevenzin-media-" + Guid.NewGuid().ToString("N"));

    public ConstrutorMediaTests() => Directory.CreateDirectory(_raiz);

    public void Dispose() { try { Directory.Delete(_raiz, recursive: true); } catch { } }

    static UsbRecord Disco(bool usb = true, bool boot = false, bool sistema = false, bool offline = false,
        bool readonly_ = false, long tamanho = 64L * 1024 * 1024 * 1024, string uniqueId = "ID-123") =>
        new(1, "Pendrive 64 GB", "SERIAL123", tamanho, uniqueId, usb ? "USB" : "SATA", boot, sistema, offline, readonly_);

    [Fact]
    public void AnalisarSaidaGetDisk_parseia_as_linhas_do_powershell()
    {
        const string saida = "4|Pendrive 64 GB|SERIAL|68719476736|ID-1|USB|False|False|False|False\n2|SSD|X|1000204886016|ID-2|NVMe|True|True|False|False\nlixo\n";
        var discos = UsbMedia.AnalisarSaidaGetDisk(saida);
        Assert.Equal(2, discos.Count);
        Assert.Equal(4, discos[0].Number);
        Assert.Equal("USB", discos[0].BusType);
        Assert.Equal(68719476736, discos[0].Tamanho);
        Assert.True(discos[1].IsBoot);
    }

    [Fact]
    public void Seguro_replica_o_safeusb_do_original()
    {
        Assert.True(UsbMedia.Seguro(Disco()));                       // USB 64 GB ok
        Assert.False(UsbMedia.Seguro(Disco(usb: false)));            // não USB
        Assert.False(UsbMedia.Seguro(Disco(boot: true)));            // disco de boot
        Assert.False(UsbMedia.Seguro(Disco(sistema: true)));         // disco de sistema
        Assert.False(UsbMedia.Seguro(Disco(offline: true)));         // offline
        Assert.False(UsbMedia.Seguro(Disco(readonly_: true)));       // somente leitura
        Assert.False(UsbMedia.Seguro(Disco(tamanho: 4L * 1024 * 1024 * 1024))); // < 8 GB
        Assert.False(UsbMedia.Seguro(Disco(uniqueId: "")));          // sem identificação estável
    }

    [Fact]
    public async Task CopiarVerificado_copia_o_conteudo_e_confere_o_hash()
    {
        var origem = Path.Combine(_raiz, "origem.bin");
        var destino = Path.Combine(_raiz, "destino.bin");
        var bytes = new byte[3 * 1024 * 1024 + 7]; // atravessa blocos de 1 MB
        new Random(42).NextBytes(bytes);
        await File.WriteAllBytesAsync(origem, bytes);

        var copiado = await CopiadorVerificado.CopiarAsync(origem, destino);

        Assert.Equal(bytes.Length, copiado);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destino));
    }

    [Fact]
    public void ValidarPedido_da_construcao_de_iso_cobre_os_casos_de_entrada()
    {
        Assert.Equal("Escolha um arquivo .iso.", ConstrutorIso.ValidarPedido(null, @"C:\x\saida.iso", 1));
        Assert.Equal("Escolha uma edição para exportar.", ConstrutorIso.ValidarPedido(@"C:\x\origem.iso", @"C:\x\saida.iso", 0));
        Assert.Equal("O destino precisa ter extensão .iso.", ConstrutorIso.ValidarPedido(@"C:\x\origem.iso", @"C:\x\saida.7z", 1));

        var origem = Path.Combine(_raiz, "origem.iso");
        var destino = Path.Combine(_raiz, "saida.iso");
        File.WriteAllText(origem, "fake");
        Assert.Null(ConstrutorIso.ValidarPedido(origem, destino, 2)); // válido

        File.WriteAllText(destino, "já existe");
        Assert.Equal("Já existe um arquivo no destino — escolha um nome novo (nada é sobrescrito).", ConstrutorIso.ValidarPedido(origem, destino, 2));
    }
}
