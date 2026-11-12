using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

public partial class App : Application
{
    public static bool ModoRender; // render headless / testes não devem bater na rede

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // O login é um diálogo mostrado ANTES da janela principal. Sem isto, fechar o login
        // (última janela) encerraria o app antes de abrir a MainWindow. Encerramento é explícito.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // Rolagem UNIFORME em todas as telas: passo controlado e consistente para os ScrollViewer de página.
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

    /// <summary>Rolagem da roda do mouse igual em todas as telas: passo fixo e suave no ScrollViewer de página.
    /// Grades/listas internas que ainda podem rolar ficam com o comportamento próprio (DataGrid rola por item).</summary>
    static void RolagemUniforme(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        if (e.Handled || sv.CanContentScroll || sv.ScrollableHeight <= 0) return; // modo "por item" (grade) usa o padrão
        // Se o cursor está sobre um ScrollViewer interno que ainda pode rolar na direção, deixa ele rolar.
        for (var d = e.OriginalSource as DependencyObject; d is Visual && d != sv; d = VisualTreeHelper.GetParent(d))
            if (d is ScrollViewer inner && inner != sv && inner.ScrollableHeight > 0 &&
                ((e.Delta < 0 && inner.VerticalOffset < inner.ScrollableHeight) || (e.Delta > 0 && inner.VerticalOffset > 0)))
                return;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.3);
        e.Handled = true;
    }
}
