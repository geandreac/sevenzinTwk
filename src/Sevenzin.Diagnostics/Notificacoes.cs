using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Uma notificação interna do app (tudo local; nada sai da máquina).</summary>
public sealed record Notificacao(DateTime Utc, string Origem, string Titulo, string Detalhe);

/// <summary>Histórico local de notificações (%LOCALAPPDATA%\Sevenzin\notificacoes.json). As telas
/// registram aqui o que fizeram (tweak aplicado, instalação concluída, captura salva) — substitui a
/// "fila de tarefas agendadas" do original por algo real e auditável. Escrita atômica e thread-safe.</summary>
public sealed class NotificacoesStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    static readonly Lazy<NotificacoesStore> PadraoLazy = new(() => new NotificacoesStore());

    /// <summary>Instância padrão (arquivo real do usuário), usada pelas telas.</summary>
    public static NotificacoesStore Padrao => PadraoLazy.Value;

    readonly object _travado = new();
    readonly string _arquivo;
    readonly List<Notificacao> _itens;

    public NotificacoesStore(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "notificacoes.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _itens = Carregar();
    }

    public IReadOnlyList<Notificacao> Todos { get { lock (_travado) return _itens.OrderByDescending(n => n.Utc).ToList(); } }

    public void Adicionar(string origem, string titulo, string detalhe)
    {
        lock (_travado)
        {
            _itens.Add(new Notificacao(DateTime.UtcNow, origem, titulo, detalhe));
            if (_itens.Count > 200) _itens.RemoveRange(0, _itens.Count - 200); // histórico limitado
            Salvar();
        }
    }

    public void Limpar()
    {
        lock (_travado)
        {
            _itens.Clear();
            Salvar();
        }
    }

    List<Notificacao> Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<Notificacao>>(File.ReadAllText(_arquivo), Opts) ?? new() : new(); }
        catch { return new(); }
    }

    void Salvar()
    {
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_itens, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}
