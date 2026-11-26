using System.Linq;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class IdentidadeDispositivoTests
{
    static ComponenteHw[] Pc(string guid = "4c4c4544-0032-3810-8052-b4c04f4b4d32", string uuid = "03000200-0400-0500-0006-000700080009",
        string placa = "PM1A23B456", string cpu = "BFEBFBFF000906EA") => new[]
    {
        new ComponenteHw("machineguid", guid), new ComponenteHw("uuid", uuid),
        new ComponenteHw("placa", placa), new ComponenteHw("cpu", cpu),
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("Default string")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("0")]
    public void Valores_vazios_ou_genericos_de_fabrica_nao_identificam_o_pc(string? valor) =>
        Assert.Null(IdentidadeDispositivo.Normalizar(valor));

    [Fact]
    public void Normalizar_ignora_maiusculas_e_espacos_nas_pontas() =>
        Assert.Equal(IdentidadeDispositivo.Normalizar("  pm1a23b456 "), IdentidadeDispositivo.Normalizar("PM1A23B456"));

    [Fact]
    public void Mesmo_hardware_gera_sempre_o_mesmo_hwid_e_so_hashes_saem_do_pc()
    {
        var a = IdentidadeDispositivo.Calcular(Pc());
        var b = IdentidadeDispositivo.Calcular(Pc());
        Assert.Equal(a.Hwid, b.Hwid);
        Assert.Equal(a.Componentes, b.Componentes);
        Assert.Matches("^[0-9a-f]{64}$", a.Hwid);
        Assert.All(a.Componentes, c => Assert.Matches("^[0-9a-f]{64}$", c)); // formato que o servidor exige
        Assert.DoesNotContain(a.Componentes, c => c.Contains("pm1a23b456")); // o serial bruto não aparece
    }

    [Fact]
    public void Reinstalar_o_Windows_ou_trocar_a_cpu_nao_muda_o_hwid()
    {
        var antes = IdentidadeDispositivo.Calcular(Pc());
        var depois = IdentidadeDispositivo.Calcular(Pc(guid: "9a9b9c9d-1111-2222-3333-444455556666", cpu: "AFEBFBFF000A0655"));
        Assert.Equal(antes.Hwid, depois.Hwid);
        Assert.Equal(antes.Componentes, depois.Componentes);
    }

    [Fact]
    public void Outro_pc_com_a_mesma_cpu_e_o_machineguid_copiado_nao_passa_por_este()
    {
        // MachineGuid é editável no registro e o ID da CPU se repete no mesmo modelo: não podem identificar o PC.
        var meu = IdentidadeDispositivo.Calcular(Pc());
        var clone = IdentidadeDispositivo.Calcular(Pc(uuid: "aabbccdd-0000-1111-2222-333344445555", placa: "XYZ987"));
        Assert.NotEqual(meu.Hwid, clone.Hwid);
        Assert.Empty(clone.Componentes.Intersect(meu.Componentes)); // servidor recusa (exige as 2 peças fortes)
    }

    [Fact]
    public void Trocar_a_placa_mae_muda_o_vinculo() =>
        Assert.Single(IdentidadeDispositivo.Calcular(Pc(placa: "NOVA123")).Componentes
            .Intersect(IdentidadeDispositivo.Calcular(Pc()).Componentes)); // só o UUID bate: abaixo do mínimo de 2

    [Fact]
    public void Exibir_mostra_os_16_primeiros_caracteres_em_grupos() =>
        Assert.Equal("3F9A-0C12-77BE-41D0", IdentidadeDispositivo.Exibir("3f9a0c1277be41d0" + new string('0', 48)));

    [Fact]
    public void Outro_computador_nao_coincide_em_nenhum_componente()
    {
        var meu = IdentidadeDispositivo.Calcular(Pc());
        var outro = IdentidadeDispositivo.Calcular(Pc("11111111-2222-3333-4444-555555555556", "aabbccdd-0000-1111-2222-333344445555", "XYZ987", "AFEBFBFF000A0655"));
        Assert.Empty(outro.Componentes.Intersect(meu.Componentes));
    }

    [Fact]
    public void Mesmo_valor_em_componentes_diferentes_gera_hashes_diferentes() =>
        Assert.NotEqual(IdentidadeDispositivo.HashComponente("placa", "ABC123"), IdentidadeDispositivo.HashComponente("cpu", "ABC123"));

    [Fact]
    public void Componentes_genericos_ficam_de_fora_e_sem_nenhum_valido_nao_ha_hwid()
    {
        var r = IdentidadeDispositivo.Calcular(Pc(placa: "Default string", cpu: ""));
        Assert.Single(r.Componentes); // só o UUID: a placa é genérica e os fracos não entram quando há peça forte
        var semForte = IdentidadeDispositivo.Calcular(Pc(uuid: "", placa: "Default string"));
        Assert.Equal(2, semForte.Componentes.Length); // placa sem identificador: cai para MachineGuid + CPU
        Assert.Throws<System.InvalidOperationException>(() =>
            IdentidadeDispositivo.Calcular(new[] { new ComponenteHw("placa", "To be filled by O.E.M."), new ComponenteHw("cpu", null) }));
    }

    [Theory]
    [InlineData("vinculado", true)]
    [InlineData("ok", true)]
    [InlineData("admin", true)]
    [InlineData("outro_dispositivo", false)]
    [InlineData(null, false)]       // sem resposta (rede, função não publicada): falha fechada
    [InlineData("qualquer", false)]
    public void Resposta_do_servidor_libera_ou_bloqueia(string? status, bool liberado)
    {
        var (ok, msg) = ClienteSupabase.InterpretarVinculo(status);
        Assert.Equal(liberado, ok);
        Assert.Equal(liberado, msg is null);
    }
}
