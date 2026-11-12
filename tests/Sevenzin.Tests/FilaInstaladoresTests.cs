using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>Fila de instaladores: persistência JSON local, substituição por nome/URL, limpeza e
/// tolerância a arquivo corrompido. Usa arquivo temporário — não toca a fila real do usuário.</summary>
public class FilaInstaladoresTests : IDisposable
{
    readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"sevenzin-fila-{Guid.NewGuid():N}.json");
    FilaInstaladores? _fila;

    FilaInstaladores Fila() => _fila ??= new FilaInstaladores(_arquivo);

    public void Dispose()
    {
        try { File.Delete(_arquivo); File.Delete(_arquivo + ".tmp"); } catch { }
        _fila = null;
    }

    static ItemFila Item(EstadoFila estado, string? detalhe = null) =>
        new("CPU-Z", "https://www.cpuid.com/cpu-z.exe", estado, detalhe, null);

    [Fact]
    public void Marcar_persiste_e_recarrega_no_mesmo_estado()
    {
        Fila().Marcar(Item(EstadoFila.Baixando, "42%"));

        var recarregada = new FilaInstaladores(_arquivo);
        Assert.Single(recarregada.Todos);
        Assert.Equal(EstadoFila.Baixando, recarregada.Todos[0].Estado);
        Assert.Equal("42%", recarregada.Todos[0].Detalhe);
        Assert.NotNull(recarregada.Todos[0].AtualizadoUtc);
    }

    [Fact]
    public void Marcar_substitui_o_mesmo_nome_url_e_limpar_remove_finalizados()
    {
        var fila = Fila();
        fila.Marcar(Item(EstadoFila.Pendente));
        fila.Marcar(Item(EstadoFila.Concluido, "verificado"));
        fila.Marcar(new ItemFila("Outro", "https://x.com/a.exe", EstadoFila.Falhou, "rede", null));

        Assert.Equal(2, fila.Todos.Count);            // mesmo nome/URL substituído, não duplicado
        Assert.Equal(EstadoFila.Concluido, fila.Todos.Single(i => i.Nome == "CPU-Z").Estado);

        fila.LimparConcluidos();
        Assert.Empty(fila.Todos);                     // Concluido e Falhou saem; Pendente ficaria
    }

    [Fact]
    public void Arquivo_corrompido_comeca_vazio_sem_excecao()
    {
        File.WriteAllText(_arquivo, "{ isso não é json");
        var fila = new FilaInstaladores(_arquivo);
        Assert.Empty(fila.Todos);
        fila.Marcar(Item(EstadoFila.Pendente));       // e continua funcionando
        Assert.Single(fila.Todos);
    }
}
