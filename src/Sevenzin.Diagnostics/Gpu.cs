using System.Management;

namespace Sevenzin.Diagnostics;

public enum FabricanteGpu { Nvidia, Amd, Intel, Desconhecido }

/// <summary>Identificação de fabricante e página OFICIAL do driver (origem verificada — nunca espelhos).</summary>
public static class DriverGpu
{
    public static FabricanteGpu Detectar(string? nomeAdaptador)
    {
        var n = (nomeAdaptador ?? "").ToLowerInvariant();
        if (n.Contains("nvidia") || n.Contains("geforce") || n.Contains("rtx") || n.Contains("gtx") || n.Contains("quadro")) return FabricanteGpu.Nvidia;
        if (n.Contains("radeon") || n.Contains("amd")) return FabricanteGpu.Amd;
        if (n.Contains("intel") || n.Contains("iris") || n.Contains("uhd") || n.Contains("hd graphics")) return FabricanteGpu.Intel;
        return FabricanteGpu.Desconhecido;
    }

    /// <summary>URL oficial do fabricante (domínio verificado). null quando não identificado.</summary>
    public static string? UrlOficialDriver(FabricanteGpu f) => f switch
    {
        FabricanteGpu.Nvidia => "https://www.nvidia.com/Download/index.aspx",
        FabricanteGpu.Amd => "https://www.amd.com/en/support",
        FabricanteGpu.Intel => "https://www.intel.com/content/www/us/en/download-center/home.html",
        _ => null,
    };

    /// <summary>Heurística: driver com mais de ~18 meses provavelmente está desatualizado.</summary>
    public static bool PareceAntigo(DateTime? dataDriver, DateTime agora) =>
        dataDriver is { } d && (agora - d).TotalDays > 540;
}

public sealed record InfoGpu(
    string Adaptador, string? VersaoDriver, DateTime? DataDriver, string? Vram, string? ModoAtual,
    FabricanteGpu Fabricante, string? UrlDriverOficial, bool DriverPossivelmenteAntigo);

/// <summary>Diagnóstico de GPU somente leitura (§21.5). Não baixa, não limpa driver, não altera flags.</summary>
public static class DiagnosticoGpu
{
    public static IReadOnlyList<InfoGpu> Coletar()
    {
        var lista = new List<InfoGpu>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Name,DriverVersion,DriverDate,AdapterRAM,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController"))
            {
                var nome = Wmi.Str(mo, "Name") ?? "GPU";
                var fab = DriverGpu.Detectar(nome);
                DateTime? data = null;
                try { if (mo["DriverDate"] is string ds && ds.Length > 0) data = ManagementDateTimeConverter.ToDateTime(ds); } catch { }
                string? vram = Wmi.U32(mo, "AdapterRAM") is { } ram && ram > 0 ? Diag.GB(ram) : null;
                string? modo = Wmi.U32(mo, "CurrentHorizontalResolution") is { } w && Wmi.U32(mo, "CurrentVerticalResolution") is { } h
                    ? $"{w}×{h} @ {Wmi.U32(mo, "CurrentRefreshRate")} Hz" : null;
                lista.Add(new InfoGpu(nome, Wmi.Str(mo, "DriverVersion"), data, vram, modo, fab,
                    DriverGpu.UrlOficialDriver(fab), DriverGpu.PareceAntigo(data, DateTime.Now)));
            }
        }
        catch { }
        return lista;
    }
}
