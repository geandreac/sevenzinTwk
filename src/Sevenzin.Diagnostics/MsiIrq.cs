using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Um dispositivo de interrupção: nome, fabricante, driver, IRQ, estado MSI (Message Signaled Interrupts)
/// e prioridade de interrupção lidos em modo somente leitura.</summary>
public sealed record DispositivoIrq(
    string Nome, string? Fabricante, string? Driver, uint? Irq, bool? Msi, string? PrioridadeIrq = null);

/// <summary>Diagnóstico de MSI/IRQ (§5.3): lista dispositivos com IRQ e inspeciona o estado real de MSI Mode
/// (<c>MSISupported</c>) e <c>DevicePriority</c> nos dispositivos PCI/PCIe via registro (SOMENTE LEITURA).
/// Não altera <c>MSISupported</c>/<c>DevicePriority</c> (risco de travamento/BSOD por dispositivo).</summary>
public static class MsiIrq
{
    /// <summary>Interpreta os valores brutos de registro de um dispositivo PCI (MSISupported e DevicePriority).
    /// Função pura/testável.</summary>
    public static (bool? MsiAtivo, string Prioridade) InterpretarMsi(int? msiSupported, int? devicePriority)
    {
        bool? msi = msiSupported switch { 1 => true, 0 => false, _ => null };
        string prio = devicePriority switch
        {
            1 => "Baixa",
            2 => "Normal",
            3 => "Alta",
            _ => "Padrão",
        };
        return (msi, prio);
    }

    /// <summary>Limpa prefixos do formato INF do Windows (ex.: <c>@oem12.inf,%dev%;NVIDIA GeForce...</c> → <c>NVIDIA GeForce...</c>).
    /// Função pura/testável.</summary>
    public static string LimparDescricaoInf(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return "Dispositivo PCI";
        var s = bruto.Trim();
        int idx = s.LastIndexOf(';');
        return idx >= 0 && idx + 1 < s.Length ? s[(idx + 1)..].Trim() : s;
    }

    /// <summary>Lista dispositivos com IRQ via Win32_IRQResource (best-effort, somente leitura).</summary>
    public static IReadOnlyList<DispositivoIrq> Listar()
    {
        var lista = new List<DispositivoIrq>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT * FROM Win32_IRQResource"))
            {
                var irq = Wmi.U32(mo, "IRQNumber");
                var nome = Wmi.Str(mo, "Name") ?? Wmi.Str(mo, "Caption") ?? "Dispositivo";
                lista.Add(new DispositivoIrq(nome, null, null, irq, null));
            }
        }
        catch { }
        return lista.OrderBy(d => d.Irq).ToList();
    }

    /// <summary>Inspeciona dispositivos PCI/PCIe no registro (<c>HKLM\SYSTEM\CurrentControlSet\Enum\PCI</c>,
    /// somente leitura) que possuem chave <c>Interrupt Management</c>, retornando o estado real de MSI Mode e
    /// prioridade de interrupção.</summary>
    public static IReadOnlyList<DispositivoIrq> ListarPciMsi()
    {
        var lista = new List<DispositivoIrq>();
        try
        {
            using var pci = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI", writable: false);
            if (pci is null) return lista;
            foreach (var hwId in pci.GetSubKeyNames())
            {
                using var hwKey = pci.OpenSubKey(hwId, writable: false);
                if (hwKey is null) continue;
                foreach (var inst in hwKey.GetSubKeyNames())
                {
                    using var instKey = hwKey.OpenSubKey(inst, writable: false);
                    if (instKey is null) continue;
                    using var irqMgmt = instKey.OpenSubKey(@"Device Parameters\Interrupt Management", writable: false);
                    if (irqMgmt is null) continue;

                    using var msiKey = irqMgmt.OpenSubKey("MessageSignaledInterruptProperties", writable: false);
                    using var affKey = irqMgmt.OpenSubKey("Affinity Policy", writable: false);
                    int? msiVal = msiKey?.GetValue("MSISupported") as int?;
                    int? prioVal = affKey?.GetValue("DevicePriority") as int?;
                    if (msiVal is null && prioVal is null) continue;

                    var nome = LimparDescricaoInf(instKey.GetValue("FriendlyName") as string ?? instKey.GetValue("DeviceDesc") as string);
                    var fab = LimparDescricaoInf(instKey.GetValue("Mfg") as string);
                    var svc = instKey.GetValue("Service") as string;
                    var (msi, prio) = InterpretarMsi(msiVal, prioVal);
                    lista.Add(new DispositivoIrq(nome, fab, svc, null, msi, prio));
                }
            }
        }
        catch { }
        return lista
            .OrderByDescending(d => d.Msi == true)
            .ThenBy(d => d.Nome)
            .ToList();
    }

    /// <summary>Dispositivos PnP gerais (Win32_PnPEntity) com fabricante e driver — leitura pura.</summary>
    public static IReadOnlyList<DispositivoIrq> ListarPci()
    {
        var lista = new List<DispositivoIrq>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Name,Manufacturer,Service FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0"))
            {
                var nome = Wmi.Str(mo, "Name");
                if (string.IsNullOrEmpty(nome)) continue;
                lista.Add(new DispositivoIrq(nome, Wmi.Str(mo, "Manufacturer"), Wmi.Str(mo, "Service"), null, null));
            }
        }
        catch { }
        return lista.OrderBy(d => d.Fabricante).ThenBy(d => d.Nome).ToList();
    }
}
