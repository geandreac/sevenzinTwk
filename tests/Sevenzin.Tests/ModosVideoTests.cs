using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Parser puro do DEVMODE e consolidação de modos. A parte real da API
/// é coberta por testes de consistência na máquina (como TopologiaCpuTests).</summary>
public class ModosVideoTests
{
    static byte[] BufferDevmode(uint largura, uint altura, uint hz, uint bpp, ushort tamanho = 220)
    {
        var b = new byte[256];
        BitConverter.GetBytes(tamanho).CopyTo(b, 68);   // dmSize
        BitConverter.GetBytes(bpp).CopyTo(b, 168);      // dmBitsPerPel
        BitConverter.GetBytes(largura).CopyTo(b, 172);  // dmPelsWidth
        BitConverter.GetBytes(altura).CopyTo(b, 176);   // dmPelsHeight
        BitConverter.GetBytes(hz).CopyTo(b, 184);       // dmDisplayFrequency
        return b;
    }

    [Fact]
    public void AnalisarDevmode_le_os_campos_de_display()
    {
        var m = LeitorModosVideo.AnalisarDevmode(BufferDevmode(1920, 1080, 144, 32));

        Assert.NotNull(m);
        Assert.Equal(1920u, m!.Value.Largura);
        Assert.Equal(1080u, m.Value.Altura);
        Assert.Equal(144u, m.Value.Hz);
        Assert.Equal(32u, m.Value.BitsPerPel);
    }

    [Fact]
    public void AnalisarDevmode_recusa_buffer_curto_e_tamanho_incorreto()
    {
        Assert.Null(LeitorModosVideo.AnalisarDevmode(new byte[16]));       // buffer pequeno demais
        Assert.Null(LeitorModosVideo.AnalisarDevmode(BufferDevmode(1, 1, 60, 32, tamanho: 64))); // dmSize inconsistente
    }

    [Fact]
    public void AnalisarDevmode_recusa_modo_invalido()
    {
        Assert.Null(LeitorModosVideo.AnalisarDevmode(BufferDevmode(0, 1080, 144, 32))); // 0×N
        Assert.Null(LeitorModosVideo.AnalisarDevmode(BufferDevmode(1920, 0, 144, 32))); // N×0
        Assert.Null(LeitorModosVideo.AnalisarDevmode(BufferDevmode(1920, 1080, 0, 32))); // 0 Hz
        Assert.Null(LeitorModosVideo.AnalisarDevmode(BufferDevmode(1920, 1080, 144, 0))); // 0 bpp
    }

    [Fact]
    public void Consolidar_remove_duplicatas_e_ordena_por_resolucao_depois_hz()
    {
        var modos = new[]
        {
            new ModoVideo(1920, 1080, 60, 32, 0),
            new ModoVideo(1280, 720, 120, 32, 0),
            new ModoVideo(1920, 1080, 144, 32, 0),
            new ModoVideo(1920, 1080, 60, 32, 0), // duplicado
            new ModoVideo(3840, 2160, 60, 32, 0),
        };

        var r = LeitorModosVideo.Consolidar(modos);

        Assert.Equal(4, r.Count);
        Assert.Equal(new ModoVideo(3840, 2160, 60, 32, 0), r[0]);
        Assert.Equal(new ModoVideo(1920, 1080, 144, 32, 0), r[1]);
        Assert.Equal(new ModoVideo(1920, 1080, 60, 32, 0), r[2]);
        Assert.Equal(new ModoVideo(1280, 720, 120, 32, 0), r[3]);
    }

    [Fact]
    public void Listar_e_Atual_sao_consistentes_em_maquina_real()
    {
        var modos = LeitorModosVideo.Listar();
        Assert.NotEmpty(modos); // display primário sempre enumera algo no Windows

        var atual = LeitorModosVideo.Atual();
        Assert.NotNull(atual);
        Assert.Contains(modos, m => m == atual!.Value); // o modo em uso está entre os suportados

        // nenhuma linha com valor inventado: todos os campos obrigatórios preenchidos
        Assert.All(modos, m =>
        {
            Assert.True(m.Largura > 0 && m.Altura > 0 && m.Hz > 0 && m.BitsPerPel > 0);
        });
    }
}
