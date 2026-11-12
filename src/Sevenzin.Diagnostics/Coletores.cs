using System.Diagnostics;
using System.Management;

namespace Sevenzin.Diagnostics;

/// <summary>Acesso só-leitura ao WMI e helpers de leitura tolerantes a falha.</summary>
internal static class Wmi
{
    public static IEnumerable<ManagementObject> Query(string wql, string scope = @"root\cimv2")
    {
        using var s = new ManagementObjectSearcher(scope, wql);
        foreach (ManagementObject mo in s.Get()) yield return mo;
    }
    public static string? Str(ManagementObject mo, string p) { try { return mo[p]?.ToString()?.Trim() is { Length: > 0 } v ? v : null; } catch { return null; } }
    public static ulong? U64(ManagementObject mo, string p) { try { return mo[p] is null ? null : Convert.ToUInt64(mo[p]); } catch { return null; } }
    public static uint? U32(ManagementObject mo, string p) { try { return mo[p] is null ? null : Convert.ToUInt32(mo[p]); } catch { return null; } }
}

internal static class Diag
{
    public static ItemInventario Falha(string cat, Exception e) =>
        new() { Categoria = cat, Nome = "(indisponível)", Fonte = "erro de coleta", Tipo = Confianca.Hipotese, Observacao = e.Message };
    public static string GB(ulong bytes) => $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
}

public static class ColetorCpu
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "CPU"; var r = new List<ItemInventario>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Name,Manufacturer,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,L2CacheSize,L3CacheSize FROM Win32_Processor"))
            {
                r.Add(new() { Categoria = cat, Nome = "Modelo", Valor = Wmi.Str(mo, "Name"), Fonte = "WMI Win32_Processor" });
                r.Add(new() { Categoria = cat, Nome = "Fabricante", Valor = Wmi.Str(mo, "Manufacturer"), Fonte = "WMI Win32_Processor" });
                var cores = Wmi.U32(mo, "NumberOfCores"); var logical = Wmi.U32(mo, "NumberOfLogicalProcessors");
                r.Add(new() { Categoria = cat, Nome = "Núcleos físicos", Valor = cores?.ToString(), Fonte = "WMI Win32_Processor" });
                r.Add(new() { Categoria = cat, Nome = "Threads lógicas", Valor = logical?.ToString(), Fonte = "WMI Win32_Processor" });
                if (cores is > 0 && logical is > 0)
                    r.Add(new() { Categoria = cat, Nome = "SMT / Hyper-Threading", Valor = logical > cores ? "ativo" : "inativo ou ausente", Fonte = "derivado de núcleos × threads", Tipo = Confianca.Inferencia });
                if (Wmi.U32(mo, "MaxClockSpeed") is { } mhz)
                    r.Add(new() { Categoria = cat, Nome = "Clock nominal", Valor = $"{mhz} MHz", Fonte = "WMI Win32_Processor", Observacao = "valor nominal; não é o clock efetivo em carga" });
                if (Wmi.U32(mo, "L3CacheSize") is { } l3) r.Add(new() { Categoria = cat, Nome = "Cache L3", Valor = $"{l3} KB", Fonte = "WMI Win32_Processor" });
                if (TopologiaCpu.Ler() is { } topo)
                {
                    const string fonte = "Win32 GetLogicalProcessorInformationEx";
                    r.Add(topo.Hibrido
                        ? new() { Categoria = cat, Nome = "Núcleos P/E", Valor = $"{topo.PCores} de performance + {topo.ECores} de eficiência", Fonte = $"{fonte} (EfficiencyClass)" }
                        : new() { Categoria = cat, Nome = "Núcleos P/E", Valor = "homogêneo (sem divisão P/E)", Fonte = $"{fonte} (EfficiencyClass)" });
                    if (topo.DominiosL3 > 0)
                        r.Add(new() { Categoria = cat, Nome = "Domínios de cache L3", Valor = topo.DominiosL3.ToString(), Fonte = fonte, Tipo = Confianca.Inferencia, Observacao = "≈ nº de CCDs em CPUs AMD Ryzen; em Intel monolíticas é 1" });
                }
                else
                    r.Add(new() { Categoria = cat, Nome = "Núcleos P/E e CCD", Valor = null, Fonte = "—", Tipo = Confianca.Hipotese, Observacao = "API de topologia indisponível nesta máquina" });
            }
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}

public static class ColetorGpu
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "GPU"; var r = new List<ItemInventario>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Name,DriverVersion,DriverDate,AdapterRAM,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController"))
            {
                var nome = Wmi.Str(mo, "Name");
                r.Add(new() { Categoria = cat, Nome = "Adaptador", Valor = nome, Fonte = "WMI Win32_VideoController" });
                r.Add(new() { Categoria = cat, Nome = "Versão do driver", Valor = Wmi.Str(mo, "DriverVersion"), Fonte = "WMI Win32_VideoController" });
                if (Wmi.U32(mo, "AdapterRAM") is { } ram)
                    r.Add(new() { Categoria = cat, Nome = "VRAM reportada", Valor = Diag.GB(ram), Fonte = "WMI Win32_VideoController", Tipo = Confianca.Hipotese, Observacao = "WMI satura AdapterRAM acima de 4 GB; confirmar por API do fabricante" });
                if (Wmi.U32(mo, "CurrentHorizontalResolution") is { } w && Wmi.U32(mo, "CurrentVerticalResolution") is { } h)
                    r.Add(new() { Categoria = cat, Nome = "Modo atual", Valor = $"{w}×{h} @ {Wmi.U32(mo, "CurrentRefreshRate")} Hz", Fonte = "WMI Win32_VideoController" });
            }
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}

public static class ColetorMemoria
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "Memória"; var r = new List<ItemInventario>();
        try
        {
            int pentes = 0; ulong total = 0; var speeds = new HashSet<uint>();
            foreach (var mo in Wmi.Query("SELECT Capacity,ConfiguredClockSpeed,Speed FROM Win32_PhysicalMemory"))
            {
                pentes++;
                total += Wmi.U64(mo, "Capacity") ?? 0;
                if ((Wmi.U32(mo, "ConfiguredClockSpeed") ?? Wmi.U32(mo, "Speed")) is { } s) speeds.Add(s);
            }
            r.Add(new() { Categoria = cat, Nome = "Pentes instalados", Valor = pentes.ToString(), Fonte = "WMI Win32_PhysicalMemory" });
            if (total > 0) r.Add(new() { Categoria = cat, Nome = "Capacidade total", Valor = Diag.GB(total), Fonte = "WMI Win32_PhysicalMemory" });
            if (speeds.Count > 0) r.Add(new() { Categoria = cat, Nome = "Frequência configurada", Valor = string.Join(" / ", speeds.OrderBy(x => x).Select(x => $"{x} MT/s")), Fonte = "WMI Win32_PhysicalMemory" });
            r.Add(new() { Categoria = cat, Nome = "Canais (dual/quad)", Valor = null, Fonte = "—", Tipo = Confianca.Hipotese, Observacao = "não confirmado: o modo de canais não é exposto de forma confiável pelo WMI" });
            foreach (var mo in Wmi.Query("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem"))
            {
                if (Wmi.U64(mo, "FreePhysicalMemory") is { } freeKb && Wmi.U64(mo, "TotalVisibleMemorySize") is { } totKb && totKb > 0)
                    r.Add(new() { Categoria = cat, Nome = "Disponível agora", Valor = $"{Diag.GB(freeKb * 1024)} de {Diag.GB(totKb * 1024)} ({100.0 * freeKb / totKb:0}%)", Fonte = "WMI Win32_OperatingSystem", Observacao = "instantâneo; varia com a carga" });
            }
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}

public static class ColetorSistema
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "Sistema"; var r = new List<ItemInventario>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Caption,Version,BuildNumber,OSArchitecture FROM Win32_OperatingSystem"))
            {
                r.Add(new() { Categoria = cat, Nome = "Windows", Valor = Wmi.Str(mo, "Caption"), Fonte = "WMI Win32_OperatingSystem" });
                r.Add(new() { Categoria = cat, Nome = "Versão / build", Valor = $"{Wmi.Str(mo, "Version")} (build {Wmi.Str(mo, "BuildNumber")})", Fonte = "WMI Win32_OperatingSystem" });
                r.Add(new() { Categoria = cat, Nome = "Arquitetura", Valor = Wmi.Str(mo, "OSArchitecture"), Fonte = "WMI Win32_OperatingSystem" });
            }
            foreach (var mo in Wmi.Query("SELECT SMBIOSBIOSVersion,Manufacturer FROM Win32_BIOS"))
                r.Add(new() { Categoria = cat, Nome = "BIOS/UEFI", Valor = $"{Wmi.Str(mo, "Manufacturer")} {Wmi.Str(mo, "SMBIOSBIOSVersion")}", Fonte = "WMI Win32_BIOS" });
            foreach (var mo in Wmi.Query("SELECT Manufacturer,Product FROM Win32_BaseBoard"))
                r.Add(new() { Categoria = cat, Nome = "Placa-mãe", Valor = $"{Wmi.Str(mo, "Manufacturer")} {Wmi.Str(mo, "Product")}", Fonte = "WMI Win32_BaseBoard" });
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}

public static class ColetorArmazenamento
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "Armazenamento"; var r = new List<ItemInventario>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Model,Size,MediaType FROM Win32_DiskDrive"))
                r.Add(new() { Categoria = cat, Nome = "Disco", Valor = $"{Wmi.Str(mo, "Model")} — {(Wmi.U64(mo, "Size") is { } sz ? Diag.GB(sz) : "?")}", Fonte = "WMI Win32_DiskDrive" });
            foreach (var mo in Wmi.Query("SELECT DeviceID,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
                if (Wmi.U64(mo, "Size") is { } sz && sz > 0 && Wmi.U64(mo, "FreeSpace") is { } free)
                    r.Add(new() { Categoria = cat, Nome = $"Volume {Wmi.Str(mo, "DeviceID")}", Valor = $"{Diag.GB(free)} livres de {Diag.GB(sz)}", Fonte = "WMI Win32_LogicalDisk" });
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}

public static class ColetorEnergia
{
    public static List<ItemInventario> Coletar()
    {
        const string cat = "Energia"; var r = new List<ItemInventario>();
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/getactivescheme") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            var outp = p.StandardOutput.ReadToEnd(); p.WaitForExit();
            var m = System.Text.RegularExpressions.Regex.Match(outp, @"\(([^)]*)\)");
            r.Add(new() { Categoria = cat, Nome = "Plano de energia ativo", Valor = m.Success ? m.Groups[1].Value : outp.Trim(), Fonte = "powercfg /getactivescheme (só leitura)" });
        }
        catch (Exception e) { r.Add(Diag.Falha(cat, e)); }
        return r;
    }
}
