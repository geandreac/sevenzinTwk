using System.Reflection;

namespace Sevenzin.Diagnostics;

/// <summary>Criação de ISO bootável via IMAPI2FS (COM embutido no Windows — sem ADK, sem binários GPL).
/// A imagem é gravável por UEFI usando o <c>efisys.bin</c> que já existe DENTRO da ISO de origem
/// (efi\microsoft\boot\efisys.bin). Limitação registrada (como no original): sem boot BIOS/Legacy.</summary>
public static class CriadorIso
{
    /// <summary>Cria a ISO UEFI-bootável a partir de uma pasta de conteúdo. Lança em falha do IMAPI.
    /// <paramref name="bootEfisys"/> = caminho do efisys.bin (já copiado para o staging).</summary>
    public static void CriarIsoUefi(string pastaConteudo, string destinoIso, string bootEfisys, string rotulo)
    {
        try
        {
            var tipo = Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage") ?? throw new InvalidOperationException("IMAPI2FS indisponível neste Windows.");
            dynamic img = Activator.CreateInstance(tipo)!;

            // ISO9660 + UDF (arquivos grandes, caminhos longos) — padrão da mídia do Windows.
            img.FileSystemsToCreate = 6;   // FsiFileSystemISO9660 (4) | FsiFileSystemUDF (2)
            img.UDFRevision = 0x102;       // UDF 1.02
            img.VolumeName = rotulo;
            img.FreeMediaBlocks = 0;       // dimensiona sozinho pelo conteúdo

            var raiz = img.Root;
            raiz.AddTree(pastaConteudo, false);

            // Boot UEFI: PlatformId 0xEF com o efisys da própria mídia de origem.
            dynamic boot = img.BootImageOptions;
            boot.BootImage = bootEfisys;
            boot.Emulation = 0;            // sem emulação (disco rígido)
            boot.PlatformId = 0xEF;        // UEFI
            boot.ImageSize = 0;            // setor 4 padrão do efisys

            dynamic resultado = img.CreateResultImage();
            using var stream = (System.IO.Stream)resultado.ImageStream;
            using var destino = File.Create(destinoIso);
            stream.CopyTo(destino);
        }
        catch (Exception ex)
        {
            throw new IOException("Falha ao criar a ISO: " + ex.Message, ex);
        }
    }
}

/// <summary>Resultado da construção da ISO personalizada.</summary>
public sealed record ResultadoConstrutorIso(bool Ok, string? Destino, string? Erro);

/// <summary>Construção da ISO personalizada (réplica do fluxo F6 do original, subconjunto seguro):
/// monta a origem (read-only), copia os arquivos (com SHA-256), exporta SÓ a edição escolhida via
/// DISM (/Export-Image, compressão máxima), cria a ISO UEFI-bootável e desmonta/limpa ao final.
/// Nada do sistema é alterado — apenas arquivos de trabalho e o ISO de saída.</summary>
public static class ConstrutorIso
{
    /// <summary>Valida o pedido de construção. Função pura (testável). Devolve null = válido.</summary>
    public static string? ValidarPedido(string? origem, string? destino, int indiceEdicao)
    {
        if (indiceEdicao < 1) return "Escolha uma edição para exportar.";
        if (string.IsNullOrWhiteSpace(destino)) return "Escolha onde salvar a nova ISO.";
        if (!destino.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return "O destino precisa ter extensão .iso.";
        if (File.Exists(destino)) return "Já existe um arquivo no destino — escolha um nome novo (nada é sobrescrito).";
        var erroOrigem = ExecutorDism.ValidarIso(origem);
        if (erroOrigem is not null) return erroOrigem;
        return null;
    }

    public static async Task<ResultadoConstrutorIso> BuildAsync(string origemIso, string destinoIso, int indiceEdicao,
        Action<string>? etapa = null, CancellationToken ct = default,
        IReadOnlyList<string>? appsRemover = null, IReadOnlyList<string>? featuresRemover = null, string? pastaDrivers = null)
    {
        void Etapa(string texto) => etapa?.Invoke(texto);

        var validacao = ValidarPedido(origemIso, destinoIso, indiceEdicao);
        if (validacao is not null) return new(false, null, validacao);

        string? letra = null;
        var staging = Path.Combine(Path.GetTempPath(), "sevenzin-iso-" + Guid.NewGuid().ToString("N"));
        try
        {
            Etapa("Montando a ISO de origem (somente leitura)…");
            letra = await MontarIsoAsync(origemIso, ct);
            if (letra is null) return new(false, null, "Não foi possível montar a ISO (exige administrador).");

            var wim = new[] { @"sources\install.wim", @"sources\install.esd" }
                .Select(p => Path.Combine(letra + @":\", p))
                .FirstOrDefault(File.Exists);
            if (wim is null) return new(false, null, "install.wim/install.esd não encontrado na ISO.");

            Directory.CreateDirectory(staging);
            Etapa("Copiando os arquivos de instalação (verificando SHA-256)…");
            var raizOrigem = letra + @":\";
            var wimRelativo = Path.GetRelativePath(raizOrigem, wim);
            foreach (var arquivo in Directory.EnumerateFiles(raizOrigem, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relativo = Path.GetRelativePath(raizOrigem, arquivo);
                // o install.wim/esd da origem é substituído pela exportação da edição escolhida
                if (relativo.Equals(wimRelativo, StringComparison.OrdinalIgnoreCase)) continue;
                await CopiadorVerificado.CopiarAsync(arquivo, Path.Combine(staging, relativo), ct: ct);
            }

            Etapa($"Exportando somente a edição {indiceEdicao} (DISM, compressão máxima)…");
            var destinoWim = Path.Combine(staging, "sources", "install.wim");
            var (codigo, saida) = await ExecutorDism.RodarDismAsync(new[]
            {
                "/Export-Image", "/SourceImageFile:" + wim, "/SourceIndex:" + indiceEdicao,
                "/DestinationImageFile:" + destinoWim, "/Compress:max",
            }, ct);
            if (codigo != 0) return new(false, null, "DISM não exportou a edição: " + saida.Trim());

            // Serviço da imagem (F12): montar o WIM exportado (índice 1 — exportação única) em RW,
            // remover apps/features escolhidos, adicionar drivers INF assinados e gravar (/Commit).
            bool precisaServico = (appsRemover?.Count ?? 0) + (featuresRemover?.Count ?? 0) > 0 || !string.IsNullOrEmpty(pastaDrivers);
            if (precisaServico)
            {
                var mount = Path.Combine(Path.GetTempPath(), "sevenzin-mount-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(mount);
                Etapa("Montando a imagem para serviço (leitura/escrita)…");
                var (cMont, sMont) = await ExecutorDism.MontarWimAsync(destinoWim, 1, mount, ct);
                if (cMont != 0) return new(false, null, "DISM não montou a imagem: " + sMont.Trim());
                try
                {
                    foreach (var app in appsRemover ?? Array.Empty<string>())
                    {
                        Etapa($"Removendo {app} da imagem…");
                        var (cA, sA) = await ExecutorDism.RemoverAppxDaImagemAsync(mount, app, ct);
                        if (cA != 0) { await ExecutorDism.DescartarWimAsync(mount, ct); return new(false, null, "Falha ao remover app: " + sA.Trim()); }
                    }
                    foreach (var feature in featuresRemover ?? Array.Empty<string>())
                    {
                        Etapa($"Removendo o recurso {feature} da imagem…");
                        var (cF, sF) = await ExecutorDism.RemoverFeatureDaImagemAsync(mount, feature, ct);
                        if (cF != 0) { await ExecutorDism.DescartarWimAsync(mount, ct); return new(false, null, "Falha ao remover recurso: " + sF.Trim()); }
                    }
                    if (!string.IsNullOrEmpty(pastaDrivers))
                    {
                        Etapa("Adicionando os drivers INF assinados à imagem…");
                        var (cD, sD) = await ExecutorDism.AdicionarDriversAsync(mount, pastaDrivers!, ct);
                        if (cD != 0) { await ExecutorDism.DescartarWimAsync(mount, ct); return new(false, null, "Falha ao adicionar drivers: " + sD.Trim()); }
                    }
                    Etapa("Gravando as alterações na imagem (/Commit)…");
                    var (cS, sS) = await ExecutorDism.SalvarWimAsync(mount, ct);
                    if (cS != 0) return new(false, null, "Falha ao gravar a imagem: " + sS.Trim());
                }
                catch
                {
                    try { await ExecutorDism.DescartarWimAsync(mount, ct); } catch { }
                    throw;
                }
            }

            Etapa("Criando a ISO com boot UEFI…");
            var efisys = Path.Combine(staging, "efi", "microsoft", "boot", "efisys.bin");
            if (!File.Exists(efisys)) return new(false, null, "efisys.bin não encontrado na ISO de origem — mídia não suportada.");
            CriadorIso.CriarIsoUefi(staging, destinoIso, efisys, "SEVENZIN");

            return new(true, destinoIso, null);
        }
        catch (OperationCanceledException)
        {
            return new(false, null, "Operação cancelada.");
        }
        catch (Exception ex)
        {
            return new(false, null, ex.Message);
        }
        finally
        {
            if (letra is not null) await DesmontarIsoAsync(origemIso, ct);
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
        }
    }

    static async Task<string?> MontarIsoAsync(string iso, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"$d=Mount-DiskImage -ImagePath '{iso.Replace("'", "''")}' -PassThru -Access ReadOnly; $v=$d | Get-Volume; Write-Output $v.DriveLetter");
        using var p = System.Diagnostics.Process.Start(psi)!;
        var saida = (await p.StandardOutput.ReadToEndAsync(ct)).Trim();
        await p.WaitForExitAsync(ct);
        return saida.Length is 1 or 2 ? saida.ToUpperInvariant() : null;
    }

    static async Task DesmontarIsoAsync(string iso, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"Dismount-DiskImage -ImagePath '{iso.Replace("'", "''")}' -ErrorAction SilentlyContinue");
        using var p = System.Diagnostics.Process.Start(psi)!;
        await p.WaitForExitAsync(ct);
    }
}
