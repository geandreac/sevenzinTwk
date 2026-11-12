using MongoDB.Bson;
using MongoDB.Driver;

namespace Sevenzin.Diagnostics;

/// <summary>Contas em MongoDB (servidor). A string de conexão vem de fora (variável de ambiente), nunca do código.
/// Senhas continuam em bcrypt (só o hash é guardado). O e-mail normalizado é o <c>_id</c> (chave única natural).
/// O mapeamento documento↔Usuario é explícito e testável, sem depender de um servidor.</summary>
public sealed class RepositorioUsuariosMongo : IRepositorioUsuarios
{
    readonly IMongoCollection<BsonDocument> _col;

    public RepositorioUsuariosMongo(string connectionString, string? nomeBanco = null, string colecao = "usuarios")
    {
        var url = MongoUrl.Create(connectionString);
        var settings = MongoClientSettings.FromUrl(url);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5); // não trava o app se o servidor não responder
        var client = new MongoClient(settings);
        var db = client.GetDatabase(nomeBanco ?? (string.IsNullOrWhiteSpace(url.DatabaseName) ? "sevenzin" : url.DatabaseName));
        _col = db.GetCollection<BsonDocument>(colecao);
    }

    static string Id(string? email) => (email ?? "").Trim().ToLowerInvariant();
    static FilterDefinition<BsonDocument> PorId(string? email) => Builders<BsonDocument>.Filter.Eq("_id", Id(email));
    static FilterDefinition<BsonDocument> Tudo => Builders<BsonDocument>.Filter.Empty;

    public static BsonDocument ParaDoc(Usuario u) => new()
    {
        ["_id"] = Id(u.Email),
        ["Email"] = u.Email,
        ["Nome"] = u.Nome,
        ["Papel"] = u.Papel.ToString(),
        ["HashSenha"] = u.HashSenha,
        ["CriadoUtc"] = u.CriadoUtc.ToUniversalTime(),
        ["TotpSegredo"] = (BsonValue?)u.TotpSegredo ?? BsonNull.Value,
        ["TotpAtivo"] = u.TotpAtivo,
    };

    public static Usuario DeDoc(BsonDocument d)
    {
        var ts = d.GetValue("TotpSegredo", BsonNull.Value);
        return new Usuario(
            d.GetValue("Email", "").AsString,
            d.GetValue("Nome", "").AsString,
            Enum.TryParse<PapelUsuario>(d.GetValue("Papel", "Usuario").AsString, out var p) ? p : PapelUsuario.Usuario,
            d.GetValue("HashSenha", "").AsString,
            d.GetValue("CriadoUtc", (BsonValue)DateTime.MinValue).ToUniversalTime(),
            ts.IsBsonNull ? null : ts.AsString,
            d.GetValue("TotpAtivo", false).AsBoolean);
    }

    public IReadOnlyList<Usuario> Todos => _col.Find(Tudo).ToList().Select(DeDoc).ToList();
    public Usuario? PorEmail(string email) { var d = _col.Find(PorId(email)).FirstOrDefault(); return d is null ? null : DeDoc(d); }
    public void Adicionar(Usuario u) => _col.ReplaceOne(PorId(u.Email), ParaDoc(u), new ReplaceOptions { IsUpsert = true });
    public void Atualizar(Usuario u) => _col.ReplaceOne(PorId(u.Email), ParaDoc(u), new ReplaceOptions { IsUpsert = true });
    public bool Remover(string email) => _col.DeleteOne(PorId(email)).DeletedCount > 0;

    /// <summary>True se o servidor responde (ping). Usado para decidir usar Mongo ou cair no JSON.</summary>
    public bool Responde()
    {
        try { _col.Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1)); return true; }
        catch { return false; }
    }

    /// <summary>Primeira vez: se a coleção está vazia, copia as contas do repositório de origem (ex.: o JSON local).</summary>
    public void MigrarSeVazio(IRepositorioUsuarios origem)
    {
        if (_col.CountDocuments(Tudo) > 0) return;
        foreach (var u in origem.Todos) Adicionar(u);
    }
}
