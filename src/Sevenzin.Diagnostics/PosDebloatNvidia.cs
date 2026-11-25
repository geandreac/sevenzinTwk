using System.Diagnostics;
using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Estado persistido antes do reinício do Auto NVIDIA Debloat para concluir os ajustes pós-boot.</summary>
public sealed record EstadoPosDebloatNvidia(
    string PerfilId,
    string? NomeGpu,
    DateTime CriadoEmUtc);

/// <summary>
/// Persiste a pendência de pós-reinício do Auto NVIDIA Debloat e registra a retomada única por tarefa agendada
/// para aplicar o perfil NVIDIA Profile Inspector + abrir o Painel de Controle da NVIDIA após o boot.
/// </summary>
public sealed class PosDebloatNvidiaStore
{
    public const string ArgPosDebloat = "--pos-debloat-nvidia";
    public const string TarefaAutostart = "SevenzinTwksStartup";
    const string Tarefa = "SevenzinTwksPosDebloat";

    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    static readonly Lazy<PosDebloatNvidiaStore> PadraoLazy = new(() => new PosDebloatNvidiaStore());

    public static PosDebloatNvidiaStore Padrao => PadraoLazy.Value;

    readonly object _lock = new();
    readonly string _arquivo;

    public PosDebloatNvidiaStore(string? caminhoArquivo = null)
    {
        _arquivo = caminhoArquivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sevenzin",
            "pos-debloat-nvidia.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
    }

    public void SalvarPendente(EstadoPosDebloatNvidia estado)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
            var tmp = _arquivo + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(estado, Opts));
            File.Move(tmp, _arquivo, overwrite: true);
        }
    }

    public bool TemPendente()
    {
        lock (_lock)
        {
            return File.Exists(_arquivo);
        }
    }

    /// <summary>Entrega a pendência uma única vez, e só depois de um boot posterior a ela: reabrir o app sem reiniciar
    /// (reinício cancelado) mantém a pendência para o próximo boot.</summary>
    public EstadoPosDebloatNvidia? ConsumirPendente(DateTime? bootUtc = null)
    {
        lock (_lock)
        {
            if (!File.Exists(_arquivo)) return null;
            EstadoPosDebloatNvidia? estado = null;
            try { estado = JsonSerializer.Deserialize<EstadoPosDebloatNvidia>(File.ReadAllText(_arquivo), Opts); }
            catch { }
            if (estado is not null && estado.CriadoEmUtc > (bootUtc ?? MonitorReinicio.BootAtualUtc())) return null;
            try { File.Delete(_arquivo); } catch { }
            if (estado is null) return null;
            // Descarta pendências muito antigas (> 24 horas)
            if ((DateTime.UtcNow - estado.CriadoEmUtc).TotalHours > 24) return null;
            return estado;
        }
    }

    public void AgendarPosReinicio(string? caminhoExe, string? perfilId, string? nomeGpu)
    {
        var perfilEfetivo = string.IsNullOrWhiteSpace(perfilId) ? "fps2" : perfilId.Trim();
        SalvarPendente(new EstadoPosDebloatNvidia(perfilEfetivo, nomeGpu, DateTime.UtcNow));

        if (!string.IsNullOrWhiteSpace(caminhoExe))
            RegistrarTarefa(caminhoExe);
    }

    // Tarefa elevada, e não RunOnce: o app exige administrador e o Windows não o abriria pelo RunOnce no logon.
    // Com "Iniciar com o Windows" ligado a tarefa de autostart já abre o app; outra abriria duas instâncias.
    static void RegistrarTarefa(string caminhoExe)
    {
        if (Schtasks($"/Query /TN \"{TarefaAutostart}\"")) return;
        Schtasks($"/Create /TN \"{Tarefa}\" /TR \"\\\"{caminhoExe}\\\" {ArgPosDebloat}\" /SC ONLOGON /RL HIGHEST /F");
    }

    public static void RemoverTarefa() => Schtasks($"/Delete /TN \"{Tarefa}\" /F");

    static bool Schtasks(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false });
            return p is not null && p.WaitForExit(5000) && p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static string MontarArgumentosShutdown(int segundos) =>
        $"/r /t {Math.Max(0, segundos)} /c \"SevenzinTwks: reiniciando para concluir a instalação limpa do driver NVIDIA.\"";

    public static bool ExecutarReinicio(int segundos = 0)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("shutdown.exe", MontarArgumentosShutdown(segundos))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            return p is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void CancelarReinicio()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("shutdown.exe", "/a")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            p?.WaitForExit(2000);
        }
        catch { }
    }
}
