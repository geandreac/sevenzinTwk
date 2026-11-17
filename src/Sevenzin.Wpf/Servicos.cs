using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Instâncias compartilhadas de autenticação/contas/sessão (um repositório único no processo).
/// WPF pequeno — localizador estático em vez de um contêiner de DI completo.</summary>
public static class Servicos
{
    public static readonly IRepositorioUsuarios Repo = CriarRepo();
    public static readonly RepositorioConvites Convites = new();
    public static readonly ServicoContas Contas = new(Repo, Environment.GetEnvironmentVariable, Convites);
    public static readonly ServicoAutenticacao Auth = new(Repo);
    public static readonly ServicoSessao Sessao = new(lerEnv: Environment.GetEnvironmentVariable);

    /// <summary>Quando o Supabase está configurado (SEVENZIN_SUPABASE_URL/ANON), a autenticação usa o Supabase.</summary>
    public static bool UsaSupabase => ConfigSupabase.Ativo;

    /// <summary>Cliente Supabase da sessão logada (login, criar conta, acessos). Null quando no modo local.</summary>
    public static ClienteSupabase? ClienteSupabase { get; set; }

    /// <summary>Semeia o admin das variáveis de ambiente (idempotente) e garante o ADMINISTRADOR ÚNICO:
    /// rebaixa qualquer outra conta administradora que não seja a identidade provisionada. Chamar uma vez
    /// na inicialização.</summary>
    public static string? SemearAdmin()
    {
        var semeado = SemeadorAdmin.Semear(Contas, Environment.GetEnvironmentVariable);
        Contas.GarantirAdminUnico();
        return semeado;
    }

    /// <summary>Qual banco de contas usar: se <c>SEVENZIN_DB</c> (string de conexão Mongo) estiver definida e o
    /// servidor responder, usa MongoDB (migrando o JSON local na 1ª vez); senão, usa o arquivo JSON local.
    /// Assim o app nunca trava se o banco estiver fora do ar, e as credenciais ficam fora do código.</summary>
    static IRepositorioUsuarios CriarRepo()
    {
        var cs = Environment.GetEnvironmentVariable("SEVENZIN_DB");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            try
            {
                var mongo = new RepositorioUsuariosMongo(cs, Environment.GetEnvironmentVariable("SEVENZIN_DB_NOME"));
                if (mongo.Responde())
                {
                    mongo.MigrarSeVazio(new RepositorioUsuariosArquivo());
                    return mongo;
                }
            }
            catch { /* cai no JSON local */ }
        }
        return new RepositorioUsuariosArquivo();
    }
}
