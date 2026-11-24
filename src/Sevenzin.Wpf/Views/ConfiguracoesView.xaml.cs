using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf.Views;

/// <summary>Tela "Configurações" unificada com abas para Geral, Preferências, Perfil, Notificações, Telemetria e Benchmark.</summary>
public partial class ConfiguracoesView : UserControl
{
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "Sevenzin";
    const string NomeTarefaStartup = "SevenzinTwksStartup";
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");

    Border[] _tabs = Array.Empty<Border>();
    FrameworkElement[] _paineis = Array.Empty<FrameworkElement>();

    public ConfiguracoesView(string? abaInicial = null)
    {
        InitializeComponent();
        _tabs = new[] { TabGeral, TabPref, TabPerfil, TabNotificacoes, TabTelemetria, TabBenchmark };
        _paineis = new FrameworkElement[] { GeralPanel, PrefPanel, PerfilPanel, NotificacoesPanel, TelemetriaPanel, BenchmarkPanel };
        SwInicio.IsChecked = StartupLigado();
        var prefsUi = PreferenciasUiStore.Padrao.Carregar();
        SwMinBandeja.IsChecked = prefsUi.MinimizarParaBandeja;
        SwFecharBandeja.IsChecked = prefsUi.FecharParaBandeja;
        SwInicioMinimizado.IsChecked = prefsUi.IniciarMinimizadoNaBandeja;
        SwLoginAuto.IsChecked = prefsUi.LoginAutomatico;
        PastaTxt.Text = Pasta;
        VersaoTxt.Text = $"SevenzinTwks {VersaoApp.ToString(3)} · reconstrução segura (WPF)";
        var cfgAssistente = new ConfigAssistenteStore().Carregar();
        AssistenteTxt.Text = cfgAssistente.EndpointUrl ?? "";
        AtualizarStatusAssistente();
        SelecionarSubAba(abaInicial ?? "geral");
    }

    public void SelecionarSubAba(string chave)
    {
        int indice = chave.Trim().ToLowerInvariant() switch
        {
            "pref" or "preferencias" => 1,
            "perfil" => 2,
            "notificacoes" => 3,
            "telemetria" => 4,
            "benchmark" => 5,
            _ => 0,
        };
        SelecionarAba(indice);
    }

    void AtualizarStatusAssistente()
    {
        var url = AssistenteTxt.Text.Trim();
        AssistenteStatus.Text = Assistente.UrlEndpointValida(url)
            ? "Endpoint configurado — o envio envia cada mensagem só quando você aperta Enviar."
            : "Nenhum endpoint configurado — o envio fica desligado.";
    }

    void AssistenteSalvar_Click(object sender, RoutedEventArgs e)
    {
        var url = AssistenteTxt.Text.Trim();
        if (url.Length == 0)
        {
            new ConfigAssistenteStore().Salvar(new ConfigAssistente(null));
            AtualizarStatusAssistente();
            Toasts.Mostrar(this, TipoToast.Sucesso, "Endpoint removido", "O envio do assistente volta a ficar desligado.");
            return;
        }
        if (!Assistente.UrlEndpointValida(url))
        {
            Toasts.Mostrar(this, TipoToast.Erro, "URL inválida", "Use uma URL HTTPS absoluta.");
            MessageBox.Show("Use uma URL HTTPS absoluta.", "URL inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new ConfigAssistenteStore().Salvar(new ConfigAssistente(url));
        AtualizarStatusAssistente();
        Toasts.Mostrar(this, TipoToast.Sucesso, "Endpoint salvo", "O envio do assistente é explícito, por mensagem.");
    }

    void AssistenteLimpar_Click(object sender, RoutedEventArgs e)
    {
        AssistenteTxt.Text = "";
        new ConfigAssistenteStore().Salvar(new ConfigAssistente(null));
        AtualizarStatusAssistente();
    }

    void Tab_Click(object sender, MouseButtonEventArgs e) => SelecionarAba(int.Parse((string)((Border)sender).Tag));

    void SelecionarAba(int i)
    {
        for (int k = 0; k < _tabs.Length; k++)
        {
            bool ativo = k == i;
            _tabs[k].BorderBrush = ativo ? (Brush)Application.Current.Resources["Brand500Brush"] : Brushes.Transparent;
            if (_tabs[k].Child is TextBlock t)
            {
                t.Foreground = (Brush)Application.Current.Resources[ativo ? "TextPrimaryBrush" : "TextMutedBrush"];
                t.FontWeight = ativo ? FontWeights.SemiBold : FontWeights.Normal;
            }
            _paineis[k].Visibility = ativo ? Visibility.Visible : Visibility.Collapsed;
        }
        if (i == 3) NotificacoesSubView.Atualizar();
    }

    static bool StartupLigado()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{NomeTarefaStartup}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is not null && p.WaitForExit(2500) && p.ExitCode == 0)
                return true;
        }
        catch { }
        using var k = Registry.CurrentUser.OpenSubKey(RunPath);
        return k?.GetValue(RunName) != null;
    }

    void InicioMinimizado_Click(object sender, RoutedEventArgs e)
    {
        var antigo = PreferenciasUiStore.Padrao.Carregar();
        var novo = antigo with { IniciarMinimizadoNaBandeja = SwInicioMinimizado.IsChecked == true };
        PreferenciasUiStore.Padrao.Salvar(novo);
        Toasts.Mostrar(
            this,
            TipoToast.Sucesso,
            "Inicialização na bandeja atualizada",
            novo.IniciarMinimizadoNaBandeja
                ? "O SevenzinTwks iniciará recolhido nos Tray Icons."
                : "O SevenzinTwks iniciará com a janela aberta na tela.");
    }

    void LoginAuto_Click(object sender, RoutedEventArgs e)
    {
        var antigo = PreferenciasUiStore.Padrao.Carregar();
        var novo = antigo with { LoginAutomatico = SwLoginAuto.IsChecked == true };
        PreferenciasUiStore.Padrao.Salvar(novo);
        Toasts.Mostrar(
            this,
            TipoToast.Sucesso,
            "Login automático atualizado",
            novo.LoginAutomatico
                ? "Login automático ativado — entra direto com as credenciais salvas."
                : "Login automático desativado — sempre exibirá a tela de login.");
    }

    void Bandeja_Click(object sender, RoutedEventArgs e)
    {
        var antigo = PreferenciasUiStore.Padrao.Carregar();
        var novo = antigo with
        {
            MinimizarParaBandeja = SwMinBandeja.IsChecked == true,
            FecharParaBandeja = SwFecharBandeja.IsChecked == true,
        };
        PreferenciasUiStore.Padrao.Salvar(novo);

        if (Window.GetWindow(this) is MainWindow mw)
            mw.SincronizarIconeBandeja();

        Toasts.Mostrar(
            this,
            TipoToast.Sucesso,
            "Bandeja do sistema atualizada",
            novo.MinimizarParaBandeja || novo.FecharParaBandeja
                ? "O ícone do SevenzinTwks já está ativo perto do relógio."
                : "Minimizar e fechar para a bandeja desativados.");
    }

    void Inicio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Environment.ProcessPath ?? "";
            using (var k = Registry.CurrentUser.CreateSubKey(RunPath, writable: true))
                k?.DeleteValue(RunName, throwOnMissingValue: false);

            if (SwInicio.IsChecked == true)
            {
                using var p = Process.Start(new ProcessStartInfo("schtasks.exe",
                    $"/Create /TN \"{NomeTarefaStartup}\" /TR \"\\\"{exe}\\\" --autostart\" /SC ONLOGON /RL HIGHEST /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                p?.WaitForExit(4000);
                if (p is null || p.ExitCode != 0)
                {
                    using var k = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
                    k.SetValue(RunName, $"\"{exe}\" --autostart", RegistryValueKind.String);
                }
            }
            else
            {
                using var p = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Delete /TN \"{NomeTarefaStartup}\" /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                p?.WaitForExit(3000);
            }

            bool confirmado = StartupLigado() == SwInicio.IsChecked;
            Toasts.Mostrar(this, confirmado ? TipoToast.Sucesso : TipoToast.Aviso,
                confirmado ? "Inicialização salva" : "Alteração não confirmada",
                confirmado ? "Iniciar com o Windows " + (SwInicio.IsChecked == true ? "ligado (como Administrador)." : "desligado.") : "Não foi possível confirmar a tarefa agendada.",
                SwInicio.IsChecked == true ? () => { SwInicio.IsChecked = false; Inicio_Click(sender, e); } : null);
        }
        catch (Exception ex)
        {
            SwInicio.IsChecked = StartupLigado();
            Toasts.Mostrar(this, TipoToast.Erro, "Falha ao salvar", ex.Message);
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void AbrirPasta_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Pasta}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    static Version VersaoApp => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
    string? _urlRelease;

    async void Verificar_Click(object sender, RoutedEventArgs e)
    {
        VerificarBtn.IsEnabled = false;
        AbrirReleaseBtn.Visibility = Visibility.Collapsed;
        AtualizaStatus.Text = "Verificando no GitHub…";
        var r = await VerificadorAtualizacoes.VerificarAsync(VersaoApp);
        VerificarBtn.IsEnabled = true;
        if (r is null) { AtualizaStatus.Text = "Não foi possível verificar agora (sem internet ou ainda sem releases publicadas)."; return; }
        if (r.HaNova)
        {
            _urlRelease = r.Url;
            AbrirReleaseBtn.Visibility = r.Url.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            AtualizaStatus.Text = $"Nova versão disponível: {r.VersaoRemota} (você tem {r.VersaoAtual}). O download abre a página da release no navegador.";
        }
        else AtualizaStatus.Text = $"Você está na versão mais recente ({r.VersaoAtual}).";
    }

    void AbrirRelease_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_urlRelease)) return;
        try { Process.Start(new ProcessStartInfo(_urlRelease) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
