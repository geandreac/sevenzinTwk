using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class ProntidaoJogosTests
{
    sealed class RegistroMemoria : IRegistro
    {
        readonly Dictionary<(Colmeia, string, string), object> _dados = new();
        public int? LerDword(Colmeia h, string c, string n) => _dados.TryGetValue((h, c, n), out var v) && v is int i ? i : null;
        public void EscreverDword(Colmeia h, string c, string n, int valor) => _dados[(h, c, n)] = valor;
        public string? LerString(Colmeia h, string c, string n) => _dados.TryGetValue((h, c, n), out var v) && v is string s ? s : null;
        public void EscreverString(Colmeia h, string c, string n, string valor) => _dados[(h, c, n)] = valor;
        public void ApagarValor(Colmeia h, string c, string n) => _dados.Remove((h, c, n));
    }

    static MotorTweaks Motor() => new(new RegistroMemoria(), Path.Combine(Path.GetTempPath(), "sevenzin_pj_" + Guid.NewGuid().ToString("N")));
    static ItemProntidao I(string comp, NivelProntidao n, bool? ap) => new(Guid.NewGuid().ToString(), "x", comp, n, ap);

    [Fact]
    public void Todos_os_ids_existem_no_catalogo_e_nenhum_e_de_seguranca()
    {
        foreach (var (id, _, _, _) in ProntidaoJogos.TweaksDoCatalogo)
        {
            var t = CatalogoTweaks.Todos.SingleOrDefault(x => x.Id == id);
            Assert.NotNull(t);
            Assert.False(t!.ExigeConfirmacao, id); // risco alto nunca entra
        }
    }

    [Fact]
    public void Calcula_ponderado_arredonda_para_baixo_e_ignora_nao_lidos()
    {
        var r = ProntidaoJogos.Calcular(new[]
        {
            I("Windows", NivelProntidao.Necessario, true),   // 3 de 3
            I("CPU", NivelProntidao.Recomendado, false),     // 0 de 1
            I("Rede", NivelProntidao.Recomendado, null),     // fora da conta
        });
        Assert.Equal(75, r.Percentual); // 3/4
        Assert.Equal((1, 2), (r.Ativos, r.Total));
        Assert.Equal(new[] { "Windows", "CPU" }, r.Componentes.Select(c => c.Nome));
        Assert.Equal(100, r.Componentes[0].Percentual);

        var quase = Enumerable.Range(0, 300).Select(i => I("CPU", NivelProntidao.Recomendado, i > 0)).ToList();
        Assert.Equal(99, ProntidaoJogos.Calcular(quase).Percentual); // 299/300 não vira 100
        Assert.Equal(0, ProntidaoJogos.Calcular(Array.Empty<ItemProntidao>()).Percentual);
    }

    [Fact]
    public void Montar_respeita_fabricante_bateria_e_le_o_estado_real()
    {
        var motor = Motor();
        var nvidia = ProntidaoJogos.Montar(motor, FabricanteGpu.Nvidia, planoSevenzinAtivo: true, naBateria: false, ("normal", "ctcp"), Array.Empty<GrupoDriverGpu>());
        Assert.DoesNotContain(nvidia, i => i.Id == "amd-chill-off");
        Assert.True(nvidia.Single(i => i.Id == "tcp-global").Aplicado);
        Assert.True(nvidia.Single(i => i.Id == "plano-energia").Aplicado);
        Assert.False(nvidia.Single(i => i.Id == "gamedvr-off").Aplicado);

        motor.Aplicar(CatalogoTweaks.Todos.Single(t => t.Id == "gamedvr-off"));
        var amdBateria = ProntidaoJogos.Montar(motor, FabricanteGpu.Amd, null, naBateria: true, (null, null), Array.Empty<GrupoDriverGpu>());
        Assert.Contains(amdBateria, i => i.Id == "amd-chill-off");
        Assert.DoesNotContain(amdBateria, i => i.Id == "plano-energia"); // na bateria não vira pendência
        Assert.Null(amdBateria.Single(i => i.Id == "tcp-global").Aplicado);
        Assert.True(amdBateria.Single(i => i.Id == "gamedvr-off").Aplicado);
    }

    [Fact]
    public void Pendentes_so_inclui_recomendados_no_modo_completo_e_ignora_nao_lidos()
    {
        var itens = new[]
        {
            I("Windows", NivelProntidao.Necessario, false), I("CPU", NivelProntidao.Recomendado, false),
            I("Rede", NivelProntidao.Necessario, null), I("Mouse", NivelProntidao.Necessario, true),
        };
        Assert.Single(ProntidaoJogos.Pendentes(itens, incluirRecomendados: false));
        Assert.Equal(2, ProntidaoJogos.Pendentes(itens, incluirRecomendados: true).Count);
    }

    [Fact]
    public void Lote_continua_apos_falha_e_desfazer_volta_ao_estado_anterior()
    {
        var motor = Motor();
        var itens = ProntidaoJogos.Montar(motor, FabricanteGpu.Nvidia, null, true, (null, null), Array.Empty<GrupoDriverGpu>());
        var pend = ProntidaoJogos.Pendentes(itens, incluirRecomendados: false);
        Assert.Equal(11, pend.Count); // os 11 necessários do catálogo (sem AMD, sem plano na bateria)

        var r = ProntidaoJogos.AplicarLote(pend, i => i.Id == "fast-startup-off"
            ? throw new InvalidOperationException("sem permissão")
            : ProntidaoJogos.Executar(i.Id, true, motor, Array.Empty<GrupoDriverGpu>()));
        Assert.Equal(10, r.Aplicados.Count);
        Assert.Equal("sem permissão", r.Falhas.Single().Motivo);

        var depois = ProntidaoJogos.Montar(motor, FabricanteGpu.Nvidia, null, true, (null, null), Array.Empty<GrupoDriverGpu>());
        Assert.All(r.Aplicados, a => Assert.True(depois.Single(i => i.Id == a.Id).Aplicado));

        foreach (var a in r.Aplicados) Assert.Null(ProntidaoJogos.Executar(a.Id, false, motor, Array.Empty<GrupoDriverGpu>()));
        var desfeito = ProntidaoJogos.Montar(motor, FabricanteGpu.Nvidia, null, true, (null, null), Array.Empty<GrupoDriverGpu>());
        Assert.All(r.Aplicados, a => Assert.False(desfeito.Single(i => i.Id == a.Id).Aplicado));
    }

    [Fact]
    public void Desvio_e_item_do_lote_que_voltou_a_ficar_pendente_e_o_lote_persiste()
    {
        var lote = new LoteProntidao(new() { "a", "b" }, new());
        var itens = new[]
        {
            new ItemProntidao("a", "A", "CPU", NivelProntidao.Necessario, false),
            new ItemProntidao("b", "B", "CPU", NivelProntidao.Necessario, true),
            new ItemProntidao("c", "C", "CPU", NivelProntidao.Necessario, false), // pendente, mas nunca foi do lote
        };
        Assert.Equal(new[] { "a" }, ProntidaoJogos.Desvios(itens, lote).Select(i => i.Id));

        var arq = Path.Combine(Path.GetTempPath(), "sevenzin_lote_" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Empty(ProntidaoJogos.LerLote(arq).Ids); // sem arquivo: lote vazio
        ProntidaoJogos.SalvarLote(lote with { DesviosAvisados = new() { "a" } }, arq);
        var lido = ProntidaoJogos.LerLote(arq);
        Assert.Equal(new[] { "a", "b" }, lido.Ids);
        Assert.Equal(new[] { "a" }, lido.DesviosAvisados);
        File.Delete(arq);
    }

    [Fact]
    public void Grupos_de_driver_entram_so_para_o_fabricante_da_gpu()
    {
        var grupos = GpuDriverTweaks.CriarGruposNvidia().Concat(GpuDriverTweaks.CriarGruposAmd()).ToList();
        var itens = ProntidaoJogos.Montar(Motor(), FabricanteGpu.Nvidia, true, false, ("normal", "ctcp"), grupos);
        Assert.Equal(GpuDriverTweaks.CriarGruposNvidia().Count, itens.Count(i => i.Id.StartsWith("nv-")));
        Assert.DoesNotContain(itens, i => i.Id.StartsWith("amd-") && i.Id != "amd-chill-off");
    }
}
