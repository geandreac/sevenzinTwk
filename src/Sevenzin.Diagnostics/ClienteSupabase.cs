using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Configuração do Supabase lida de variáveis de ambiente (nunca do código).
/// SEVENZIN_SUPABASE_URL = URL do projeto; SEVENZIN_SUPABASE_ANON = chave pública (publishable/anon).</summary>
public static class ConfigSupabase
{
    public static string? Url => Environment.GetEnvironmentVariable("SEVENZIN_SUPABASE_URL")?.Trim().TrimEnd('/');
    public static string? Anon => Environment.GetEnvironmentVariable("SEVENZIN_SUPABASE_ANON")?.Trim();
    public static bool Ativo => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Anon);
}

public sealed record SessaoSupabase(string AccessToken, string RefreshToken, string UserId, string Email);
public sealed record ResultadoSupabase(bool Ok, string? Mensagem, string? Dado = null);
public sealed record PerfilSupabase(string Email, string Papel, string? Nome, DateTime CriadoUtc);

/// <summary>Cliente mínimo do Supabase via HTTP (endpoints estáveis GoTrue + PostgREST + Functions).
/// Faz login, lê o papel em <c>perfis</c> (RLS), registra/lê <c>acessos</c> e chama a Edge Function de criação de conta.
/// Senhas nunca passam por aqui em claro além do POST de login (TLS); o app não guarda senha.</summary>
public sealed class ClienteSupabase
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    readonly string _url, _anon;
    public SessaoSupabase? Sessao { get; private set; }
    public bool EhAdmin { get; private set; }

    public ClienteSupabase()
    {
        _url = ConfigSupabase.Url ?? throw new InvalidOperationException("SEVENZIN_SUPABASE_URL não definida.");
        _anon = ConfigSupabase.Anon ?? throw new InvalidOperationException("SEVENZIN_SUPABASE_ANON não definida.");
    }

    HttpRequestMessage Req(HttpMethod metodo, string caminho, object? corpo = null, string? token = null, string? prefer = null)
    {
        var r = new HttpRequestMessage(metodo, _url + caminho);
        r.Headers.TryAddWithoutValidation("apikey", _anon);
        r.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (token ?? _anon));
        if (prefer is not null) r.Headers.TryAddWithoutValidation("Prefer", prefer);
        if (corpo is not null) r.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
        return r;
    }

    static string? ErroDe(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            foreach (var chave in new[] { "error_description", "msg", "message", "error", "erro" })
                if (d.RootElement.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
        }
        catch { }
        return null;
    }

    /// <summary>Login por e-mail/senha. Preenche <see cref="Sessao"/> e <see cref="EhAdmin"/> (do papel em perfis).</summary>
    public async Task<ResultadoSupabase> LoginAsync(string email, string senha)
    {
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Post, "/auth/v1/token?grant_type=password",
                new { email = (email ?? "").Trim(), password = senha }));
            var txt = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return new(false, ErroDe(txt) ?? "E-mail ou senha inválidos.");
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;
            var user = root.GetProperty("user");
            Sessao = new(
                root.GetProperty("access_token").GetString()!,
                root.GetProperty("refresh_token").GetString()!,
                user.GetProperty("id").GetString()!,
                (user.TryGetProperty("email", out var em) ? em.GetString() : null) ?? email ?? "");
            var perfil = await PerfilAtualAsync();
            EhAdmin = perfil?.Papel?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
            return new(true, null, perfil?.Nome);
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Perfil (papel/nome) do usuário logado — RLS garante que ele vê só o próprio.</summary>
    public async Task<PerfilSupabase?> PerfilAtualAsync()
    {
        if (Sessao is null) return null;
        var resp = await Http.SendAsync(Req(HttpMethod.Get,
            $"/rest/v1/perfis?select=email,papel,nome,criado_utc&id=eq.{Sessao.UserId}", token: Sessao.AccessToken));
        if (!resp.IsSuccessStatusCode) return null;
        var lista = Parsear(await resp.Content.ReadAsStringAsync());
        return lista.FirstOrDefault();
    }

    /// <summary>Todos os perfis (só admin, por RLS). Para a lista de contas da tela Admin.</summary>
    public async Task<List<PerfilSupabase>> ListarPerfisAsync()
    {
        if (Sessao is null) return new();
        var resp = await Http.SendAsync(Req(HttpMethod.Get,
            "/rest/v1/perfis?select=email,papel,nome,criado_utc&order=criado_utc.desc", token: Sessao.AccessToken));
        return resp.IsSuccessStatusCode ? Parsear(await resp.Content.ReadAsStringAsync()) : new();
    }

    static List<PerfilSupabase> Parsear(string json)
    {
        var r = new List<PerfilSupabase>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateArray())
                r.Add(new PerfilSupabase(
                    p.GetProperty("email").GetString() ?? "",
                    p.TryGetProperty("papel", out var pa) ? pa.GetString() ?? "usuario" : "usuario",
                    p.TryGetProperty("nome", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
                    p.TryGetProperty("criado_utc", out var c) && c.TryGetDateTime(out var dt) ? dt.ToUniversalTime() : default));
        }
        catch { }
        return r;
    }

    /// <summary>Admin cria uma conta chamando a Edge Function (a service_role fica no servidor).</summary>
    public async Task<ResultadoSupabase> CriarUsuarioAsync(string email, string senha, string papel, string? nome)
    {
        if (Sessao is null) return new(false, "Sessão expirada. Entre de novo.");
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Post, "/functions/v1/criar-usuario",
                new { email, senha, papel, nome }, token: Sessao.AccessToken));
            var txt = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? new(true, null) : new(false, ErroDe(txt) ?? "Falha ao criar a conta.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    public async Task RegistrarAcessoAsync(string email, string resultado, bool sucesso)
    {
        try
        {
            await Http.SendAsync(Req(HttpMethod.Post, "/rest/v1/acessos",
                new { email, resultado, sucesso }, prefer: "return=minimal"));
        }
        catch { }
    }

    /// <summary>Histórico de acessos (só admin, por RLS) para o dashboard.</summary>
    public async Task<List<Acesso>> LerAcessosAsync()
    {
        if (Sessao is null) return new();
        var r = new List<Acesso>();
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Get,
                "/rest/v1/acessos?select=utc,email,resultado,sucesso&order=utc.desc&limit=500", token: Sessao.AccessToken));
            if (!resp.IsSuccessStatusCode) return r;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            foreach (var a in doc.RootElement.EnumerateArray())
                r.Add(new Acesso(
                    a.TryGetProperty("utc", out var u) && u.TryGetDateTime(out var dt) ? dt.ToUniversalTime() : default,
                    a.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : "",
                    a.TryGetProperty("resultado", out var rs) && rs.ValueKind == JsonValueKind.String ? rs.GetString()! : "",
                    a.TryGetProperty("sucesso", out var s) && s.ValueKind == JsonValueKind.True));
        }
        catch { }
        return r;
    }

    /// <summary>Dispara o e-mail de redefinição de senha (Supabase Auth).</summary>
    public async Task<bool> ResetSenhaAsync(string email)
    {
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Post, "/auth/v1/recover", new { email = (email ?? "").Trim() }));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
