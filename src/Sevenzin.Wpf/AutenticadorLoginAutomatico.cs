using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>
/// Executa a autenticação automática em segundo plano sem exigir interação do usuário na tela de login,
/// utilizando as credenciais salvas via DPAPI ("Lembrar de mim").
/// </summary>
public static class AutenticadorLoginAutomatico
{
    /// <summary>
    /// Tenta realizar o login automático. Retorna true se a sessão foi aberta com sucesso.
    /// Em caso de falha transitória de rede durante o boot do Windows (ex.: placa de rede obtendo IP),
    /// tenta novamente até <paramref name="maxTentativas"/> vezes antes de desistir e abrir a tela de login.
    /// </summary>
    public static async Task<bool> TentarAsync(int maxTentativas = 3, int intervaloMs = 1500)
    {
        var prefs = PreferenciasUiStore.Padrao.Carregar();
        if (!prefs.LoginAutomatico) return false;

        var (email, senha) = CredenciaisLembradasStore.Padrao.Carregar();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
            return false;

        // Autenticação via Supabase (servidor de contas)
        if (Servicos.UsaSupabase)
        {
            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    var cliente = new ClienteSupabase();
                    var r = await cliente.LoginAsync(email, senha);
                    if (r.Ok)
                    {
                        // Se houver 2FA/MFA ativo, não pode completar auto-login sem o código do usuário
                        var fatores = await cliente.FatoresAsync();
                        if (ClienteSupabase.TotpVerificado(fatores) is not null)
                            return false;

                        // Computador não vinculado (ou sem resposta do servidor): vai para a tela de login,
                        // que mostra o motivo ao entrar manualmente.
                        if (!(await cliente.ConfirmarDispositivoAsync()).Ok)
                            return false;

                        var papel = cliente.EhAdmin ? PapelUsuario.Administrador : PapelUsuario.Usuario;
                        var nome = string.IsNullOrWhiteSpace(cliente.NomePerfil)
                            ? ValidacaoCredenciais.NomeDeEmail(cliente.Sessao!.Email)
                            : cliente.NomePerfil!;

                        Servicos.Sessao.Abrir(
                            new Usuario(cliente.Sessao!.Email, nome, papel, "-", DateTime.UtcNow),
                            autenticadoPorBackend: true);
                        Servicos.ClienteSupabase = cliente;

                        _ = cliente.RegistrarAcessoAsync(email, "Sucesso (Login Automático)", true);
                        RecuperacaoFalhaStore.Padrao.MarcarSessaoAtiva(email);
                        return true;
                    }

                    // Se foi rejeitado por senha errada ou conta inexistente, não repete
                    if (r.Mensagem is not null && (r.Mensagem.Contains("inválid", StringComparison.OrdinalIgnoreCase) ||
                                                  r.Mensagem.Contains("invalid", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
                catch
                {
                    // Falha de rede transitória durante o boot do Windows
                }

                if (tentativa < maxTentativas)
                    await Task.Delay(intervaloMs);
            }
            return false;
        }

        // Autenticação local
        try
        {
            var r = Servicos.Auth.Autenticar(email, senha);
            if (r.Ok && r.Usuario is not null)
            {
                RegistroAcessos.Registrar(email, "Sucesso (Login Automático)", true);
                Servicos.Sessao.Abrir(r.Usuario);
                RecuperacaoFalhaStore.Padrao.MarcarSessaoAtiva(email);
                return true;
            }
        }
        catch { }

        return false;
    }
}
