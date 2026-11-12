using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Write-cache por disco: resolução do caminho Enum (pura), tweak dinâmico (conformidade) e
/// aplicação/desfazer no registro fake. A descoberta real de discos é coberta por teste de consistência.</summary>
public class CacheEscritaTests
{
    sealed class RegistroFake : IRegistro
    {
        public readonly Dictionary<string, int> Store = new();
        static string K(Colmeia h, string c, string n) => $"{h}|{c}|{n}";
        public int? LerDword(Colmeia h, string c, string n) => Store.TryGetValue(K(h, c, n), out var v) ? v : null;
        public void EscreverDword(Colmeia h, string c, string n, int v) => Store[K(h, c, n)] = v;
        public void ApagarValor(Colmeia h, string c, string n) => Store.Remove(K(h, c, n));
    }

    static string PastaTemp() => Path.Combine(Path.GetTempPath(), "sevenzin-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CaminhoEnum_de_pnp_tem_o_prefixo_do_registro()
    {
        const string pnp = @"SCSI\Disk&Ven_Samsung&Prod_SSD\4&abcd&0&000000";
        Assert.Equal(@"SYSTEM\CurrentControlSet\Enum\" + pnp, CacheEscrita.CaminhoEnumDePnP(pnp));
    }

    [Fact]
    public void SanitizarId_e_estavel_e_sem_caracteres_especiais()
    {
        var a = CacheEscrita.SanitizarId(@"SCSI\Disk&Ven_X&Prod_Y\4&1&0");
        var b = CacheEscrita.SanitizarId(@"SCSI\Disk&Ven_X&Prod_Y\4&1&0");
        Assert.Equal(a, b);                       // mesmo PnP → mesmo Id (backup estável)
        Assert.Matches("^[0-9A-F]{32}$", a);      // hex puro: seguro como nome de arquivo/chave
        Assert.NotEqual(a, CacheEscrita.SanitizarId(@"SCSI\Disk&Ven_X&Prod_Y\4&2&0"));
    }

    [Fact]
    public void TweakParaDisco_exige_admin_confirmacao_e_risco_alto()
    {
        var disco = new DiscoCache("Samsung SSD", @"SCSI\Disk&Ven_Samsung\4&1&0",
            @"SYSTEM\CurrentControlSet\Enum\SCSI\Disk&Ven_Samsung\4&1&0\Device Parameters", null);

        var t = CacheEscrita.TweakParaDisco(disco);

        Assert.Equal(Colmeia.MaquinaLocal, t.Colmeia);
        Assert.True(t.ExigeAdmin);
        Assert.True(t.ExigeConfirmacao);
        Assert.Contains("ALTO", t.Risco, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CacheIsPowerProtected", t.NomeValor);
        Assert.Equal(1, t.ValorOtimizado);
        Assert.Equal(disco.CaminhoParametros, t.Caminho);
    }

    [Fact]
    public void Motor_aplica_e_desfaz_o_write_cache_no_registro_fake()
    {
        var reg = new RegistroFake();
        var motor = new MotorTweaks(reg, PastaTemp());
        var t = CacheEscrita.TweakParaDisco(new DiscoCache("X", "PnP-X", @"Enum\X\inst\Device Parameters", null));

        motor.Aplicar(t);
        Assert.Equal(1, reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor));
        Assert.True(motor.Aplicado(t));

        Assert.True(motor.Desfazer(t));
        Assert.Null(reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor)); // não existia antes → valor removido
    }

    [Fact]
    public void Listar_discos_nao_lanca_e_cada_entrada_tem_caminho_valido()
    {
        var discos = CacheEscrita.Listar(); // máquina real: pode ser vazio (sem WMI/Enum), nunca exceção
        Assert.All(discos, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.PnP));
            Assert.False(string.IsNullOrWhiteSpace(d.CaminhoParametros));
            Assert.StartsWith(@"SYSTEM\CurrentControlSet\Enum\", d.CaminhoParametros, StringComparison.OrdinalIgnoreCase);
        });
    }
}
