namespace Sevenzin.Diagnostics;

/// <summary>Um adaptador de rede ativo (IPEnabled) do Windows.</summary>
public sealed record InterfaceRede(string Guid, string Descricao);

/// <summary>Otimizações de rede POR ADAPTADOR — paridade com o original (catalogo §4: TcpAckFrequency,
/// TCPNoDelay, TcpDelAckTicks, NetbiosOptions, EnableMulticast). Chaves dinâmicas por {GUID} da interface;
/// cada item é um Tweak reversível aplicado pelo MotorTweaks (backup/desfazer/auditoria).</summary>
public static class RedePorInterface
{
    /// <summary>Caminhos puros (testáveis) das chaves por interface.</summary>
    public static string CaminhoTcpip(string guid) => @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + guid;
    public static string CaminhoNetbt(string guid) => @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_" + guid;
    public static string CaminhoDnscache(string guid) => @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\InterfaceSpecificParameters\" + guid;

    /// <summary>Lista os adaptadores IP ativos (WMI). Lista vazia quando não há rede.</summary>
    public static List<InterfaceRede> Listar()
    {
        var lista = new List<InterfaceRede>();
        try
        {
            foreach (var mo in Wmi.Query("SELECT SettingID,Description FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE"))
            {
                var guid = Wmi.Str(mo, "SettingID");
                if (guid is null) continue;
                lista.Add(new InterfaceRede(guid, Wmi.Str(mo, "Description") ?? guid));
            }
        }
        catch { /* WMI indisponível */ }
        return lista.OrderBy(i => i.Descricao, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Os 5 ajustes do adaptador, com os VALORES LITERAIS do original. Todos HKLM (admin),
    /// reversíveis pelo MotorTweaks (prévia/backup/desfazer/auditoria).</summary>
    public static List<Tweak> TweaksPara(string guid) => new()
    {
        new Tweak($"rede-tcpack-{Sanitizar(guid)}", "TCP ACK imediato", "TcpAckFrequency=1 (valor do original) — confirmação imediata de pacotes recebidos.", "Rede",
            Colmeia.MaquinaLocal, CaminhoTcpip(guid), "TcpAckFrequency", 1, "Médio — pode aumentar overhead; meça A/B", true),
        new Tweak($"rede-nodelay-{Sanitizar(guid)}", "TCP NoDelay", "TCPNoDelay=1 (valor do original) — desliga o algoritmo de Nagle (envio imediato).", "Rede",
            Colmeia.MaquinaLocal, CaminhoTcpip(guid), "TCPNoDelay", 1, "Médio — pode aumentar overhead; meça A/B", true),
        new Tweak($"rede-delack-{Sanitizar(guid)}", "ACK atrasado off", "TcpDelAckTicks=0 (valor do original) — remove o atraso combinado de ACKs.", "Rede",
            Colmeia.MaquinaLocal, CaminhoTcpip(guid), "TcpDelAckTicks", 0, "Médio — pode aumentar overhead; meça A/B", true),
        new Tweak($"rede-netbios-{Sanitizar(guid)}", "NetBIOS off", "NetbiosOptions=2 (valor do original) — desliga o NetBIOS sobre TCP/IP.", "Rede",
            Colmeia.MaquinaLocal, CaminhoNetbt(guid), "NetbiosOptions", 2, "Baixo — desliga legado de compartilhamento", true),
        new Tweak($"rede-multicast-{Sanitizar(guid)}", "DNS multicast off", "EnableMulticast=0 (valor do original) — desliga o DNS multicast (LLMNR).", "Rede",
            Colmeia.MaquinaLocal, CaminhoDnscache(guid), "EnableMulticast", 0, "Baixo — privacidade", true),
    };

    /// <summary>Sufixo estável do Id por GUID (hex do GUID, sem caracteres especiais).</summary>
    public static string Sanitizar(string guid)
    {
        var limpo = guid.Replace("{", "").Replace("}", "").Replace("-", "");
        return limpo.Length >= 16 ? limpo[..16] : limpo;
    }
}
