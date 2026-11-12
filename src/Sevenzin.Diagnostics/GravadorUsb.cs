using System.Diagnostics;

namespace Sevenzin.Diagnostics;

/// <summary>Resultado da gravação do pendrive.</summary>
public sealed record ResultadoGravacaoUsb(bool Ok, string Detalhe);

/// <summary>Grava a mídia do Windows num pendrive (réplica do fluxo F7 do original, caminho FAT32):
/// prepara o disco (Clear-Disk/New-Partition/Format FAT32), copia os arquivos da ISO com SHA-256 e,
/// quando o install.wim passa de 4 GB (limite FAT32), divide em install.swm de 3.800 MB (DISM) —
/// exatamente como o original. Boot: UEFI (limitação registrada: sem BIOS/Legacy).
/// A gravação é DESTRUTIVA para o pendrive — a confirmação fica na UI, e o motor revalida SafeUsb.</summary>
public static class GravadorUsb
{
    const long LimiteFat32 = 4L * 1024 * 1024 * 1024;   // arquivo máximo em FAT32
    const long TamanhoParte = 3800L * 1024 * 1024;      // partes do SWM, como no original

    /// <summary>Prepara o disco: apaga partições, cria partição única FAT32 rotulada e devolve a letra.</summary>
    public static async Task<(string? Letra, string? Erro)> PrepararDiscoAsync(int numeroDisco, CancellationToken ct = default)
    {
        var script =
            $"Clear-Disk -Number {numeroDisco} -RemoveData -Confirm:$false -ErrorAction Stop; " +
            $"New-Partition -DiskNumber {numeroDisco} -UseMaximumSize -AssignDriveLetter | Out-Null; " +
            $"Format-Volume -FileSystem FAT32 -NewFileSystemLabel SEVENZIN -Confirm:$false -ErrorAction Stop | Out-Null; " +
            $"(Get-Partition -DiskNumber {numeroDisco} | Get-Volume).DriveLetter";
        var (codigo, saida) = await RodarPowerShellAsync(script, ct);
        if (codigo != 0) return (null, "Falha ao preparar o pendrive: " + saida.Trim());
        var letra = saida.Trim();
        return (letra.Length is 1 or 2 ? letra.ToUpperInvariant() : null, null);
    }

    public static async Task<ResultadoGravacaoUsb> GravarAsync(UsbRecord usb, string origemIso,
        Action<string>? etapa = null, CancellationToken ct = default)
    {
        void Etapa(string t) => etapa?.Invoke(t);

        if (!UsbMedia.Seguro(usb)) return new(false, "O pendrive não passou no filtro de segurança (SafeUsb) — nada foi gravado.");
        var revalidado = UsbMedia.Reidentificar(usb);
        if (revalidado is null) return new(false, "O pendrive não pôde ser reidentificado (UniqueId/Serial/Tamanho) — nada foi gravado.");
        usb = revalidado;

        string? letraIso = null;
        try
        {
            Etapa($"Preparando o pendrive {usb.Nome} (apaga TODO o conteúdo)…");
            var (letraUsb, erroPrep) = await PrepararDiscoAsync(usb.Number, ct);
            if (letraUsb is null) return new(false, erroPrep ?? "Sem letra de unidade no pendrive.");

            Etapa("Montando a ISO (somente leitura)…");
            letraIso = await MontarIsoAsync(origemIso, ct);
            if (letraIso is null) return new(false, "Não foi possível montar a ISO (exige administrador).");

            Etapa("Copiando os arquivos para o pendrive (verificando SHA-256)…");
            var raizOrigem = letraIso + @":\";
            var raizDestino = letraUsb + @":\";
            foreach (var arquivo in Directory.EnumerateFiles(raizOrigem, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relativo = Path.GetRelativePath(raizOrigem, arquivo);
                await CopiadorVerificado.CopiarAsync(arquivo, Path.Combine(raizDestino, relativo), ct: ct);
            }

            var wim = Path.Combine(raizDestino, "sources", "install.wim");
            if (File.Exists(wim) && new FileInfo(wim).Length > LimiteFat32)
            {
                Etapa("Dividindo o install.wim em partes de 3.800 MB (limite FAT32)…");
                var (codigo, saida) = await ExecutorDism.RodarDismAsync(new[]
                {
                    "/Split-Image", "/ImageFile:" + wim, "/SWMFile:" + Path.Combine(raizDestino, "sources", "install.swm"),
                    "/FileSize:" + TamanhoParte,
                }, ct);
                if (codigo != 0) return new(false, "DISM não dividiu o WIM: " + saida.Trim());
                File.Delete(wim);
            }

            Etapa("Pendrive pronto (boot UEFI).");
            return new(true, $"Pendrive gravado em {letraUsb}: (FAT32, boot UEFI — sem BIOS/Legacy, como o original alertava).");
        }
        catch (OperationCanceledException)
        {
            return new(false, "Operação cancelada.");
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
        finally
        {
            if (letraIso is not null) await DesmontarIsoAsync(origemIso, ct);
        }
    }

    static async Task<(int Codigo, string Saida)> RodarPowerShellAsync(string script, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);
        using var p = Process.Start(psi)!;
        var saida = await p.StandardOutput.ReadToEndAsync(ct);
        var erro = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, saida + (erro.Length > 0 ? "\n" + erro : ""));
    }

    static async Task<string?> MontarIsoAsync(string iso, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"$d=Mount-DiskImage -ImagePath '{iso.Replace("'", "''")}' -PassThru -Access ReadOnly; $v=$d | Get-Volume; Write-Output $v.DriveLetter");
        using var p = Process.Start(psi)!;
        var saida = (await p.StandardOutput.ReadToEndAsync(ct)).Trim();
        await p.WaitForExitAsync(ct);
        return saida.Length is 1 or 2 ? saida.ToUpperInvariant() : null;
    }

    static async Task DesmontarIsoAsync(string iso, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"Dismount-DiskImage -ImagePath '{iso.Replace("'", "''")}' -ErrorAction SilentlyContinue");
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync(ct);
    }
}
