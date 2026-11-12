using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Instâncias compartilhadas de autenticação/contas/sessão (um repositório único no processo).
/// WPF pequeno — localizador estático em vez de um contêiner de DI completo.</summary>
public static class Servicos
{
    public static readonly IRepositorioUsuarios Repo = new RepositorioUsuariosArquivo();
    public static readonly ServicoContas Contas = new(Repo);
    public static readonly ServicoAutenticacao Auth = new(Repo);
    public static readonly ServicoSessao Sessao = new();

    /// <summary>Semeia o admin das variáveis de ambiente (idempotente). Chamar uma vez na inicialização.</summary>
    public static string? SemearAdmin() => SemeadorAdmin.Semear(Contas, Environment.GetEnvironmentVariable);
}
