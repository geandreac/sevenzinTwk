using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class TweaksTests
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
    static Tweak Safe() => CatalogoTweaks.Todos[0]; // gamedvr-off, HKCU DWORD=0

    [Fact]
    public void Trava_recusa_tweak_de_seguranca()
    {
        var defender = new Tweak("x", "x", "x", "Seg", Colmeia.MaquinaLocal,
            @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, "Alto", true);
        Assert.True(GuardaSeguranca.Proibido(defender));
        var motor = new MotorTweaks(new RegistroFake(), PastaTemp());
        Assert.Throws<InvalidOperationException>(() => motor.Aplicar(defender));
    }

    [Fact]
    public void Catalogo_nao_tem_nenhum_tweak_de_seguranca()
    {
        Assert.All(CatalogoTweaks.Todos, t => Assert.False(GuardaSeguranca.Proibido(t)));
        // Admin exigido exatamente para os ajustes de máquina (HKLM); HKCU nunca pede admin.
        Assert.All(CatalogoTweaks.Todos, t => Assert.Equal(t.Colmeia == Colmeia.MaquinaLocal, t.ExigeAdmin));
        Assert.Contains(CatalogoTweaks.Todos, t => t.Colmeia == Colmeia.UsuarioAtual); // há HKCU sem admin
        Assert.Contains(CatalogoTweaks.Todos, t => t.Colmeia == Colmeia.MaquinaLocal); // há HKLM de máquina
    }

    [Fact]
    public void Catalogo_tem_ids_unicos_e_cobre_os_grupos_seguros()
    {
        var ids = CatalogoTweaks.Todos.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count()); // sem id duplicado
        // Expansão do subconjunto seguro do original: privacidade/telemetria/visual/jogos/desempenho presentes.
        var cats = CatalogoTweaks.Todos.Select(t => t.Categoria).ToHashSet();
        Assert.Superset(new HashSet<string> { "Jogos", "Privacidade", "Visual", "Telemetria", "Desempenho" }, cats);
        // Nenhum tweak toca no UAC (EnableLUA) nem no caminho de política do UAC — barrado pela trava, mas aqui provamos que nem entra.
        Assert.DoesNotContain(CatalogoTweaks.Todos, t => t.NomeValor.Equals("EnableLUA", StringComparison.OrdinalIgnoreCase));
        Assert.All(CatalogoTweaks.Todos, t => Assert.False(string.IsNullOrWhiteSpace(t.Descricao)));
    }

    [Fact]
    public void Aplicar_e_desfazer_tweak_de_maquina_hklm_funciona_igual()
    {
        var reg = new RegistroFake();
        var motor = new MotorTweaks(reg, PastaTemp());
        var hklm = CatalogoTweaks.Todos.First(t => t.Colmeia == Colmeia.MaquinaLocal);
        Assert.False(GuardaSeguranca.Proibido(hklm)); // o HKLM do catálogo não é proibido
        reg.EscreverDword(hklm.Colmeia, hklm.Caminho, hklm.NomeValor, 123); // valor anterior
        motor.Aplicar(hklm);
        Assert.Equal(hklm.ValorOtimizado, reg.LerDword(hklm.Colmeia, hklm.Caminho, hklm.NomeValor));
        Assert.True(motor.Desfazer(hklm));
        Assert.Equal(123, reg.LerDword(hklm.Colmeia, hklm.Caminho, hklm.NomeValor)); // restaurou
    }

    [Fact]
    public void Aplicar_escreve_o_valor_e_marca_aplicado()
    {
        var reg = new RegistroFake();
        var motor = new MotorTweaks(reg, PastaTemp());
        var t = Safe();
        Assert.False(motor.Aplicado(t));
        var r = motor.Aplicar(t);
        Assert.Null(r.ValorAnterior);                 // não existia antes
        Assert.True(motor.Aplicado(t));
        Assert.Equal(t.ValorOtimizado, reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor));
    }

    [Fact]
    public void Desfazer_restaura_valor_anterior_existente()
    {
        var reg = new RegistroFake();
        var t = Safe();
        reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, 1); // havia um valor antes (1)
        var motor = new MotorTweaks(reg, PastaTemp());
        motor.Aplicar(t);                                        // vira 0, backup guarda 1
        Assert.True(motor.Desfazer(t));
        Assert.Equal(1, reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor)); // restaurou 1
    }

    [Fact]
    public void Desfazer_remove_valor_que_nao_existia_e_persiste_entre_instancias()
    {
        var reg = new RegistroFake();
        var pasta = PastaTemp();
        var t = Safe();
        new MotorTweaks(reg, pasta).Aplicar(t);                  // não existia antes → backup null
        var motor2 = new MotorTweaks(reg, pasta);               // "reinício": lê o backup do disco
        Assert.True(motor2.TemBackup(t.Id));
        Assert.True(motor2.Desfazer(t));
        Assert.Null(reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor)); // valor removido
    }

    [Fact]
    public void Auditoria_registra_aplicar_e_desfazer()
    {
        var pasta = PastaTemp();
        var motor = new MotorTweaks(new RegistroFake(), pasta);
        var t = Safe();
        motor.Aplicar(t);
        motor.Desfazer(t);
        var log = File.ReadAllText(Path.Combine(pasta, "tweaks-auditoria.log"));
        Assert.Contains("aplicar\t" + t.Id, log);
        Assert.Contains("desfazer\t" + t.Id, log);
    }
}
