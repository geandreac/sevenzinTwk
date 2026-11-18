using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Estado da licença/ativação do Windows (leitura via <c>slmgr.vbs</c>). Sempre consulta o sistema;
/// nunca aplica crack, bypass ou servidor público de KMS.</summary>
public sealed record EstadoLicenca(string Descricao, string Estado, string Canal, string? KmsHost, string Bruto);

public sealed record ResultadoLicenca(bool Ok, string Mensagem);

/// <summary>Gerenciamento LEGÍTIMO de ativação do Windows (§5.1): consulta o estado da licença, identifica o
/// canal (OEM/Retail/Volume:KMS) e permite configurar um servidor KMS corporativo informado pelo administrador
/// (<c>slmgr /skms</c> + <c>/ato</c>). Não incorpora chaves nem contorna licenciamento.</summary>
public static class Licenciamento
{
    static readonly Regex RxHostKms = new(
        @"^[a-zA-Z0-9](?:[a-zA-Z0-9\-\.]{0,251}[a-zA-Z0-9])?(?::([1-9][0-9]{0,4}))?$",
        RegexOptions.Compiled);

    /// <summary>Valida hostname/IP[:porta] para servidor KMS corporativo, bloqueando injeção de argumentos
    /// ou espaços em linha de comando. Função pura/testável.</summary>
    public static bool HostKmsValido(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        var h = host.Trim();
        var m = RxHostKms.Match(h);
        if (!m.Success) return false;
        if (m.Groups[1].Success && (!int.TryParse(m.Groups[1].Value, out int porta) || porta is < 1 or > 65535))
            return false;
        return h.Contains('.');
    }

    static (int Codigo, string Saida) Rodar(string args)
    {
        // cscript para saída em console (slmgr.vbs sozinho abre caixa de diálogo).
        var psi = new ProcessStartInfo("cscript", $"//nologo slmgr.vbs {args}")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
        };
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, saida);
    }

    /// <summary>Parse do texto do <c>slmgr /dlv</c> (PT-BR / EN) para os campos-chave. Função pura/testável.</summary>
    public static EstadoLicenca AnalisarDlv(string saida)
    {
        string Campo(string rotulo)
        {
            foreach (var linha in (saida ?? "").Split('\n'))
            {
                var i = linha.IndexOf(rotulo, StringComparison.OrdinalIgnoreCase);
                if (i >= 0) return linha[(i + rotulo.Length)..].Trim(' ', ':', '\t', '\r');
            }
            return "";
        }
        var estado = Campo("Status da Licença");
        if (estado.Length == 0) estado = Campo("License Status");
        var canal = Campo("Canal do Produto");
        if (canal.Length == 0) canal = Campo("Product Key Channel");
        var kms = Campo("Nome do Computador KMS");
        if (kms.Length == 0) kms = Campo("KMS machine name");
        var desc = Campo("Descrição");
        if (desc.Length == 0) desc = Campo("Description");
        return new EstadoLicenca(desc, estado, canal, kms.Length > 0 ? kms : null, saida ?? "");
    }

    /// <summary>Estado detalhado da licença (slmgr /dlv).</summary>
    public static EstadoLicenca EstadoDetalhado()
    {
        try
        {
            var (_, saida) = Rodar("/dlv");
            return AnalisarDlv(saida);
        }
        catch { return new EstadoLicenca("", "", "", null, ""); }
    }

    /// <summary>Vencimento da ativação (slmgr /xpr) — texto curto.</summary>
    public static string Vencimento()
    {
        try
        {
            var (_, saida) = Rodar("/xpr");
            return saida.Trim();
        }
        catch { return ""; }
    }

    /// <summary>Configura um servidor KMS corporativo informado pelo administrador e tenta ativar.
    /// Reversível: informar um host vazio remove o servidor KMS (/ckms).</summary>
    public static ResultadoLicenca ConfigurarKms(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            try
            {
                var (c0, _) = Rodar("/ckms"); // limpa o host KMS
                return c0 == 0
                    ? new(true, "Servidor KMS removido (volta ao canal padrão).")
                    : new(false, "Falha ao remover o servidor KMS (exige administrador).");
            }
            catch (Exception e) { return new(false, e.Message); }
        }

        var limpo = host.Trim();
        if (!HostKmsValido(limpo))
            return new(false, "Host KMS inválido. Informe um domínio ou IP corporativo válido (ex.: kms.empresa.local ou kms.empresa.local:1688).");

        try
        {
            var (c1, s1) = Rodar($"/skms {limpo}");
            if (c1 != 0) return new(false, "Falha ao definir o servidor KMS (exige administrador): " + s1.Trim());
            var (c2, s2) = Rodar("/ato");
            return new(c2 == 0, c2 == 0 ? "Servidor KMS configurado e ativação solicitada." : "Servidor KMS definido, mas a ativação falhou: " + s2.Trim());
        }
        catch (Exception e) { return new(false, e.Message); }
    }
}
