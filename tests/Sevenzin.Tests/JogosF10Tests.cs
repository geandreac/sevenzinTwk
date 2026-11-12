using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F10: GPU de alto desempenho e priority boost por jogo — JSON com os campos novos,
/// boost aplicado/revertido no próprio processo (seguro) e preferência de GPU gravada/removida.</summary>
public class JogosF10Tests
{
    sealed class RegistroStrFake : IRegistro
    {
        public readonly Dictionary<string, string> Store = new();
        static string K(Colmeia h, string c, string n) => $"{h}|{c}|{n}";
        public int? LerDword(Colmeia h, string c, string n) => throw new NotSupportedException();
        public void EscreverDword(Colmeia h, string c, string n, int v) => throw new NotSupportedException();
        public string? LerString(Colmeia h, string c, string n) => Store.TryGetValue(K(h, c, n), out var s) ? s : null;
        public void EscreverString(Colmeia h, string c, string n, string s) => Store[K(h, c, n)] = s;
        public void ApagarValor(Colmeia h, string c, string n) => Store.Remove(K(h, c, n));
    }

    [Fact]
    public void PerfilJogo_serializa_gpu_e_boost_e_tolera_json_antigo()
    {
        var opts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var novo = new PerfilJogo("X", @"C:\x.exe", 0, PrioridadeJogo.Alta, PrefereGpuAlta: true, PriorityBoost: true);
        var volta = JsonSerializer.Deserialize<PerfilJogo>(JsonSerializer.Serialize(novo, opts), opts);
        Assert.NotNull(volta);
        Assert.True(volta!.PrefereGpuAlta);
        Assert.True(volta.PriorityBoost);

        var antigo = """{"Nome":"A","Executavel":"C:\\a.exe","MascaraAfinidade":0,"Prioridade":"Normal"}""";
        var lido = JsonSerializer.Deserialize<PerfilJogo>(antigo, opts);
        Assert.NotNull(lido);
        Assert.False(lido!.PrefereGpuAlta);
        Assert.False(lido.PriorityBoost);
    }

    [Fact]
    public void Boost_liga_e_reverte_no_proprio_processo()
    {
        var p = Process.GetCurrentProcess();
        bool original = p.PriorityBoostEnabled;

        var estado = AplicadorPerfil.Aplicar(p, 0, PrioridadeJogo.Normal, boost: true);
        Assert.True(p.PriorityBoostEnabled);

        AplicadorPerfil.Reverter(p, estado);
        Assert.Equal(original, p.PriorityBoostEnabled); // restaura o estado exato do processo
    }

    [Fact]
    public void ControleProcessoWindows_grava_e_remove_a_preferencia_de_gpu()
    {
        var reg = new RegistroStrFake();
        var controle = new ControleProcessoWindows(registro: reg);
        var alvo = Process.GetCurrentProcess().ProcessName + ".exe";
        var jogo = new PerfilJogo("Teste", alvo, 0, PrioridadeJogo.Normal, PrefereGpuAlta: true);

        Assert.True(controle.Aplicar(jogo)); // processo de teste está rodando
        Assert.Equal("GpuPreference=2;", reg.LerString(Colmeia.UsuarioAtual, @"Software\Microsoft\DirectX\UserGpuPreferences", alvo));

        Assert.True(controle.Reverter(alvo));
        Assert.Null(reg.LerString(Colmeia.UsuarioAtual, @"Software\Microsoft\DirectX\UserGpuPreferences", alvo));
    }
}
