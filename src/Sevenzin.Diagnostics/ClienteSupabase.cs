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
public sealed record PerfilSupabase(string Id, string Email, string Papel, string? Nome, DateTime CriadoUtc);
public sealed record FatorMfa(string Id, string Status, string Tipo);
public sealed record InscricaoTotp(string FatorId, string Segredo, string Uri);

/// <summary>Cliente mínimo do Supabase via HTTP (endpoints estáveis GoTrue + PostgREST + Functions).
/// Faz login, lê o papel em <c>perfis</c> (RLS), registra/lê <c>acessos</c> e chama a Edge Function de criação de conta.
/// Senhas nunca passam por aqui em claro além do POST de login (TLS); o app não guarda senha.</summary>
public sealed class ClienteSupabase
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    readonly string _url, _anon;
    public SessaoSupabase? Sessao { get; private set; }
    public bool EhAdmin { get; private set; }
    public string? NomePerfil { get; private set; }

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
            NomePerfil = perfil?.Nome;
            return new(true, null, perfil?.Nome);
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    /// <summary>Perfil (papel/nome) do usuário logado — RLS garante que ele vê só o próprio.</summary>
    public async Task<PerfilSupabase?> PerfilAtualAsync()
    {
        if (Sessao is null) return null;
        var resp = await Http.SendAsync(Req(HttpMethod.Get,
            $"/rest/v1/perfis?select=id,email,papel,nome,criado_utc&id=eq.{Sessao.UserId}", token: Sessao.AccessToken));
        if (!resp.IsSuccessStatusCode) return null;
        var lista = Parsear(await resp.Content.ReadAsStringAsync());
        return lista.FirstOrDefault();
    }

    /// <summary>Todos os perfis (só admin, por RLS). Para a lista de contas da tela Admin.</summary>
    public async Task<List<PerfilSupabase>> ListarPerfisAsync()
    {
        if (Sessao is null) return new();
        var resp = await Http.SendAsync(Req(HttpMethod.Get,
            "/rest/v1/perfis?select=id,email,papel,nome,criado_utc&order=criado_utc.desc", token: Sessao.AccessToken));
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
                    p.TryGetProperty("id", out var pid) ? pid.GetString() ?? "" : "",
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

    /// <summary>Admin remove uma conta pela Edge Function (service_role no servidor).</summary>
    public async Task<ResultadoSupabase> RemoverUsuarioAsync(string id)
    {
        if (Sessao is null) return new(false, "Sessão expirada. Entre de novo.");
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Post, "/functions/v1/remover-usuario",
                new { id }, token: Sessao.AccessToken));
            var txt = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? new(true, null) : new(false, ErroDe(txt) ?? "Falha ao remover a conta.");
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    // ───────── MFA (TOTP) ─────────

    /// <summary>Fatores MFA do usuário logado (do objeto /auth/v1/user).</summary>
    public async Task<List<FatorMfa>> FatoresAsync()
    {
        var r = new List<FatorMfa>();
        if (Sessao is null) return r;
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Get, "/auth/v1/user", token: Sessao.AccessToken));
            if (!resp.IsSuccessStatusCode) return r;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("factors", out var fs) && fs.ValueKind == JsonValueKind.Array)
                foreach (var f in fs.EnumerateArray())
                    r.Add(new FatorMfa(
                        f.GetProperty("id").GetString()!,
                        f.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                        f.TryGetProperty("factor_type", out var t) ? t.GetString() ?? "" : ""));
        }
        catch { }
        return r;
    }

    public static FatorMfa? TotpVerificado(IEnumerable<FatorMfa> fatores) =>
        fatores.FirstOrDefault(f => f.Tipo == "totp" && f.Status == "verified");

    /// <summary>Inicia a inscrição de um TOTP; devolve o segredo para o usuário cadastrar no autenticador.</summary>
    public async Task<InscricaoTotp?> InscreverTotpAsync()
    {
        if (Sessao is null) return null;
        try
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Post, "/auth/v1/factors",
                new { factor_type = "totp", friendly_name = "Sevenzin " + DateTime.UtcNow.Ticks }, token: Sessao.AccessToken));
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var totp = root.GetProperty("totp");
            return new InscricaoTotp(root.GetProperty("id").GetString()!,
                totp.GetProperty("secret").GetString() ?? "",
                totp.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "");
        }
        catch { return null; }
    }

    /// <summary>Desafia e verifica um fator TOTP com o código do autenticador. Usado na inscrição E no login (eleva a aal2).</summary>
    public async Task<ResultadoSupabase> VerificarTotpAsync(string fatorId, string codigo)
    {
        if (Sessao is null) return new(false, "Sessão expirada.");
        try
        {
            var ch = await Http.SendAsync(Req(HttpMethod.Post, $"/auth/v1/factors/{fatorId}/challenge", new { }, token: Sessao.AccessToken));
            var chTxt = await ch.Content.ReadAsStringAsync();
            if (!ch.IsSuccessStatusCode) return new(false, ErroDe(chTxt) ?? "Falha ao iniciar a verificação.");
            var challengeId = JsonDocument.Parse(chTxt).RootElement.GetProperty("id").GetString()!;

            var vr = await Http.SendAsync(Req(HttpMethod.Post, $"/auth/v1/factors/{fatorId}/verify",
                new { challenge_id = challengeId, code = (codigo ?? "").Trim() }, token: Sessao.AccessToken));
            var vrTxt = await vr.Content.ReadAsStringAsync();
            if (!vr.IsSuccessStatusCode) return new(false, ErroDe(vrTxt) ?? "Código inválido ou expirado.");
            using var doc = JsonDocument.Parse(vrTxt);
            if (doc.RootElement.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
                Sessao = Sessao with { AccessToken = at.GetString()! };
            return new(true, null);
        }
        catch (Exception e) { return new(false, e.Message); }
    }

    public async Task<bool> DesinscreverFatorAsync(string fatorId)
    {
        if (Sessao is null) return false;
        try { return (await Http.SendAsync(Req(HttpMethod.Delete, $"/auth/v1/factors/{fatorId}", token: Sessao.AccessToken))).IsSuccessStatusCode; }
        catch { return false; }
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
