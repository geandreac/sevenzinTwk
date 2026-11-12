using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sevenzin.Diagnostics;

/// <summary>Estado de um item da fila de downloads/verificação de instaladores.</summary>
public enum EstadoFila { Pendente, Baixando, Verificando, Concluido, Falhou }

/// <summary>Um item da fila: utilitário do catálogo + estado atual. Persistido em JSON local —
/// preenche a lacuna do original (05_metricas §7: o original NÃO persistia a fila).</summary>
public sealed record ItemFila(string Nome, string UrlDownload, EstadoFila Estado, string? Detalhe, DateTime? AtualizadoUtc);

/// <summary>Fila de instaladores persistida em %LOCALAPPDATA%\Sevenzin\fila-instaladores.json.
/// Só guarda o que o usuário pediu (nada é baixado aqui); o download em si continua no fluxo
/// verificado da tela Instaladores. Escrita atômica (temp + move) e thread-safe.</summary>
public sealed class FilaInstaladores
{
    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly object _travado = new();
    readonly string _arquivo;
    readonly List<ItemFila> _itens;

    public FilaInstaladores(string? caminho = null)
    {
        _arquivo = caminho ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "fila-instaladores.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _itens = Carregar();
    }

    public IReadOnlyList<ItemFila> Todos { get { lock (_travado) return _itens.ToList(); } }

    public void Marcar(ItemFila item)
    {
        lock (_travado)
        {
            _itens.RemoveAll(x => x.Nome.Equals(item.Nome, StringComparison.OrdinalIgnoreCase) && x.UrlDownload.Equals(item.UrlDownload, StringComparison.OrdinalIgnoreCase));
            _itens.Add(item with { AtualizadoUtc = DateTime.UtcNow });
            Salvar();
        }
    }

    public void Remover(ItemFila item)
    {
        lock (_travado)
        {
            if (_itens.RemoveAll(x => x.Nome.Equals(item.Nome, StringComparison.OrdinalIgnoreCase) && x.UrlDownload.Equals(item.UrlDownload, StringComparison.OrdinalIgnoreCase)) > 0)
                Salvar();
        }
    }

    public void LimparConcluidos()
    {
        lock (_travado)
        {
            if (_itens.RemoveAll(x => x.Estado == EstadoFila.Concluido || x.Estado == EstadoFila.Falhou) > 0)
                Salvar();
        }
    }

    List<ItemFila> Carregar()
    {
        try { return File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<ItemFila>>(File.ReadAllText(_arquivo), Opts) ?? new() : new(); }
        catch { return new(); }
    }

    void Salvar()
    {
        // Escrita atômica: nunca deixar um JSON pela metade se o app for encerrado no meio.
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_itens, Opts));
        File.Move(tmp, _arquivo, overwrite: true);
    }
}
