using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class GpuUsoTests
{
    [Fact]
    public void Soma_apenas_as_engines_3D()
    {
        var amostras = new (string, float)[]
        {
            ("pid_100_luid_0_eng_0_engtype_3D", 30f),
            ("pid_200_luid_0_eng_0_engtype_3D", 20f),
            ("pid_300_luid_0_eng_1_engtype_VideoDecode", 50f), // ignorado
            ("pid_400_luid_0_eng_2_engtype_Copy", 40f),        // ignorado
        };
        Assert.Equal(50d, LeitorGpuUso.AgregarUso3D(amostras));
    }

    [Fact]
    public void Limita_a_100_quando_a_soma_passa()
    {
        var amostras = new (string, float)[]
        {
            ("a_engtype_3D", 80f),
            ("b_engtype_3D", 60f),
        };
        Assert.Equal(100d, LeitorGpuUso.AgregarUso3D(amostras));
    }

    [Fact]
    public void Sem_engines_3D_resulta_em_zero()
        => Assert.Equal(0d, LeitorGpuUso.AgregarUso3D(new (string, float)[] { ("x_engtype_Copy", 70f) }));
}
