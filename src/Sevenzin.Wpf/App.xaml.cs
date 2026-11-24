using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

public partial class App : Application
{
    public static bool ModoRender; // render headless / testes não devem bater na rede
    public static RegistroRecuperacaoFalha? RecuperacaoRecente { get; set; }
    static int _relancando;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        RegistrarProtecaoContraFechamento();

        EventManager.RegisterClassHandler(typeof(ScrollViewer), ScrollViewer.PreviewMouseWheelEvent, new MouseWheelEventHandler(RolagemUniforme));

        int mi = Array.IndexOf(e.Args, "--makeicon");
        if (mi >= 0)
        {
            IconeGerador.Gerar(mi + 1 < e.Args.Length ? e.Args[mi + 1] : "sevenzin.ico", new[] { 16, 24, 32, 48, 64, 128, 256 });
            Shutdown();
            return;
        }

        int i = Array.IndexOf(e.Args, "--render");
        if (i >= 0)
        {
            ModoRender = true;
            Servicos.Sessao.Abrir(new Usuario("render@local", "Render", PapelUsuario.Administrador, "-", DateTime.UtcNow));
            var saida = i + 1 < e.Args.Length ? e.Args[i + 1] : "render.png";
            var tag = i + 2 < e.Args.Length ? e.Args[i + 2] : null;
            Renderizador.Rodar(this, saida, tag);
            return;
        }

        // Sempre abrir como Administrador logo na partida
        if (!Elevacao.EhAdmin() && Elevacao.GarantirAdminNaPartida(e.Args))
            return;

        Servicos.SemearAdmin();

        // Desligamento/reinício do PC desde a última abertura (tela azul, queda de energia, Windows Update…) → Notificações.
        // Em segundo plano: ler o log do Windows não pode atrasar a abertura.
        _ = Task.Run(() => MonitorReinicio.VerificarENotificar(NotificacoesStore.Padrao));

        var prefs = PreferenciasUiStore.Padrao.Carregar();
        bool deveIniciarMinimizado = StartupHelper.DeveIniciarMinimizado(e.Args, prefs.IniciarMinimizadoNaBandeja);

        // Verifica se houve fechamento inesperado na sessão anterior e registra em Notificações
        var recuperacao = RecuperacaoFalhaStore.Padrao.ConsumirPendente();
        bool veioDeAutoReabertura = Array.IndexOf(e.Args, "--recuperado") >= 0;
        if (recuperacao is not null)
        {
            RecuperacaoRecente = recuperacao;
            var resumoMotivo = $"O aplicativo fechou inesperadamente e foi recuperado. Motivo: {recuperacao.Motivo}"
                             + (string.IsNullOrWhiteSpace(recuperacao.Detalhe) ? "" : $" ({recuperacao.Detalhe})");
            NotificacoesStore.Padrao.Adicionar("Recuperação", "Fechamento inesperado detectado", resumoMotivo);

            // Reabertura rápida: se ocorreu nos últimos 3 minutos e o usuário local é conhecido, restaura a sessão sem pedir login de novo
            bool recente = veioDeAutoReabertura || (DateTime.UtcNow - recuperacao.OcorreuEmUtc).TotalSeconds <= 180;
            if (recente && !string.IsNullOrWhiteSpace(recuperacao.EmailUsuario))
            {
                var u = Servicos.Repo.PorEmail(recuperacao.EmailUsuario)
                     ?? (veioDeAutoReabertura
                         ? new Usuario(recuperacao.EmailUsuario, ValidacaoCredenciais.NomeDeEmail(recuperacao.EmailUsuario), PapelUsuario.Usuario, "-", DateTime.UtcNow)
                         : null);
                if (u is not null)
                {
                    Servicos.Sessao.Abrir(u);
                    RecuperacaoFalhaStore.Padrao.MarcarSessaoAtiva(u.Email);
                    var mwRec = new MainWindow();
                    if (deveIniciarMinimizado)
                        mwRec.IniciarMinimizadoNaBandeja();
                    else
                        mwRec.Show();
                    return;
                }
            }
        }

        // Tenta autenticação automática com credenciais salvas sem travar na tela de login
        bool logado = false;
        if (prefs.LoginAutomatico && CredenciaisLembradasStore.Padrao.TemCredenciais())
        {
            logado = await AutenticadorLoginAutomatico.TentarAsync(
                maxTentativas: deveIniciarMinimizado ? 4 : 2,
                intervaloMs: 1500);
        }

        if (!logado)
        {
            var login = new LoginWindow();
            if (login.ShowDialog() == true && Servicos.Sessao.Valida)
            {
                RecuperacaoFalhaStore.Padrao.MarcarSessaoAtiva(Servicos.Sessao.Atual?.Usuario.Email);
            }
            else
            {
                RecuperacaoFalhaStore.Padrao.MarcarEncerramentoNormal();
                Shutdown();
                return;
            }
        }

        var mw = new MainWindow();
        if (deveIniciarMinimizado)
        {
            mw.IniciarMinimizadoNaBandeja();
        }
        else
        {
            mw.Show();
        }
    }

    void RegistrarProtecaoContraFechamento()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            if (ModoRender) return;
            e.Handled = true; // Impede o encerramento do aplicativo!
            var motivo = ExtrairMotivoClaro(e.Exception);
            NotificacoesStore.Padrao.Adicionar(
                "Sistema",
                "Fechamento evitado automaticamente",
                $"Uma falha em tempo de execução foi interceptada sem fechar o SevenzinTwks. Motivo: {motivo}");

            if (Current?.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
            {
                // Avisar sobre um erro nunca pode fechar o app: uma falha aqui escapava do handler e derrubava o processo.
                try
                {
                    Toasts.Mostrar(
                        mw,
                        TipoToast.Aviso,
                        "Erro contido sem fechar",
                        $"Motivo: {motivo}",
                        () => mw.SelecionarAba("notificacoes"));
                }
                catch { }
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            if (ModoRender) return;
            var motivo = ExtrairMotivoClaro(e.Exception);
            NotificacoesStore.Padrao.Adicionar(
                "Sistema",
                "Falha em segundo plano contida",
                $"Tarefa assíncrona reportou erro sem fechar o aplicativo. Motivo: {motivo}");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (ModoRender) return;
            var ex = e.ExceptionObject as Exception;
            var motivo = ex is not null ? ExtrairMotivoClaro(ex) : "Falha crítica não tratada no domínio do aplicativo";
            var detalhe = ex?.GetType().Name ?? "UnhandledException";
            RecuperacaoFalhaStore.Padrao.RegistrarFalha(motivo, detalhe, Servicos.Sessao.Atual?.Usuario.Email);

            if (e.IsTerminating && Interlocked.Exchange(ref _relancando, 1) == 0)
                Elevacao.RelancarParaRecuperacao();
        };
    }

    static string ExtrairMotivoClaro(Exception ex)
    {
        var raiz = ex is AggregateException agg && agg.InnerExceptions.Count > 0
            ? agg.Flatten().InnerExceptions[0]
            : ex.InnerException ?? ex;
        var msg = (raiz.Message ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return string.IsNullOrWhiteSpace(msg) ? raiz.GetType().Name : msg;
    }

    static void RolagemUniforme(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        if (e.Handled || sv.CanContentScroll || sv.ScrollableHeight <= 0) return;
        for (var d = e.OriginalSource as DependencyObject; d is Visual && d != sv; d = VisualTreeHelper.GetParent(d))
            if (d is ScrollViewer inner && inner != sv && inner.ScrollableHeight > 0 &&
                ((e.Delta < 0 && inner.VerticalOffset < inner.ScrollableHeight) || (e.Delta > 0 && inner.VerticalOffset > 0)))
                return;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.3);
        e.Handled = true;
    }
}
