using System.Diagnostics;
using System.Security.Cryptography;

namespace Sevenzin.Diagnostics;

/// <summary>Um disco físico visto pelo Windows (saída de Get-Disk).</summary>
public sealed record UsbRecord(int Number, string Nome, string Serial, long Tamanho, string UniqueId,
    string BusType, bool IsBoot, bool IsSystem, bool IsOffline, bool IsReadOnly);

/// <summary>Descoberta e filtro de segurança de pendrives — réplica do SafeUsb/CheckUsb do original
/// (05_metricas §9 / 06_fluxos F7: BusType=USB, nunca boot/sistema/offline/readonly, ≥ 8 GB,
/// UniqueId presente). Só leitura até o usuário confirmar a gravação.</summary>
public static class UsbMedia
{
    /// <summary>Parse da saída de `Get-Disk` (linhas "n|nome|serial|tamanho|uniqueid|bustype|isboot|issystem|isoffline|isreadonly").
    /// Função pura (testável).</summary>
    public static List<UsbRecord> AnalisarSaidaGetDisk(string saida)
    {
        var discos = new List<UsbRecord>();
        foreach (var linha in saida.Split('\n'))
        {
            var partes = linha.Trim().Split('|');
            if (partes.Length < 10) continue;
            if (!int.TryParse(partes[0], out var numero)) continue;
            if (!long.TryParse(partes[3], out var tamanho)) continue;
            discos.Add(new UsbRecord(
                numero, partes[1], partes[2], tamanho, partes[4], partes[5],
                bool.TryParse(partes[6], out var b) && b,
                bool.TryParse(partes[7], out var s) && s,
                bool.TryParse(partes[8], out var o) && o,
                bool.TryParse(partes[9], out var r) && r));
        }
        return discos;
    }

    /// <summary>Filtro SafeUsb (réplica literal da lógica do original): só USB removível, sem papel de
    /// boot/sistema, online, gravável, ≥ 8 GB e com identificação estável. Função pura (testável).</summary>
    public static bool Seguro(UsbRecord d) =>
        d.BusType.Equals("USB", StringComparison.OrdinalIgnoreCase)
        && !d.IsBoot && !d.IsSystem && !d.IsOffline && !d.IsReadOnly
        && d.Tamanho >= 8L * 1024 * 1024 * 1024
        && !string.IsNullOrWhiteSpace(d.UniqueId);

    /// <summary>Lista os discos USB da máquina (Get-Disk via PowerShell). Lista vazia quando não há.</summary>
    public static List<UsbRecord> Listar()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("Get-Disk | ForEach-Object { \"{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}\" -f $_.Number,$_.FriendlyName,$_.SerialNumber,$_.Size,$_.UniqueId,$_.BusType,$_.IsBoot,$_.IsSystem,$_.IsOffline,$_.IsReadOnly }");
            using var p = Process.Start(psi)!;
            var saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return AnalisarSaidaGetDisk(saida).Where(Seguro).ToList();
        }
        catch { return new List<UsbRecord>(); }
    }

    /// <summary>Reidentifica um pendrive após replugar (números mudam): bate UniqueId + Serial + Tamanho,
    /// como o CheckUsb do original. null quando não há correspondência única.</summary>
    public static UsbRecord? Reidentificar(UsbRecord esperado)
    {
        try
        {
            var todos = Listar();
            var batem = todos.Where(d =>
                d.UniqueId.Equals(esperado.UniqueId, StringComparison.OrdinalIgnoreCase)
                && d.Serial.Equals(esperado.Serial, StringComparison.OrdinalIgnoreCase)
                && d.Tamanho == esperado.Tamanho).ToList();
            return batem.Count == 1 ? batem[0] : null;
        }
        catch { return null; }
    }
}

/// <summary>Cópia com verificação SHA-256 (réplica do MediaTransfer.cs do original: blocos + verificação
/// antes de dar por concluído). Lança <see cref="IOException"/> se o hash do destino não bater.</summary>
public static class CopiadorVerificado
{
    public static async Task<long> CopiarAsync(string origem, string destino, IProgress<double>? progresso = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using var entrada = File.OpenRead(origem);
        string esperado;
        long total, lido = 0;
        await using (var saida = File.Create(destino))
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024]; // blocos de 1 MB, como o original
            total = entrada.Length;
            int n;
            while ((n = await entrada.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, n);
                await saida.WriteAsync(buffer.AsMemory(0, n), ct);
                lido += n;
                if (total > 0) progresso?.Report((double)lido / total);
            }
            await saida.FlushAsync(ct);
            esperado = Convert.ToHexString(hash.GetHashAndReset());
        }

        await using var conferido = File.OpenRead(destino);
        using var hashDestino = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bufferV = new byte[1024 * 1024];
        int m;
        while ((m = await conferido.ReadAsync(bufferV, ct)) > 0) hashDestino.AppendData(bufferV, 0, m);
        string atual = Convert.ToHexString(hashDestino.GetHashAndReset());
        if (!esperado.Equals(atual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destino);
            throw new IOException($"Verificação SHA-256 falhou ao copiar {origem}.");
        }
        return lido;
    }
}
