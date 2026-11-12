using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F11: desinstalação assistida — localização dos setups oficiais (pura) e argumentos exatos.
/// Nenhum desinstalador é executado nos testes.</summary>
public class DesinstaladorAppsTests : IDisposable
{
    readonly string _raiz = Path.Combine(Path.GetTempPath(), "sevenzin-des-" + Guid.NewGuid().ToString("N"));

    public DesinstaladorAppsTests() => Directory.CreateDirectory(_raiz);

    public void Dispose() { try { Directory.Delete(_raiz, recursive: true); } catch { } }

    [Fact]
    public void EncontrarEdgeSetup_acha_o_installer_da_versao_e_devolve_null_sem_ele()
    {
        var versao = Path.Combine(_raiz, "120.0.0.0");
        var installer = Path.Combine(versao, "Installer");
        Directory.CreateDirectory(installer);
        File.WriteAllText(Path.Combine(installer, "setup.exe"), "fake");

        Assert.Equal(Path.Combine(installer, "setup.exe"), DesinstaladorApps.EncontrarEdgeSetup(_raiz));
        Assert.Null(DesinstaladorApps.EncontrarEdgeSetup(Path.Combine(_raiz, "SemEdge")));
    }

    [Fact]
    public void EncontrarOneDriveSetup_prefere_syswow64_e_cai_no_system32()
    {
        var syswow = Path.Combine(_raiz, "SysWOW64");
        var system = Path.Combine(_raiz, "System32");
        Directory.CreateDirectory(syswow);
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "OneDriveSetup.exe"), "fake");

        Assert.Equal(Path.Combine(system, "OneDriveSetup.exe"), DesinstaladorApps.EncontrarOneDriveSetup(_raiz)); // só System32

        File.WriteAllText(Path.Combine(syswow, "OneDriveSetup.exe"), "fake");
        Assert.Equal(Path.Combine(syswow, "OneDriveSetup.exe"), DesinstaladorApps.EncontrarOneDriveSetup(_raiz)); // SysWOW64 primeiro

        Assert.Null(DesinstaladorApps.EncontrarOneDriveSetup(Path.Combine(_raiz, "SemWindows")));
    }

    [Fact]
    public void Argumentos_de_desinstalacao_sao_os_oficiais()
    {
        Assert.Equal(new[] { "--uninstall", "--force-uninstall", "--system-level" }, DesinstaladorApps.ArgumentosEdge);
        Assert.Equal(new[] { "/uninstall" }, DesinstaladorApps.ArgumentosOneDrive);
    }
}
