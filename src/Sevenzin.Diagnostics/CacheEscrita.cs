using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Um disco físico com o caminho de registro onde fica o ajuste de cache de escrita
/// (valor <c>CacheIsPowerProtected</c> — paridade com o original, 05_metricas §7).</summary>
public sealed record DiscoCache(string Modelo, string PnP, string CaminhoParametros, int? CacheForcadoAtual);

/// <summary>Write-cache forçado por disco (<c>CacheIsPowerProtected=1</c>): descobre os discos via WMI
/// e resolve o nó <c>HKLM\SYSTEM\CurrentControlSet\Enum\&lt;PnP&gt;\&lt;instância&gt;\Device Parameters</c>.
/// A escrita em si é feita pelo <see cref="MotorTweaks"/> (tweak dinâmico por disco: prévia/backup/desfazer/
/// auditoria + confirmação na UI). Risco ALTO: perda de dados em queda de energia.</summary>
public static class CacheEscrita
{
    /// <summary>Prefixo do nó Enum para um PnPDeviceID. Função pura (testável).</summary>
    public static string CaminhoEnumDePnP(string pnp) => @"SYSTEM\CurrentControlSet\Enum\" + pnp;

    /// <summary>Discos com nó "Device Parameters" resolvível (onde o valor existe ou pode ser criado).
    /// Discos sem nó (removíveis, incomuns) ficam de fora — nunca um valor inventado.</summary>
    public static List<DiscoCache> Listar()
    {
        var discos = new List<DiscoCache>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT Model,PNPDeviceID FROM Win32_DiskDrive"))
            {
                var modelo = Wmi.Str(mo, "Model") ?? "(disco sem modelo)";
                var pnp = Wmi.Str(mo, "PNPDeviceID");
                if (pnp is null) continue;
                var caminho = ResolverCaminhoParametros(pnp);
                if (caminho is null) continue;
                int? atual = null;
                try
                {
                    using var dp = Registry.LocalMachine.OpenSubKey(caminho);
                    atual = dp?.GetValue("CacheIsPowerProtected") is int v ? v : null;
                }
                catch { }
                discos.Add(new DiscoCache(modelo, pnp, caminho, atual));
            }
        }
        catch { /* WMI indisponível */ }
        return discos;
    }

    /// <summary>Resolve o caminho completo de "Device Parameters" para um PnPDeviceID, ou null se não houver.</summary>
    public static string? ResolverCaminhoParametros(string pnp)
    {
        try
        {
            var raiz = CaminhoEnumDePnP(pnp);
            using var k = Registry.LocalMachine.OpenSubKey(raiz);
            if (k is null) return null;
            foreach (var instancia in k.GetSubKeyNames())
            {
                var caminho = $@"{raiz}\{instancia}\Device Parameters";
                using var dp = Registry.LocalMachine.OpenSubKey(caminho);
                if (dp is not null) return caminho;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Constrói o tweak dinâmico do disco (aplicável pelo MotorTweaks, com backup/desfazer/auditoria).
    /// O Id é estável por PnP — o backup sobrevive a reinícios e a troca de instância.</summary>
    public static Tweak TweakParaDisco(DiscoCache d) => new(
        Id: "write-cache-" + SanitizarId(d.PnP),
        Nome: $"Write-cache forçado — {d.Modelo}",
        Descricao: "Marca o cache do disco como protegido contra queda de energia (CacheIsPowerProtected=1).",
        Categoria: "Segurança",
        Colmeia: Colmeia.MaquinaLocal,
        Caminho: d.CaminhoParametros,
        NomeValor: "CacheIsPowerProtected",
        ValorOtimizado: 1,
        Risco: "ALTO — risco de perda de dados em queda de energia",
        ExigeAdmin: true,
        ExigeConfirmacao: true);

    /// <summary>Id estável e seguro para o backup (SHA-256 do PnP, 32 hex — sem caracteres especiais).</summary>
    public static string SanitizarId(string pnp)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pnp));
        return Convert.ToHexString(hash)[..32];
    }
}
