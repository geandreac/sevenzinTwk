using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Um identificador de hardware lido do Windows, ainda em texto (só existe na memória do app).</summary>
public sealed record ComponenteHw(string Nome, string? Valor);

/// <summary>Identidade do computador para vincular a conta a um PC (HWID). Lê quatro identificadores (MachineGuid do
/// Windows, UUID e serial da placa-mãe, ID do processador) e envia ao servidor só hashes SHA-256 com sal do app: o
/// valor bruto nunca sai do PC e o hash não serve para rastrear o PC em outro serviço.
/// O vínculo usa só as peças FORTES, UUID e serial da placa, que são únicos por máquina: o ID do processador se repete
/// em toda CPU do mesmo modelo e o MachineGuid é um valor de registro que qualquer um copia de um PC para outro.
/// Reinstalar o Windows ou trocar a CPU não muda o HWID; trocar a placa-mãe muda (liberação pelo admin).
/// Os dois fracos só entram quando a placa não traz nenhum identificador real.</summary>
public static class IdentidadeDispositivo
{
    const string Sal = "sevenzin-hwid-v1";

    // Valores de fábrica genéricos que muitas placas trazem no lugar do serial real: iguais em milhares de PCs,
    // então não identificam nada e não podem contar como "mesmo computador".
    static readonly HashSet<string> Genericos = new(StringComparer.OrdinalIgnoreCase)
    {
        "TO BE FILLED BY O.E.M.", "DEFAULT STRING", "NONE", "NOT APPLICABLE", "NOT SPECIFIED", "SYSTEM SERIAL NUMBER",
        "BASE BOARD SERIAL NUMBER", "N/A", "NA", "UNKNOWN", "O.E.M.", "OEM", "SERIAL", "INVALID",
    };

    /// <summary>Limpa um identificador: maiúsculas, sem espaços nas pontas. Devolve null para vazio, valor genérico
    /// de fábrica ou sequência de um caractere só (ex.: 00000000-0000-..., FFFFFFFF-FFFF-...). Pura.</summary>
    public static string? Normalizar(string? valor)
    {
        var v = (valor ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0 || Genericos.Contains(v)) return null;
        var semSeparador = v.Replace("-", "").Replace(" ", "").Replace(".", "");
        if (semSeparador.Length == 0 || semSeparador.All(c => c == semSeparador[0])) return null;
        return v;
    }

    static string Sha256Hex(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

    /// <summary>Hash de um componente: SHA-256 de "sal|nome|valor", em hex minúsculo (64 caracteres). Pura.</summary>
    public static string HashComponente(string nome, string valor) => Sha256Hex($"{Sal}|{nome}|{valor}");

    static readonly HashSet<string> Fortes = new(StringComparer.Ordinal) { "uuid", "placa" };

    /// <summary>Calcula o HWID (hash do conjunto) e a lista de hashes por componente, na ordem recebida.
    /// Componentes genéricos ou vazios ficam de fora; havendo peça forte (UUID ou serial da placa), só elas contam.
    /// Sem nenhum componente válido, lança exceção. Pura.</summary>
    public static (string Hwid, string[] Componentes) Calcular(IEnumerable<ComponenteHw> brutos)
    {
        var validos = brutos
            .Select(c => (c.Nome, Valor: Normalizar(c.Valor)))
            .Where(c => c.Valor is not null)
            .ToList();
        if (validos.Any(c => Fortes.Contains(c.Nome)))
            validos = validos.Where(c => Fortes.Contains(c.Nome)).ToList();
        var comps = validos.Select(c => HashComponente(c.Nome, c.Valor!)).ToArray();
        if (comps.Length == 0)
            throw new InvalidOperationException("Não foi possível identificar este computador.");
        return (Sha256Hex(Sal + "|" + string.Join("|", comps)), comps);
    }

    /// <summary>Lê os quatro identificadores deste PC. Cada leitura falha sozinha (fica null) sem derrubar as outras.</summary>
    public static IReadOnlyList<ComponenteHw> LerDoSistema() => new[]
    {
        new ComponenteHw("machineguid", Tentar(() =>
        {
            using var chave = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return chave?.GetValue("MachineGuid") as string;
        })),
        new ComponenteHw("uuid", Tentar(() => Wmi("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID"))),
        new ComponenteHw("placa", Tentar(() => Wmi("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber"))),
        new ComponenteHw("cpu", Tentar(() => Wmi("SELECT ProcessorId FROM Win32_Processor", "ProcessorId"))),
    };

    static string? Tentar(Func<string?> ler)
    {
        try { return ler(); } catch { return null; }
    }

    static string? Wmi(string consulta, string campo)
    {
        using var busca = new ManagementObjectSearcher(consulta);
        foreach (var o in busca.Get())
            using (o)
                return o[campo]?.ToString();
        return null;
    }

    /// <summary>HWID para mostrar ao usuário e ao suporte: os 16 primeiros caracteres do hash, em grupos
    /// (ex.: 3F9A-0C12-77BE-41D0). Basta para identificar o PC no painel do admin. Pura.</summary>
    public static string Exibir(string hwid) =>
        string.Join("-", Enumerable.Range(0, 4).Select(i => hwid.Substring(i * 4, 4))).ToUpperInvariant();

    static (string, string[])? _cache;

    /// <summary>HWID deste PC (calculado uma vez por execução: o hardware não muda com o app aberto).</summary>
    public static (string Hwid, string[] Componentes) Atual() => _cache ??= Calcular(LerDoSistema());
}
