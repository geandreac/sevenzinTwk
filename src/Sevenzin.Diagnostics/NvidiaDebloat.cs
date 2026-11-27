using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sevenzin.Diagnostics;

public enum CanalDriverNvidia { GameReady = 0, Studio = 1 }
public enum PerfilDebloatNvidia { Seguro = 0, Avancado = 1 }

public sealed record OpcoesNvidiaDebloat(
    string? InstaladorLocal = null,
    CanalDriverNvidia Canal = CanalDriverNvidia.GameReady,
    PerfilDebloatNvidia Perfil = PerfilDebloatNvidia.Avancado,
    bool RemoverDlssNgx = false,
    bool RemoverPhysX = false,
    bool RemoverHdAudio = false,
    bool SomenteExtrair = false,
    bool LimparResiduos = true);

public sealed record ProgressoNvidiaDebloat(
    int Etapa,
    double Percentual,
    string Detalhe,
    bool Concluido = false,
    bool Erro = false);

public sealed record ItemLookupNvidia(string Nome, int Valor, int ParentId);
public sealed record DriverCatalogoNvidia(string Versao, string UrlDownload);

/// <summary>
/// Motor do Auto NVIDIA Debloat (NvClean): consulta apenas endpoints HTTPS oficiais da NVIDIA,
/// valida host e assinatura Authenticode NVIDIA, extrai com 7-Zip local, poda componentes opcionais
/// preservando os módulos vitais (`Display.Driver`, `NvContainer`, `NVI2`, `ControlPanel`), ajusta
/// `setup.cfg` com backup/validação XML, executa instalação limpa silenciosa sob confirmação do usuário
/// e limpa os resíduos temporários no fim.
/// </summary>
public static class NvidiaDebloat
{
    static readonly HttpClient Http = CriarHttpClient();

    static HttpClient CriarHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        return c;
    }

    /// <summary>Padrões protegidos (nunca removidos, paridade exata com `m__000E`).</summary>
    public static readonly IReadOnlyList<string> PadroesProtegidos = new[]
    {
        @"^Display\.Driver",
        @"^NvContainer",
        @"^NVI\d*$",
        "ControlPanel",
        "NvCpl",
        "NVDisplay",
        "^MSVCRT",
        @"^Display\.Optimus",
    };

    /// <summary>Componentes removidos no perfil Seguro (`m__0002` — 9 entradas).</summary>
    public static readonly IReadOnlyList<string> ComponentesSeguro = new[]
    {
        "GFExperience",
        "GFExperience.NvStreamSrv",
        "GFExperience.NvStreamServer",
        "Display.Update",
        "Update.Core",
        "NvTelemetry",
        "NvTelemetryContainer",
        "nvvhci",
        "NvvHCI",
    };

    /// <summary>Componentes adicionais removidos no perfil Avançado (`m__0008` — 16 entradas).</summary>
    public static readonly IReadOnlyList<string> ComponentesAvancadoExtra = new[]
    {
        "ShadowPlay",
        "ShieldWirelessController",
        "GfnRuntimeSdk",
        "NvStreamSrv",
        "GFExperience.NvStreamServer",
        "Display.NView",
        "NvBackend",
        "Network.Service",
        "NvVAD",
        "NvApp",
        "NvApp.MessageBus",
        "NvCamera",
        "DisplayDriverCrashAnalyzer",
        "Display.CrashAnalyzer",
        "FrameViewSDK",
        "NvModuleTracker",
    };

    static readonly string[] ArquivosConsentimentoSetupCfg =
    {
        "EulaHtmlFile",
        "FunctionalConsentFile",
        "PrivacyPolicyFile",
    };

    /// <summary>Detecta se o nome do adaptador corresponde a uma GPU de Notebook/Laptop.</summary>
    public static bool EhNotebookGpu(string? nomeGpu)
    {
        if (string.IsNullOrWhiteSpace(nomeGpu)) return false;
        if (nomeGpu.Contains("Laptop", StringComparison.OrdinalIgnoreCase)
            || nomeGpu.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
            || nomeGpu.Contains("Notebook", StringComparison.OrdinalIgnoreCase)
            || nomeGpu.Contains("Max-Q", StringComparison.OrdinalIgnoreCase))
            return true;
        return Regex.IsMatch(nomeGpu, @"\b(GTX|RTX)\s*\d{3,4}\s*M\b", RegexOptions.IgnoreCase);
    }

    /// <summary>Normaliza o nome comercial da GPU para cruzar com o catálogo oficial da NVIDIA.</summary>
    public static string NormalizarModeloGpu(string? nome)
    {
        var t = (nome ?? string.Empty).ToUpperInvariant()
            .Replace("NVIDIA", string.Empty)
            .Replace("GEFORCE", string.Empty)
            .Replace("LAPTOP GPU", string.Empty)
            .Replace("NOTEBOOK", string.Empty)
            .Replace("MOBILE", string.Empty)
            .Replace("WITH MAX-Q DESIGN", string.Empty)
            .Replace("MAX-Q", string.Empty);
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    /// <summary>Verifica se um componente bate em algum regex da lista de proteção obrigatória.</summary>
    public static bool EhComponenteProtegido(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return false;
        foreach (var p in PadroesProtegidos)
            if (Regex.IsMatch(nome, p, RegexOptions.IgnoreCase))
                return true;
        return false;
    }

    /// <summary>Monta a lista final de componentes candidatos à remoção conforme o perfil e checkboxes opcionais.</summary>
    public static IReadOnlyList<string> MontarListaComponentesRemover(OpcoesNvidiaDebloat opcoes)
    {
        var lista = new List<string>(ComponentesSeguro);
        if (opcoes.Perfil == PerfilDebloatNvidia.Avancado)
            lista.AddRange(ComponentesAvancadoExtra);
        if (opcoes.RemoverPhysX) lista.Add("PhysX");
        if (opcoes.RemoverHdAudio) lista.Add("HDAudio");
        if (opcoes.RemoverDlssNgx) lista.Add("NGX");

        return lista
            .Where(c => !EhComponenteProtegido(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Valida o padrão de nome de arquivo oficial DCH WHQL da NVIDIA (Game Ready ou Studio `-nsd-`).</summary>
    public static bool NomeInstaladorOficialValido(string? nomeArquivo, CanalDriverNvidia? canal = null)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo)) return false;
        if (!Regex.IsMatch(nomeArquivo, @"\A\d{3}\.\d{2}-(?:desktop|notebook)-.+-dch-whql\.exe\z", RegexOptions.IgnoreCase))
            return false;
        bool ehStudio = nomeArquivo.Contains("-nsd-", StringComparison.OrdinalIgnoreCase);
        return canal switch
        {
            CanalDriverNvidia.GameReady => !ehStudio,
            CanalDriverNvidia.Studio => ehStudio,
            _ => true,
        };
    }

    /// <summary>Exige HTTPS estrito nos servidores oficiais `us.download.nvidia.com` ou `international.download.nvidia.com`.</summary>
    public static bool UrlDownloadNvidiaValida(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        bool hostOk = uri.Host.Equals("us.download.nvidia.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Equals("international.download.nvidia.com", StringComparison.OrdinalIgnoreCase);
        return hostOk && uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Edita `setup.cfg` em memória removendo referências às pastas podadas e aos HTMLs de EULA/consentimento.</summary>
    public static int EditarManifestoSetupCfg(XDocument doc, IReadOnlyCollection<string> pastasRemovidas)
    {
        var paraRemover = new List<XElement>();

        foreach (var el in doc.Descendants())
        {
            var attrName = el.Attribute("name")?.Value ?? string.Empty;
            var attrPkg = el.Attribute("package")?.Value ?? string.Empty;
            if ((attrName.Length > 0 && EhComponenteProtegido(attrName)) ||
                (attrPkg.Length > 0 && EhComponenteProtegido(attrPkg)))
                continue;

            foreach (var alvo in pastasRemovidas)
            {
                if (CorrespondeAtributo(attrName, alvo) || CorrespondeAtributo(attrPkg, alvo))
                {
                    paraRemover.Add(el);
                    break;
                }
            }
        }

        foreach (var fileEl in doc.Descendants("file"))
        {
            var attrName = fileEl.Attribute("name")?.Value ?? string.Empty;
            if (ArquivosConsentimentoSetupCfg.Any(c => attrName.Contains(c, StringComparison.OrdinalIgnoreCase)))
                paraRemover.Add(fileEl);
        }

        int removidos = 0;
        foreach (var el in paraRemover)
        {
            if (el.Parent is not null)
            {
                el.Remove();
                removidos++;
            }
        }

        // Valida que o XML resultante continua bem-formado
        _ = XDocument.Parse(doc.ToString());
        return removidos;
    }

    static bool CorrespondeAtributo(string valorAtributo, string componente) =>
        valorAtributo.Length > 0
        && componente.Length > 0
        && (valorAtributo.Equals(componente, StringComparison.OrdinalIgnoreCase)
            || valorAtributo.Contains(componente, StringComparison.OrdinalIgnoreCase));

    /// <summary>Faz o parse do XML retornado por `lookupValueSearch.aspx?TypeID=2|3`.</summary>
    public static IReadOnlyList<ItemLookupNvidia> ParseLookupXml(string xml)
    {
        var lista = new List<ItemLookupNvidia>();
        if (string.IsNullOrWhiteSpace(xml)) return lista;
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var item in doc.Descendants("LookupValue"))
            {
                var nome = (item.Element("Name")?.Value ?? string.Empty).Trim();
                int.TryParse(item.Element("Value")?.Value, out int val);
                int.TryParse(item.Attribute("ParentID")?.Value, out int parentId);
                if (val != 0 && nome.Length > 0)
                    lista.Add(new ItemLookupNvidia(nome, val, parentId));
            }
        }
        catch { }
        return lista;
    }

    /// <summary>Resolve `(psid, pfid)` cruzando séries (TypeID=2) e modelos (TypeID=3) para Desktop ou Notebook.</summary>
    public static bool ResolverIdsCatalogo(
        string? nomeGpu,
        bool notebook,
        IReadOnlyList<ItemLookupNvidia> series,
        IReadOnlyList<ItemLookupNvidia> modelos,
        out int psid,
        out int pfid,
        out string nomeCatalogo)
    {
        psid = 0; pfid = 0; nomeCatalogo = string.Empty;
        var alvo = NormalizarModeloGpu(nomeGpu);
        if (alvo.Length == 0) return false;

        var seriesFiltradas = series.Where(s =>
        {
            bool serieNotebook = s.Nome.Contains("Notebook", StringComparison.OrdinalIgnoreCase)
                              || s.Nome.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                              || s.Nome.Contains("(Notebooks)", StringComparison.OrdinalIgnoreCase);
            return serieNotebook == notebook;
        }).ToList();

        foreach (var s in seriesFiltradas)
        {
            foreach (var m in modelos.Where(x => x.ParentId == s.Valor))
            {
                if (NormalizarModeloGpu(m.Nome) == alvo)
                {
                    psid = s.Valor;
                    pfid = m.Valor;
                    nomeCatalogo = m.Nome;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Plano B quando o modelo não está no catálogo (placa sem driver com ID desconhecido, nome fora do padrão):
    /// a série GeForce RTX mais nova (desktop ou notebook) e o primeiro modelo dela. O pacote Game Ready/Studio é o
    /// mesmo para todas as GeForce que ele suporta, e o instalador da NVIDIA recusa sozinho uma placa que ele não cobre.</summary>
    public static bool ResolverSerieMaisRecente(
        bool notebook,
        IReadOnlyList<ItemLookupNvidia> series,
        IReadOnlyList<ItemLookupNvidia> modelos,
        out int psid,
        out int pfid,
        out string nomeCatalogo)
    {
        psid = 0; pfid = 0; nomeCatalogo = string.Empty;
        var serie = series
            .Select(s => (s, m: Regex.Match(s.Nome, @"^GeForce RTX (\d+) Series(?: \(Notebooks?\))?$", RegexOptions.IgnoreCase)))
            .Where(x => x.m.Success && x.s.Nome.Contains("Notebook", StringComparison.OrdinalIgnoreCase) == notebook)
            .OrderByDescending(x => int.Parse(x.m.Groups[1].Value))
            .Select(x => x.s)
            .FirstOrDefault(s => modelos.Any(m => m.ParentId == s.Valor));
        if (serie is null) return false;
        var modelo = modelos.First(m => m.ParentId == serie.Valor);
        (psid, pfid, nomeCatalogo) = (serie.Valor, modelo.Valor, $"{serie.Nome} (pacote geral)");
        return true;
    }

    /// <summary>Extrai o driver oficial mais recente da resposta JSON de `AjaxDriverService.php`.</summary>
    public static DriverCatalogoNvidia? ExtrairMelhorDriverJson(string json, bool notebook, CanalDriverNvidia canal)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("IDS", out var ids) || ids.ValueKind != JsonValueKind.Array)
                return null;

            string marcadorChassi = notebook ? "-notebook-" : "-desktop-";
            foreach (var item in ids.EnumerateArray())
            {
                if (!item.TryGetProperty("downloadInfo", out var dl)) continue;
                var versao = dl.TryGetProperty("Version", out var vEl) ? (vEl.GetString() ?? "") : "";
                var url = dl.TryGetProperty("DownloadURL", out var uEl) ? (uEl.GetString() ?? "") : "";
                if (!Regex.IsMatch(versao, @"\A\d{3}\.\d{2}\z")) continue;
                if (!UrlDownloadNvidiaValida(url)) continue;

                var fileName = Path.GetFileName(new Uri(url).LocalPath).ToLowerInvariant();
                if (!fileName.StartsWith(versao.ToLowerInvariant() + "-", StringComparison.Ordinal)) continue;
                if (!fileName.Contains(marcadorChassi, StringComparison.Ordinal)) continue;
                if (!NomeInstaladorOficialValido(fileName, canal)) continue;

                return new DriverCatalogoNvidia(versao, url);
            }
        }
        catch { }
        return null;
    }

    public static string FormatarTamanho(double bytes) => bytes switch
    {
        >= 1073741824.0 => $"{bytes / 1073741824.0:N1} GB",
        >= 1048576.0 => $"{bytes / 1048576.0:N0} MB",
        >= 1024.0 => $"{bytes / 1024.0:N0} KB",
        _ => $"{bytes:N0} B",
    };

    /// <summary>Localiza um extrator 7-Zip instalado na máquina (`7z.exe` ou `NanaZipC.exe`).</summary>
    public static string? LocalizarExtrator7Zip()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidatos = new[]
        {
            Path.Combine(pf, "7-Zip", "7z.exe"),
            Path.Combine(pf86, "7-Zip", "7z.exe"),
            Path.Combine(pf, "NanaZip", "7z.exe"),
            Path.Combine(pf, "NanaZip", "NanaZipC.exe"),
        };
        foreach (var c in candidatos)
            if (File.Exists(c)) return c;

        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var trimmed = dir.Trim();
                    if (trimmed.Length == 0) continue;
                    var p = Path.Combine(trimmed, "7z.exe");
                    if (File.Exists(p)) return p;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Valida a assinatura Authenticode exigindo `Status == Valid` e certificado emitido para `NVIDIA`.</summary>
    public static bool VerificarAssinaturaNvidia(string caminhoArquivo, out string motivo)
    {
        motivo = string.Empty;
        if (string.IsNullOrWhiteSpace(caminhoArquivo) || !File.Exists(caminhoArquivo))
        {
            motivo = "arquivo ausente";
            return false;
        }
        var escapado = caminhoArquivo.Replace("'", "''");
        var cmd = $"$s=Get-AuthenticodeSignature -LiteralPath '{escapado}';if($s.Status -eq 'Valid' -and $s.SignerCertificate -and $s.SignerCertificate.Subject -match 'NVIDIA'){{Write-Output 'VALID'}}else{{Write-Output ('INVALID:'+$s.Status)}}";
        var saida = RodarPowerShellLeitura(cmd);
        if (saida.Equals("VALID", StringComparison.OrdinalIgnoreCase))
            return true;
        motivo = saida.Length == 0 ? "verificação de assinatura indisponível" : saida;
        return false;
    }

    const string PainelLegado = @"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe";
    /// <summary>Como abrir o Painel de Controle NVIDIA: o executável clássico, se existir; senão o app da Microsoft
    /// Store (driver DCH) pelo AppsFolder, com o nome de família LIDO do pacote instalado; null quando nenhum existe.
    /// O nome não pode ser fixo: o valor que estava no código ("…_56jybq7fm28ns") não é o do pacote real
    /// ("…_56jybvy8sckqj"), e o Windows abria um item inexistente sem erro. Função pura.</summary>
    public static (string Exe, string Args)? ComandoPainelControle(bool legadoExiste, string? familiaPacoteLoja) =>
        legadoExiste ? (PainelLegado, "")
        : string.IsNullOrWhiteSpace(familiaPacoteLoja) ? null
        : ("explorer.exe", $@"shell:AppsFolder\{familiaPacoteLoja.Trim()}!NVIDIACorp.NVIDIAControlPanel");

    /// <summary>Abre o Painel de Controle NVIDIA (clássico `nvcplui.exe` ou app DCH da Microsoft Store). Nunca lança.</summary>
    public static bool AbrirPainelControle(Action<string>? log = null)
    {
        try
        {
            var familia = RodarPowerShellLeitura("(Get-AppxPackage -Name NVIDIACorp.NVIDIAControlPanel | Select-Object -First 1).PackageFamilyName");
            var cmd = ComandoPainelControle(File.Exists(PainelLegado), familia);
            if (cmd is null)
            {
                log?.Invoke("  [!] Painel de Controle NVIDIA ainda não encontrado (driver DCH pode concluir registro pela Store).");
                return false;
            }
            Process.Start(new ProcessStartInfo(cmd.Value.Exe, cmd.Value.Args) { UseShellExecute = true });
            log?.Invoke("  [OK] Painel de Controle NVIDIA aberto.");
            return true;
        }
        catch (Exception e)
        {
            log?.Invoke("  [!] Não foi possível abrir o Painel de Controle NVIDIA: " + e.Message);
            return false;
        }
    }

    /// <summary>Tenta abrir o Painel de Controle NVIDIA após o reinício com breves tentativas enquanto serviços sobem.</summary>
    public static async Task<bool> AbrirPainelControleComEsperaAsync(Action<string>? log = null, int maxTentativas = 4, int intervaloMs = 2000)
    {
        for (int i = 1; i <= maxTentativas; i++)
        {
            if (AbrirPainelControle(i == maxTentativas ? log : null))
                return true;
            if (i < maxTentativas)
                await Task.Delay(intervaloMs);
        }
        return false;
    }

    static string RodarPowerShellLeitura(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"{comando}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return string.Empty;
            var txt = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            return txt.Trim();
        }
        catch { return string.Empty; }
    }

    /// <summary>Executa o pipeline completo do Auto NVIDIA Debloat com log auditável em `%LOCALAPPDATA%\Sevenzin\NvClean`.</summary>
    public static Task<int> ExecutarAsync(
        string? nomeGpuDetectada,
        OpcoesNvidiaDebloat opcoes,
        Action<string> onLog,
        Action<ProgressoNvidiaDebloat> onProgresso,
        CancellationToken ct = default) =>
        Task.Run(() => ExecutarInterno(nomeGpuDetectada, opcoes, onLog, onProgresso, ct), ct);

    static int ExecutarInterno(
        string? nomeGpuDetectada,
        OpcoesNvidiaDebloat opcoes,
        Action<string> uiLog,
        Action<ProgressoNvidiaDebloat> onProgresso,
        CancellationToken ct)
    {
        var pastaLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "NvClean");
        Directory.CreateDirectory(pastaLogs);
        var arqLog = Path.Combine(pastaLogs, $"nvclean-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var sbLog = new StringBuilder();

        void Log(string msg)
        {
            var linha = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            sbLog.AppendLine(linha);
            try { File.AppendAllText(arqLog, linha + Environment.NewLine); } catch { }
            uiLog(msg);
        }

        try
        {
            string? instalador = opcoes.InstaladorLocal;
            bool baixadoAutomaticamente = false;

            if (string.IsNullOrWhiteSpace(instalador) || !File.Exists(instalador))
            {
                var pastaDownload = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "NVIDIA");
                instalador = BaixarMaisRecente(nomeGpuDetectada, opcoes.Canal, pastaDownload, Log, onProgresso, ct);
                baixadoAutomaticamente = true;
            }
            else
            {
                onProgresso(new ProgressoNvidiaDebloat(1, 100, "Instalador local selecionado"));
            }

            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(instalador) || !File.Exists(instalador))
            {
                Log("[X] Nenhum instalador de driver NVIDIA válido obtido.");
                onProgresso(new ProgressoNvidiaDebloat(1, 0, "Falha ao obter driver", Concluido: true, Erro: true));
                return 1;
            }
            if (!instalador.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Log("[X] O arquivo precisa ser o instalador oficial .exe da NVIDIA.");
                onProgresso(new ProgressoNvidiaDebloat(1, 0, "Arquivo inválido", Concluido: true, Erro: true));
                return 1;
            }
            if (!baixadoAutomaticamente && !NomeInstaladorOficialValido(Path.GetFileName(instalador)))
            {
                Log("[X] Selecione um instalador oficial DCH WHQL da NVIDIA (ex.: 566.36-desktop-win10-win11-64bit-international-dch-whql.exe).");
                onProgresso(new ProgressoNvidiaDebloat(1, 0, "Nome fora do padrão oficial", Concluido: true, Erro: true));
                return 1;
            }
            if (!VerificarAssinaturaNvidia(instalador, out var motivoAssinatura))
            {
                Log("[X] A assinatura digital Authenticode NVIDIA é inválida: " + motivoAssinatura);
                onProgresso(new ProgressoNvidiaDebloat(1, 0, "Assinatura inválida", Concluido: true, Erro: true));
                return 1;
            }

            var fi = new FileInfo(instalador);
            Log($"==> Driver verificado: {fi.FullName} ({fi.Length / 1048576.0:N1} MB)");

            var listaRemover = MontarListaComponentesRemover(opcoes);
            Log($"==> Perfil: {(opcoes.Perfil == PerfilDebloatNvidia.Avancado ? "AVANÇADO" : "Seguro")} · Canal: {(opcoes.Canal == CanalDriverNvidia.Studio ? "Studio Driver" : "Game Ready")}");
            Log("  Protegido (nunca removido): Display.Driver, NvContainer, NVI2, ControlPanel.");

            Log("==> Preparando extrator 7-Zip...");
            var extrator = LocalizarExtrator7Zip();
            if (extrator is null)
            {
                Log("[X] Nenhum extrator 7-Zip (7z.exe / NanaZip) encontrado. Instale o 7-Zip pela aba Instaladores e tente novamente.");
                onProgresso(new ProgressoNvidiaDebloat(2, 0, "7-Zip não encontrado", Concluido: true, Erro: true));
                return 1;
            }
            Log("  [OK] " + extrator);

            var versaoLimpa = new string(fi.Name.Where(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
            if (string.IsNullOrEmpty(versaoLimpa)) versaoLimpa = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var pastaTemp = Path.Combine(Path.GetTempPath(), "SevenzinNvClean_" + versaoLimpa);
            if (Directory.Exists(pastaTemp)) ApagarPastaComContagem(pastaTemp, Log);
            Directory.CreateDirectory(pastaTemp);

            Log("==> Extraindo pacote do driver (1–2 min)...");
            onProgresso(new ProgressoNvidiaDebloat(2, 10, "Extraindo pacote..."));
            int rcExtracao = RodarProcesso(extrator, $"x \"{fi.FullName}\" -o\"{pastaTemp}\" -y -bsp0 -bso0", ct);
            if (rcExtracao != 0)
            {
                Log("[X] Falha ao extrair o pacote do driver NVIDIA.");
                onProgresso(new ProgressoNvidiaDebloat(2, 0, "Falha na extração", Concluido: true, Erro: true));
                return 1;
            }

            var setupExe = Path.Combine(pastaTemp, "setup.exe");
            var setupCfg = Path.Combine(pastaTemp, "setup.cfg");
            if (!File.Exists(setupExe) || !File.Exists(setupCfg))
            {
                Log("[X] setup.exe / setup.cfg não encontrados no pacote extraído.");
                onProgresso(new ProgressoNvidiaDebloat(2, 0, "Pacote inválido", Concluido: true, Erro: true));
                return 1;
            }
            if (!VerificarAssinaturaNvidia(setupExe, out var motivoSetup))
            {
                Log("[X] setup.exe sem assinatura NVIDIA válida: " + motivoSetup);
                onProgresso(new ProgressoNvidiaDebloat(2, 0, "setup.exe inválido", Concluido: true, Erro: true));
                return 1;
            }

            Log("==> Removendo componentes de bloat/telemetria...");
            onProgresso(new ProgressoNvidiaDebloat(3, 45, "Podando componentes..."));
            var removidosPasta = new List<string>();
            foreach (var comp in listaRemover)
            {
                ct.ThrowIfCancellationRequested();
                if (EhComponenteProtegido(comp)) continue;
                var sub = Path.Combine(pastaTemp, comp);
                if (Directory.Exists(sub))
                {
                    ApagarPastaComContagem(sub, Log);
                    if (!Directory.Exists(sub))
                        removidosPasta.Add(comp);
                }
            }

            Log("==> Ajustando setup.cfg para instalação limpa...");
            File.Copy(setupCfg, setupCfg + ".bak", overwrite: true);
            try
            {
                var doc = XDocument.Load(setupCfg);
                int nosRemovidos = EditarManifestoSetupCfg(doc, removidosPasta);
                doc.Save(setupCfg);
                _ = XDocument.Load(setupCfg);
                Log($"  [OK] setup.cfg validado ({nosRemovidos} entradas removidas; backup em setup.cfg.bak).");
            }
            catch (Exception exXml)
            {
                try { File.Copy(setupCfg + ".bak", setupCfg, overwrite: true); } catch { }
                Log("[X] Falha ao validar setup.cfg; instalação abortada: " + exXml.Message);
                onProgresso(new ProgressoNvidiaDebloat(3, 0, "Erro no manifesto XML", Concluido: true, Erro: true));
                return 1;
            }

            if (opcoes.SomenteExtrair)
            {
                Log($"==> Modo somente extração concluído em: {pastaTemp}");
                onProgresso(new ProgressoNvidiaDebloat(6, 100, "Pacote enxuto pronto", Concluido: true));
                return 0;
            }

            Log("==> Instalando driver limpo (-s -noreboot -clean -nofinish -passive)...");
            Log("  A tela pode piscar durante alguns segundos — isso é normal.");
            onProgresso(new ProgressoNvidiaDebloat(4, 75, "Instalando driver limpo..."));
            int rcSetup = RodarProcesso(setupExe, "-s -noreboot -clean -nofinish -passive", ct);
            bool instalouOk = rcSetup is 0 or 1;
            if (!instalouOk)
            {
                Log($"[X] O instalador NVIDIA retornou código {rcSetup}.");
                onProgresso(new ProgressoNvidiaDebloat(4, 0, $"Código {rcSetup}", Concluido: true, Erro: true));
                return rcSetup;
            }
            Log($"  [OK] Driver instalado (código {rcSetup}).");

            onProgresso(new ProgressoNvidiaDebloat(5, 90, "Limpando temporários..."));
            Log("==> Limpando resíduos temporários...");
            long liberado = ApagarPastaComContagem(pastaTemp, Log);
            if (opcoes.LimparResiduos)
            {
                var sysRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
                foreach (var res in new[]
                {
                    Path.Combine(sysRoot, "NVIDIA"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NVIDIA Corporation", "Downloader"),
                    Path.Combine(Path.GetTempPath(), "NVIDIA Corporation"),
                })
                {
                    liberado += ApagarPastaComContagem(res, Log);
                }
            }
            Log($"  [OK] Espaço temporário liberado: {FormatarTamanho(liberado)}");
            Log($"[OK] Driver limpo instalado com sucesso (log salvo em {arqLog}). Preparando reinício para finalizar e abrir o Painel NVIDIA...");
            onProgresso(new ProgressoNvidiaDebloat(6, 100, "Instalação concluída — reiniciando em 10s...", Concluido: true));
            return 0;
        }
        catch (OperationCanceledException)
        {
            Log("[!] Operação cancelada pelo usuário.");
            onProgresso(new ProgressoNvidiaDebloat(0, 0, "Cancelado", Concluido: true, Erro: true));
            return -2;
        }
        catch (Exception ex)
        {
            string? hosts = null;
            try { hosts = File.ReadAllText(Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts")); } catch { }
            var rede = MensagemFalhaRede(ex, hosts);
            Log("[X] Erro: " + (rede ?? ex.Message));
            onProgresso(new ProgressoNvidiaDebloat(0, 0, rede is null ? ex.Message : "Sem acesso à NVIDIA", Concluido: true, Erro: true));
            return -1;
        }
    }

    /// <summary>Traduz falha de rede (nome não resolvido / sem conexão) numa mensagem acionável; null se não for de
    /// rede. Antes aparecia só o texto cru do Windows ("Este host não é reconhecido"). Se o arquivo hosts tiver linha
    /// ativa com "nvidia.com" (comum em ferramentas de "debloat"), aponta isso como causa provável.</summary>
    public static string? MensagemFalhaRede(Exception ex, string? conteudoHosts)
    {
        HttpRequestException? http = null;
        for (var e = ex; e is not null && http is null; e = e.InnerException) http = e as HttpRequestException;
        if (http is null) return null;
        bool semNome = http.HttpRequestError == HttpRequestError.NameResolutionError
            || http.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.HostNotFound or System.Net.Sockets.SocketError.TryAgain or System.Net.Sockets.SocketError.NoData };
        bool semConexao = semNome || http.HttpRequestError == HttpRequestError.ConnectionError;
        if (!semConexao) return null;

        bool hostsBloqueia = (conteudoHosts ?? "").Split('\n')
            .Select(l => l.Trim())
            .Any(l => l.Length > 0 && !l.StartsWith('#') && l.Contains("nvidia.com", StringComparison.OrdinalIgnoreCase));
        var causa = hostsBloqueia
            ? "o arquivo hosts desta máquina bloqueia domínios da NVIDIA (linhas com \"nvidia.com\" em C:\\Windows\\System32\\drivers\\etc\\hosts) — remova-as e tente de novo"
            : semNome
                ? "esta máquina não conseguiu encontrar o servidor da NVIDIA (DNS) — confira a internet, VPN ou bloqueador de anúncios (Pi-hole, AdGuard)"
                : "esta máquina não conseguiu se conectar ao servidor da NVIDIA — confira a internet, firewall ou VPN";
        return $"Sem acesso à NVIDIA: {causa}. Alternativa: baixe o driver no site da NVIDIA em outro PC e use \"Já tenho o instalador\".";
    }

    static string? BaixarMaisRecente(
        string? nomeGpu,
        CanalDriverNvidia canal,
        string pastaDestino,
        Action<string> log,
        Action<ProgressoNvidiaDebloat> onProgresso,
        CancellationToken ct)
    {
        bool notebook = EhNotebookGpu(nomeGpu);
        log($"==> Detectando GPU: {(string.IsNullOrWhiteSpace(nomeGpu) ? "(desconhecida)" : nomeGpu)} [{(notebook ? "NOTEBOOK" : "DESKTOP")}]");
        onProgresso(new ProgressoNvidiaDebloat(1, 5, "Consultando catálogo oficial NVIDIA..."));

        var xmlSeries = Http.GetStringAsync("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=2", ct).GetAwaiter().GetResult();
        var xmlModelos = Http.GetStringAsync("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3", ct).GetAwaiter().GetResult();

        var series = ParseLookupXml(xmlSeries);
        var modelos = ParseLookupXml(xmlModelos);
        if (!ResolverIdsCatalogo(nomeGpu, notebook, series, modelos, out int psid, out int pfid, out var nomeCat))
        {
            if (!ResolverSerieMaisRecente(notebook, series, modelos, out psid, out pfid, out nomeCat))
            {
                log("[X] Não foi possível mapear automaticamente este modelo no catálogo da NVIDIA. Use a opção 'Já tenho o instalador'.");
                return null;
            }
            log("  [!] Modelo não encontrado no catálogo; usando o pacote GeForce mais recente, que é o mesmo para as placas suportadas.");
        }
        log($"  [OK] Catálogo NVIDIA: {nomeCat} (psid={psid}, pfid={pfid})");

        int osId = Environment.OSVersion.Version.Build >= 22000 ? 135 : 57;
        int upCrd = canal == CanalDriverNvidia.Studio ? 1 : 0;
        var urlApi = $"https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID={osId}&languageCode=1033&dch=1&numberOfResults=5&isWHQL=1&upCRD={upCrd}";
        var json = Http.GetStringAsync(urlApi, ct).GetAwaiter().GetResult();
        var driver = ExtrairMelhorDriverJson(json, notebook, canal);
        if (driver is null)
        {
            log("[X] Nenhum driver WHQL DCH encontrado para esta GPU e canal no servidor da NVIDIA.");
            return null;
        }
        log($"  [OK] Versão oficial mais recente: {driver.Versao} ({driver.UrlDownload})");

        using var headReq = new HttpRequestMessage(HttpMethod.Head, driver.UrlDownload);
        using var headResp = Http.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct).GetAwaiter().GetResult();
        long tamanho = headResp.IsSuccessStatusCode && headResp.Content.Headers.ContentLength is { } len ? len : -1;
        if (tamanho < 52_428_800) // mínimo 50 MB conforme especificação
        {
            log("[X] URL oficial retornou tamanho inesperado (< 50 MB).");
            return null;
        }

        Directory.CreateDirectory(pastaDestino);
        var destino = Path.Combine(pastaDestino, Path.GetFileName(new Uri(driver.UrlDownload).LocalPath));
        if (File.Exists(destino) && new FileInfo(destino).Length == tamanho && VerificarAssinaturaNvidia(destino, out _))
        {
            log("  [OK] Instalador já presente em cache e verificado: " + destino);
            return destino;
        }

        var parcial = destino + ".download-" + Guid.NewGuid().ToString("N") + ".exe";
        try
        {
            log($"==> Baixando instalador oficial ({tamanho / 1048576.0:N0} MB)...");
            using var getReq = new HttpRequestMessage(HttpMethod.Get, driver.UrlDownload);
            using var resp = Http.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, ct).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
            using var net = resp.Content.ReadAsStreamAsync(ct).GetAwaiter().GetResult();
            using (var fs = File.Create(parcial))
            {
                var buf = new byte[1048576];
                long lido = 0;
                var sw = Stopwatch.StartNew();
                long ultimoTick = 0;
                int n;
                while ((n = net.ReadAsync(buf, 0, buf.Length, ct).GetAwaiter().GetResult()) > 0)
                {
                    fs.Write(buf, 0, n);
                    lido += n;
                    if (lido > tamanho) throw new IOException("Download excedeu o tamanho esperado.");
                    if (sw.ElapsedMilliseconds - ultimoTick > 250 || lido == tamanho)
                    {
                        ultimoTick = sw.ElapsedMilliseconds;
                        double pct = lido * 100.0 / tamanho;
                        onProgresso(new ProgressoNvidiaDebloat(1, pct, $"{lido / 1048576.0:N0} / {tamanho / 1048576.0:N0} MB"));
                    }
                }
                if (lido != tamanho) throw new IOException("Download incompleto.");
            }
            if (!VerificarAssinaturaNvidia(parcial, out var errSig))
                throw new InvalidDataException("Assinatura NVIDIA inválida após download: " + errSig);

            if (File.Exists(destino)) File.Delete(destino);
            File.Move(parcial, destino);
            log("  [OK] Download concluído e assinatura Authenticode NVIDIA verificada.");
            return destino;
        }
        finally
        {
            try { if (File.Exists(parcial)) File.Delete(parcial); } catch { }
        }
    }

    /// <summary>Roda e espera. Sem redirecionar a saída: redirecionar sem ler enche o buffer do pipe (~4 KB) e o
    /// processo filho trava para sempre (era o que congelava a extração do 7-Zip, que imprime o progresso).</summary>
    public static int RodarProcesso(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Falha ao iniciar {exe}");
        using var reg = ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } });
        proc.WaitForExit();
        return proc.ExitCode;
    }

    static long ApagarPastaComContagem(string caminho, Action<string> log)
    {
        try
        {
            var di = new DirectoryInfo(caminho);
            if (!di.Exists) return 0;
            long bytes = CalcularTamanhoPasta(di);
            Directory.Delete(caminho, recursive: true);
            log($"  [OK] Removido: {Path.GetFileName(caminho)} ({FormatarTamanho(bytes)})");
            return bytes;
        }
        catch (Exception ex)
        {
            log($"  [!] Não foi possível remover {caminho}: {ex.Message}");
            return 0;
        }
    }

    static long CalcularTamanhoPasta(DirectoryInfo di)
    {
        long total = 0;
        try
        {
            foreach (var f in di.GetFiles()) { try { total += f.Length; } catch { } }
            foreach (var sub in di.GetDirectories()) total += CalcularTamanhoPasta(sub);
        }
        catch { }
        return total;
    }
}
