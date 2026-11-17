namespace Sevenzin.Diagnostics;

/// <summary>Variante visual de um toast de notificação.</summary>
public enum TipoToast { Sucesso, Erro, Aviso, Info }

/// <summary>Fila de toasts (lógica pura, testável): deduplicação por janela curta, duração fixa,
/// expiração com pausa e contagem de visíveis. A apresentação fica na UI (WPF).</summary>
public sealed class FilaToasts
{
    public static readonly TimeSpan DuracaoPadrao = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan JanelaDeduplicacao = TimeSpan.FromSeconds(1);

    public sealed record Item(Guid Id, DateTime CriadoUtc, TipoToast Tipo, string Titulo, string Mensagem);

    readonly List<Item> _itens = new();
    public IReadOnlyList<Item> Itens => _itens;

    /// <summary>Adiciona um toast; devolve null quando é duplicata do MESMO evento em < 1 s (dedup).</summary>
    public Item? Adicionar(TipoToast tipo, string titulo, string mensagem, DateTime? agora = null)
    {
        var instante = agora ?? DateTime.UtcNow;
        if (_itens.Any(i => i.Tipo == tipo && i.Titulo.Equals(titulo, StringComparison.OrdinalIgnoreCase)
                && instante - i.CriadoUtc < JanelaDeduplicacao))
            return null;
        var item = new Item(Guid.NewGuid(), instante, tipo, titulo, mensagem);
        _itens.Add(item);
        return item;
    }

    /// <summary>Devolve os itens expirados (criados há mais que a duração) exceto os pausados, e os remove.
    /// Função pura em relação ao relógio (testável).</summary>
    public List<Item> RemoverExpirados(DateTime agora, TimeSpan duracao, IReadOnlyCollection<Guid>? pausados = null)
    {
        var expirados = _itens
            .Where(i => agora - i.CriadoUtc >= duracao && (pausados is null || !pausados.Contains(i.Id)))
            .ToList();
        foreach (var e in expirados) _itens.Remove(e);
        return expirados;
    }

    /// <summary>Remove um item específico (dismiss manual / Desfazer executado).</summary>
    public bool Remover(Guid id) => _itens.RemoveAll(i => i.Id == id) > 0;
}
