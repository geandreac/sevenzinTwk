using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Uma edição do Windows dentro de um arquivo WIM/ESD (resultado de `dism /Get-WimInfo`).</summary>
public sealed record EdicaoImagem(int Indice, string Nome, string Descricao, string? Versao, string? Arquitetura, long TamanhoBytes);

/// <summary>App provisionado dentro da imagem (resultado de `dism /Get-AppxProvisionedPackages`).</summary>
public sealed record AppImagem(string Pacote);

/// <summary>Recurso opcional dentro da imagem (resultado de `dism /Get-WindowsOptionalFeature`).</summary>
public sealed record FeatureImagem(string Nome, string Estado);

/// <summary>Parsers PUROS da saída do DISM (testáveis com saída real de máquina). A execução em si fica
/// em <see cref="ExecutorDism"/> — separada, com confirmação e elevação na UI.</summary>
public static class AnalisadorImagem
{
    /// <summary>Parse de `dism /Get-WimInfo`: blocos "Index/Name/Description/Version/Architecture/Size".</summary>
    public static List<EdicaoImagem> AnalisarSaidaWimInfo(string saida)
    {
        var edicoes = new List<EdicaoImagem>();
        int? indice = null; string nome = "", descricao = "", versao = null, arquitetura = null; long tamanho = 0;
        void Fechar()
        {
            if (indice is { } i && nome.Length > 0)
                edicoes.Add(new EdicaoImagem(i, nome, descricao, versao, arquitetura, tamanho));
            indice = null; nome = ""; descricao = ""; versao = null; arquitetura = null; tamanho = 0;
        }
        foreach (var linha in saida.Split('\n'))
        {
            var l = linha.Trim();
            if (l.StartsWith("Index", StringComparison.OrdinalIgnoreCase))
            {
                if (indice is not null) Fechar();
                if (int.TryParse(l.Split(':')[1].Trim(), out var idx)) indice = idx;
            }
            else if (indice is not null)
            {
                if (l.StartsWith("Name", StringComparison.OrdinalIgnoreCase) && !l.StartsWith("Names", StringComparison.OrdinalIgnoreCase)) nome = Depois(l);
                else if (l.StartsWith("Description", StringComparison.OrdinalIgnoreCase)) descricao = Depois(l);
                else if (l.StartsWith("Version", StringComparison.OrdinalIgnoreCase)) versao = Depois(l);
                else if (l.StartsWith("Architecture", StringComparison.OrdinalIgnoreCase)) arquitetura = Depois(l);
                else if (l.StartsWith("Size", StringComparison.OrdinalIgnoreCase))
                    tamanho = long.TryParse(Depois(l).Replace(".", "").Split(' ')[0], out var t) ? t : 0;
            }
        }
        Fechar();
        return edicoes;
    }

    /// <summary>Parse de `dism /Get-AppxProvisionedPackages`: linhas "PackageName : ...".</summary>
    public static List<AppImagem> AnalisarSaidaAppx(string saida) =>
        saida.Split('\n').Select(l => l.Trim())
            .Where(l => l.StartsWith("PackageName", StringComparison.OrdinalIgnoreCase))
            .Select(l => new AppImagem(Depois(l)))
            .Where(a => a.Pacote.Length > 0)
            .ToList();

    /// <summary>Parse de `dism /Get-WindowsOptionalFeature`: pares "Feature Name/State".</summary>
    public static List<FeatureImagem> AnalisarSaidaFeatures(string saida)
    {
        var features = new List<FeatureImagem>();
        string? nome = null;
        foreach (var linha in saida.Split('\n'))
        {
            var l = linha.Trim();
            if (l.StartsWith("Feature Name", StringComparison.OrdinalIgnoreCase)) nome = Depois(l);
            else if (nome is not null && l.StartsWith("State", StringComparison.OrdinalIgnoreCase))
            {
                features.Add(new FeatureImagem(nome, Depois(l)));
                nome = null;
            }
        }
        return features;
    }

    static string Depois(string linha) => linha.Contains(':') ? linha[(linha.IndexOf(':') + 1)..].Trim() : linha.Trim();
}

/// <summary>Resultado da análise de uma imagem (edições + apps + features).</summary>
public sealed record ResultadoAnaliseImagem(IReadOnlyList<EdicaoImagem> Edicoes, IReadOnlyList<AppImagem> Apps, IReadOnlyList<FeatureImagem> Features, string? Erro = null)
{
    public bool Ok => Erro is null;
}

/// <summary>Execução do DISM (somente leitura) para analisar uma ISO do Windows — paridade com o fluxo
/// F5 do original (AnalyzeImage), sem a parte de edição/construção. Nada é alterado: a ISO é montada só
/// para leitura e desmontada ao final.</summary>
public static class ExecutorDism
{
    /// <summary>Valida o caminho da ISO (extensão + existência). Função pura (testável).</summary>
    public static string? ValidarIso(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return "Escolha um arquivo .iso.";
        if (!caminho.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return "O arquivo precisa ter extensão .iso.";
        return File.Exists(caminho) ? null : "Arquivo não encontrado.";
    }

    /// <summary>Caminho do DISM do sistema (presente em qualquer Windows com serviço de imagem).</summary>
    public static string CaminhoDism => Path.Combine(Environment.SystemDirectory, "Dism.exe");

    /// <summary>Roda `dism /English <argumentos>` e devolve a saída padrão (com a de erro anexada).</summary>
    public static async Task<(int Codigo, string Saida)> RodarDismAsync(IEnumerable<string> argumentos, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(CaminhoDism)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { "/English" }.Concat(argumentos)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return (-1, "Não foi possível iniciar o DISM.");
        var saida = await p.StandardOutput.ReadToEndAsync(ct);
        var erro = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, saida + (erro.Length > 0 ? "\n" + erro : ""));
    }

    /// <summary>Analisa uma ISO completa: monta (read-only), localiza o install.wim/esd, lê edições,
    /// apps provisionados e features da 1ª edição, e desmonta. Devolve Erro em qualquer falha.</summary>
    public static async Task<ResultadoAnaliseImagem> AnalisarIsoAsync(string caminhoIso, CancellationToken ct = default)
    {
        var validacao = ValidarIso(caminhoIso);
        if (validacao is not null) return new(new List<EdicaoImagem>(), new List<AppImagem>(), new List<FeatureImagem>(), validacao);

        string? letra = null;
        try
        {
            letra = await MontarIsoAsync(caminhoIso, ct);
            if (letra is null) return new(new List<EdicaoImagem>(), new List<AppImagem>(), new List<FeatureImagem>(), "Não foi possível montar a ISO (exige administrador).");

            var wim = new[] { @"sources\install.wim", @"sources\install.esd" }
                .Select(p => Path.Combine(letra + @":\", p))
                .FirstOrDefault(File.Exists);
            if (wim is null) return new(new List<EdicaoImagem>(), new List<AppImagem>(), new List<FeatureImagem>(), "install.wim/install.esd não encontrado na ISO.");

            var (codigoEdicoes, saidaWim) = await RodarDismAsync(new[] { "/Get-WimInfo", "/WimFile:" + wim }, ct);
            if (codigoEdicoes != 0) return new(new List<EdicaoImagem>(), new List<AppImagem>(), new List<FeatureImagem>(), "DISM não leu a imagem: " + saidaWim.Trim());

            var edicoes = AnalisadorImagem.AnalisarSaidaWimInfo(saidaWim);
            var (codigoApps, saidaApps) = await RodarDismAsync(new[] { "/Get-AppxProvisionedPackages", "/WimFile:" + wim, "/Index:1" }, ct);
            var (codigoFeat, saidaFeat) = await RodarDismAsync(new[] { "/Get-WindowsOptionalFeature", "/WimFile:" + wim, "/Index:1" }, ct);

            return new ResultadoAnaliseImagem(
                edicoes,
                codigoApps == 0 ? AnalisadorImagem.AnalisarSaidaAppx(saidaApps) : new List<AppImagem>(),
                codigoFeat == 0 ? AnalisadorImagem.AnalisarSaidaFeatures(saidaFeat) : new List<FeatureImagem>());
        }
        catch (Exception ex)
        {
            return new(new List<EdicaoImagem>(), new List<AppImagem>(), new List<FeatureImagem>(), ex.Message);
        }
        finally
        {
            if (letra is not null) await DesmontarIsoAsync(caminhoIso, ct);
        }
    }

    static async Task<string?> MontarIsoAsync(string iso, CancellationToken ct)
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
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"Dismount-DiskImage -ImagePath '{iso.Replace("'", "''")}' -ErrorAction SilentlyContinue");
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync(ct);
    }
}
