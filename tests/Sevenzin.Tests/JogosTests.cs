using System.Diagnostics;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class JogosTests
{
    [Fact]
    public void Prioridade_nunca_e_realtime()
    {
        foreach (PrioridadeJogo p in Enum.GetValues<PrioridadeJogo>())
            Assert.NotEqual(ProcessPriorityClass.RealTime, AplicadorPerfil.Mapear(p));
    }

    [Fact]
    public void Aplicar_e_reverter_restaura_afinidade_e_prioridade()
    {
        using var proc = Process.GetCurrentProcess();
        var original = new EstadoProcesso((long)proc.ProcessorAffinity, proc.PriorityClass);
        try
        {
            long umNucleo = 1L; // só o núcleo 0
            var anterior = AplicadorPerfil.Aplicar(proc, umNucleo, PrioridadeJogo.AcimaDoNormal);
            Assert.Equal(original.Afinidade, anterior.Afinidade);                 // devolveu o estado anterior
            Assert.Equal(1L, (long)proc.ProcessorAffinity);                        // afinidade aplicada
            Assert.Equal(ProcessPriorityClass.AboveNormal, proc.PriorityClass);    // prioridade aplicada

            AplicadorPerfil.Reverter(proc, anterior);
            Assert.Equal(original.Afinidade, (long)proc.ProcessorAffinity);        // restaurou
            Assert.Equal(original.Prioridade, proc.PriorityClass);
        }
        finally
        {
            proc.ProcessorAffinity = (IntPtr)original.Afinidade;                   // garante restauração mesmo se falhar
            proc.PriorityClass = original.Prioridade;
        }
    }

    [Fact]
    public void Mascara_zero_vira_todos_os_nucleos()
    {
        using var proc = Process.GetCurrentProcess();
        var original = new EstadoProcesso((long)proc.ProcessorAffinity, proc.PriorityClass);
        try
        {
            AplicadorPerfil.Aplicar(proc, 0, PrioridadeJogo.Normal); // 0 = todos
            Assert.Equal(AplicadorPerfil.MascaraTodosNucleos, (long)proc.ProcessorAffinity);
        }
        finally { proc.ProcessorAffinity = (IntPtr)original.Afinidade; proc.PriorityClass = original.Prioridade; }
    }

    [Fact]
    public void Catalogo_adiciona_substitui_remove_e_persiste()
    {
        var arq = Path.Combine(Path.GetTempPath(), "sevenzin-jogos-" + Guid.NewGuid().ToString("N") + ".json");
        var cat = new CatalogoJogos(arq);
        cat.Adicionar(new PerfilJogo("Jogo A", "jogoA.exe", 3, PrioridadeJogo.Alta));
        cat.Adicionar(new PerfilJogo("Jogo A v2", "JOGOA.EXE", 1, PrioridadeJogo.Normal)); // mesmo exe (case-insensitive) substitui
        Assert.Single(cat.Todos);
        Assert.Equal("Jogo A v2", cat.Todos[0].Nome);

        var recarregado = new CatalogoJogos(arq); // persistiu em disco
        Assert.Single(recarregado.Todos);
        Assert.Equal(PrioridadeJogo.Normal, recarregado.Todos[0].Prioridade);

        Assert.True(recarregado.Remover("jogoa.exe"));
        Assert.Empty(new CatalogoJogos(arq).Todos);
    }

    sealed class FakeControle : IControleProcesso
    {
        public readonly HashSet<string> Rodando = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Aplicados = new();
        public readonly List<string> Revertidos = new();
        public bool AplicarOk = true;
        public IReadOnlyCollection<string> ExesRodando(IEnumerable<string> alvos) => alvos.Where(Rodando.Contains).ToList();
        public bool Aplicar(PerfilJogo jogo) { if (!AplicarOk) return false; Aplicados.Add(jogo.Executavel); return true; }
        public bool Reverter(string executavel) { Revertidos.Add(executavel); return true; }
    }

    static GestorJogos Gestor(FakeControle fc, params PerfilJogo[] jogos)
    {
        var arq = Path.Combine(Path.GetTempPath(), "sevenzin-g-" + Guid.NewGuid().ToString("N") + ".json");
        var cat = new CatalogoJogos(arq);
        foreach (var j in jogos) cat.Adicionar(j);
        return new GestorJogos(cat, fc);
    }

    [Fact]
    public void Sincronizar_aplica_o_que_abriu_nao_reaplica_e_reverte_o_que_fechou()
    {
        var fc = new FakeControle();
        var a = new PerfilJogo("A", "a.exe", 0, PrioridadeJogo.Alta);
        var b = new PerfilJogo("B", "b.exe", 0, PrioridadeJogo.Normal);
        var g = Gestor(fc, a, b);

        fc.Rodando.Add("a.exe");
        g.Sincronizar();
        Assert.True(g.EstaAplicado("a.exe"));
        Assert.False(g.EstaAplicado("b.exe"));
        Assert.Equal(new[] { "a.exe" }, fc.Aplicados);

        g.Sincronizar();                                   // a.exe ainda rodando → não reaplica
        Assert.Single(fc.Aplicados);

        fc.Rodando.Clear();                                // a.exe fechou → reverte
        g.Sincronizar();
        Assert.Contains("a.exe", fc.Revertidos);
        Assert.False(g.EstaAplicado("a.exe"));
    }

    [Fact]
    public void AplicarAgora_e_Reverter_manual_rastreiam_estado()
    {
        var fc = new FakeControle();
        var j = new PerfilJogo("A", "a.exe", 0, PrioridadeJogo.Alta);
        var g = Gestor(fc, j);

        Assert.True(g.AplicarAgora(j));
        Assert.True(g.EstaAplicado("a.exe"));
        Assert.True(g.Reverter(j));
        Assert.False(g.EstaAplicado("a.exe"));
        Assert.Contains("a.exe", fc.Revertidos);
        Assert.False(g.Reverter(j));                        // nada mais a reverter
    }

    [Fact]
    public void AplicarAgora_falha_sem_rastrear_quando_processo_nao_aplica()
    {
        var fc = new FakeControle { AplicarOk = false };
        var j = new PerfilJogo("A", "a.exe", 0, PrioridadeJogo.Alta);
        var g = Gestor(fc, j);

        Assert.False(g.AplicarAgora(j));
        Assert.False(g.EstaAplicado("a.exe"));
    }

    /// <summary>Integração: o controle REAL localiza processos pelo nome do executável (determinístico e
    /// sem efeito colateral). A mutação de afinidade/prioridade em si é coberta pelo teste do AplicadorPerfil.</summary>
    [Fact]
    public void ControleWindows_localiza_processos_pelo_executavel()
    {
        using var proc = Process.GetCurrentProcess();
        var exe = proc.ProcessName + ".exe";
        var c = new ControleProcessoWindows();

        Assert.True(c.ExesRodando(new[] { exe }).Count > 0);                       // acha o processo atual
        Assert.Empty(c.ExesRodando(new[] { "processo-inexistente-xyz-123.exe" })); // não inventa
        Assert.False(c.Reverter("processo-inexistente-xyz-123.exe"));              // nada a reverter sem estado
    }
}
