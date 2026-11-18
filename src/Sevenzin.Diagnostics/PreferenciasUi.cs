using System.Text.Json;

namespace Sevenzin.Diagnostics;

/// <summary>Preferências de comportamento da janela (bandeja do sistema).</summary>
public sealed record PreferenciasUi(
    bool MinimizarParaBandeja = false,
    bool FecharParaBandeja = false);

/// <summary>Armazenamento atômico local em `%LOCALAPPDATA%\Sevenzin\preferencias-ui.json`.</summary>
public sealed class PreferenciasUiStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    static readonly Lazy<PreferenciasUiStore> PadraoLazy = new(() => new PreferenciasUiStore());

    public static PreferenciasUiStore Padrao => PadraoLazy.Value;

    readonly object _lock = new();
    readonly string _arquivo;

    public PreferenciasUiStore(string? caminhoArquivo = null)
    {
        _arquivo = caminhoArquivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sevenzin",
            "preferencias-ui.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
    }

    public PreferenciasUi Carregar()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_arquivo)) return new PreferenciasUi();
                return JsonSerializer.Deserialize<PreferenciasUi>(File.ReadAllText(_arquivo), Opts) ?? new PreferenciasUi();
            }
            catch { return new PreferenciasUi(); }
        }
    }

    public void Salvar(PreferenciasUi prefs)
    {
        lock (_lock)
        {
            var tmp = _arquivo + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(prefs, Opts));
            File.Move(tmp, _arquivo, overwrite: true);
        }
    }
}
