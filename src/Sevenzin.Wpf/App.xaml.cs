using System.Windows;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // O login é um diálogo mostrado ANTES da janela principal. Sem isto, fechar o login
        // (última janela) encerraria o app antes de abrir a MainWindow. Encerramento é explícito.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
            // Modo headless (screenshots): sessão admin sintética para todas as telas renderizarem.
            Servicos.Sessao.Abrir(new Usuario("render@local", "Render", PapelUsuario.Administrador, "-", DateTime.UtcNow));
            var saida = i + 1 < e.Args.Length ? e.Args[i + 1] : "render.png";
            var tag = i + 2 < e.Args.Length ? e.Args[i + 2] : null;
            Renderizador.Rodar(this, saida, tag);
            return;
        }

        Servicos.SemearAdmin(); // cria o admin das variáveis de ambiente, se definidas

        var login = new LoginWindow();
        if (login.ShowDialog() == true && Servicos.Sessao.Valida)
            new MainWindow().Show();
        else
            Shutdown();
    }
}
