using System.Text.RegularExpressions;

namespace Sevenzin.Diagnostics;

/// <summary>Situação do driver de uma placa de vídeo encontrada no barramento PCI.</summary>
public enum EstadoDriverPlaca
{
    /// <summary>Driver do fabricante instalado.</summary>
    Instalado,
    /// <summary>Só o "Adaptador de Vídeo Básico da Microsoft": imagem funciona, aceleração e ajustes não.</summary>
    Basico,
    /// <summary>Nenhum driver: o Windows nem classifica a placa como vídeo (some do Gerenciador em "Adaptadores de vídeo").</summary>
    Ausente,
}

/// <summary>Placa de vídeo vista direto no PCI, mesmo sem driver (quando o Win32_VideoController não a lista).</summary>
public sealed record PlacaVideoPci(string Instancia, FabricanteGpu Fabricante, string IdDispositivo, string Nome, EstadoDriverPlaca Estado);

/// <summary>Acha placas de vídeo pelo PCI (classe 03 = controlador de vídeo) e o nome comercial pela tabela offline de
/// IDs da NVIDIA. Cobre o caso em que o driver foi removido (DDU, limpeza) e o Windows não colocou nenhum no lugar:
/// aí nenhuma consulta de "adaptador de vídeo" encontra a placa, mas ela continua no PCI.</summary>
public static class PlacasVideoPci
{
    static readonly Lazy<IReadOnlyDictionary<string, string>> TabelaNvidia = new(CarregarTabelaNvidia);

    /// <summary>Fabricante e ID do dispositivo a partir do ID de instância ("PCI\VEN_10DE&amp;DEV_2414&amp;…").</summary>
    public static (FabricanteGpu Fabricante, string Dispositivo)? LerIds(string? instancia)
    {
        var m = Regex.Match(instancia ?? "", @"VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var fab = m.Groups[1].Value.ToUpperInvariant() switch
        {
            "10DE" => FabricanteGpu.Nvidia,
            "1002" => FabricanteGpu.Amd,
            "8086" => FabricanteGpu.Intel,
            _ => FabricanteGpu.Desconhecido,
        };
        return (fab, m.Groups[2].Value.ToUpperInvariant());
    }

    /// <summary>A classe PCI 03xx (VGA, 3D, XGA) é controlador de vídeo, com ou sem driver.</summary>
    public static bool EhControladorDeVideo(IEnumerable<string>? idsCompativeis) =>
        idsCompativeis?.Any(i => i.StartsWith(@"PCI\CC_03", StringComparison.OrdinalIgnoreCase)) == true;

    public static EstadoDriverPlaca Estado(string? classePnp, string? servico) =>
        !string.Equals(classePnp, "Display", StringComparison.OrdinalIgnoreCase) ? EstadoDriverPlaca.Ausente
        : string.Equals(servico, "BasicDisplay", StringComparison.OrdinalIgnoreCase) ? EstadoDriverPlaca.Basico
        : EstadoDriverPlaca.Instalado;

    /// <summary>Nome comercial a partir de uma linha da base PCI ("GA103 [GeForce RTX 3060 Ti]" → "NVIDIA GeForce RTX 3060 Ti").
    /// Pega o nome entre colchetes, a primeira alternativa ("A / B") e tira o "Lite Hash Rate", que não existe no catálogo de drivers.</summary>
    public static string NomeComercialNvidia(string descricaoPci)
    {
        var m = Regex.Match(descricaoPci, @"\[([^\]]+)\]");
        var nome = (m.Success ? m.Groups[1].Value : descricaoPci).Split(" / ")[0];
        nome = Regex.Replace(nome, @"\s*Lite Hash Rate", "", RegexOptions.IgnoreCase).Trim();
        return nome.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase) ? nome : "NVIDIA " + nome;
    }

    /// <summary>Nome da placa NVIDIA pelo ID do dispositivo, ou null se a tabela (de 2022) não conhece o modelo.</summary>
    public static string? NomeNvidia(string idDispositivo) =>
        TabelaNvidia.Value.TryGetValue(idDispositivo.ToLowerInvariant(), out var d) ? NomeComercialNvidia(d) : null;

    /// <summary>Lê a tabela "id  descrição" (uma por linha; # é comentário).</summary>
    public static IReadOnlyDictionary<string, string> LerTabela(string texto)
    {
        var t = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in texto.Split('\n'))
        {
            var l = linha.TrimEnd('\r');
            if (l.Length < 7 || l[0] == '#' || l[4] != ' ') continue;
            t.TryAdd(l[..4], l[4..].Trim());
        }
        return t;
    }

    static IReadOnlyDictionary<string, string> CarregarTabelaNvidia()
    {
        using var s = typeof(PlacasVideoPci).Assembly.GetManifestResourceStream("Sevenzin.Diagnostics.nvidia-pci-ids.txt");
        return s is null ? new Dictionary<string, string>() : LerTabela(new StreamReader(s).ReadToEnd());
    }

    /// <summary>Placas de vídeo no PCI (somente leitura). Lista vazia se a consulta falhar.</summary>
    public static IReadOnlyList<PlacaVideoPci> Coletar()
    {
        var lista = new List<PlacaVideoPci>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT DeviceID, Name, PNPClass, Service, CompatibleID FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%'"))
            {
                if (!EhControladorDeVideo(mo["CompatibleID"] as string[])) continue;
                var instancia = Wmi.Str(mo, "DeviceID") ?? "";
                if (LerIds(instancia) is not { } ids) continue;
                var estado = Estado(Wmi.Str(mo, "PNPClass"), Wmi.Str(mo, "Service"));
                var nome = ids.Fabricante == FabricanteGpu.Nvidia && NomeNvidia(ids.Dispositivo) is { } n ? n
                    : estado == EstadoDriverPlaca.Instalado ? Wmi.Str(mo, "Name") ?? "Placa de vídeo"
                    : $"Placa {NomeFabricante(ids.Fabricante)} (ID {ids.Dispositivo})";
                lista.Add(new PlacaVideoPci(instancia, ids.Fabricante, ids.Dispositivo, nome, estado));
            }
        }
        catch { }
        return lista;
    }

    public static string NomeFabricante(FabricanteGpu f) => f switch
    {
        FabricanteGpu.Nvidia => "NVIDIA",
        FabricanteGpu.Amd => "AMD",
        FabricanteGpu.Intel => "Intel",
        _ => "de vídeo",
    };
}
