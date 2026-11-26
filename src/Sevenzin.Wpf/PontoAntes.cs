using System.Windows;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Passo "ponto de restauração" antes de uma operação com impacto no sistema. Respeita as preferências
/// (criar antes / bloquear se falhar), registra o resultado em Notificações e devolve se a operação pode seguir.</summary>
static class PontoAntes
{
    public const string AvisoBackup = "Pontos de restauração cobrem configurações do sistema, registro e drivers. Eles não guardam arquivos pessoais e não substituem um backup.";

    /// <summary>true = pode aplicar a <paramref name="acao"/>; false = o usuário desistiu ou o ponto falhou e ele não quis seguir.</summary>
    public static async Task<bool> GarantirAsync(FrameworkElement origem, string acao, Action<string>? status = null)
    {
        var prefs = PreferenciasUiStore.Padrao.Carregar();
        if (!prefs.CriarPontoAntesDeOtimizar) return true;

        status?.Invoke("Criando ponto de restauração do Windows…");
        var r = await Task.Run(() => PontoRestauracao.Criar($"SevenzinTwks — antes de {acao}"));
        if (r.Criado)
        {
            NotificacoesStore.Padrao.Adicionar("Proteção", "Ponto de restauração criado", $"{r.Mensagem} Motivo: antes de {acao}.");
            Toasts.Mostrar(origem, TipoToast.Sucesso, "Ponto de restauração criado", r.Mensagem);
            return true;
        }

        NotificacoesStore.Padrao.Adicionar("Proteção", "Ponto de restauração não criado", $"{r.Mensagem} (antes de {acao})");
        if (!prefs.BloquearSemPonto)
        {
            Toasts.Mostrar(origem, TipoToast.Aviso, "Seguindo sem ponto de restauração", r.Mensagem);
            return true;
        }
        bool seguir = MessageBox.Show(
            $"Não foi possível criar o ponto de restauração.\n\n{r.Mensagem}\n\nSem ele, voltar atrás depende só do \"Desfazer\" do SevenzinTwks.\n\nContinuar \"{acao}\" mesmo assim?",
            "Sem ponto de restauração", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        NotificacoesStore.Padrao.Adicionar("Proteção", seguir ? "Seguiu sem ponto de restauração" : "Operação cancelada sem ponto de restauração",
            $"Decisão do usuário em \"{acao}\".");
        return seguir;
    }
}
