namespace Sevenzin.Diagnostics;

/// <summary>Necessário = risco baixo e benefício claro (peso 3). Recomendado = risco médio, "meça A/B" (peso 1).</summary>
public enum NivelProntidao { Necessario, Recomendado }

/// <summary>Um ajuste que conta na prontidão. <paramref name="Aplicado"/> null = não deu para ler (fica fora da conta).</summary>
public sealed record ItemProntidao(string Id, string Nome, string Componente, NivelProntidao Nivel, bool? Aplicado, string Risco = "");

/// <summary>Ids que o botão "Otimizar para jogos" aplicou (para "Desfazer tudo" e para notar quando o Windows os
/// reverte) e o último conjunto de desvios já avisado (para não repetir o aviso a cada abertura).</summary>
public sealed record LoteProntidao(List<string> Ids, List<string> DesviosAvisados);

public sealed record ResultadoLote(IReadOnlyList<ItemProntidao> Aplicados, IReadOnlyList<(ItemProntidao Item, string Motivo)> Falhas);

public sealed record ComponenteProntidao(string Nome, int Percentual, int Ativos, int Total);

public sealed record ResultadoProntidao(int Percentual, int Ativos, int Total,
    IReadOnlyList<ComponenteProntidao> Componentes, IReadOnlyList<ItemProntidao> Itens);

/// <summary>"Prontidão para jogos": quanto do que é recomendado para jogos NESTE PC já está aplicado (0–100%).
/// Não é FPS nem desempenho medido — é a proporção ponderada de ajustes ativos, lida do estado real.
/// Itens de Segurança, timer global, memória e power gating ficam de fora (ver docs/estudo-prontidao-jogos.md).</summary>
public static class ProntidaoJogos
{
    public static readonly string[] OrdemComponentes = { "Windows", "CPU", "GPU", "Rede", "Mouse", "Energia" };

    // (id no CatalogoTweaks, componente, nível, só para este fabricante de GPU)
    public static readonly IReadOnlyList<(string Id, string Componente, NivelProntidao Nivel, FabricanteGpu? So)> TweaksDoCatalogo = new (string, string, NivelProntidao, FabricanteGpu?)[]
    {
        ("gamedvr-off", "Windows", NivelProntidao.Necessario, null),
        ("gamedvr-captura-off", "Windows", NivelProntidao.Necessario, null),
        ("fast-startup-off", "Windows", NivelProntidao.Necessario, null),
        ("mmcss-games-gpu", "CPU", NivelProntidao.Necessario, null),
        ("mmcss-games-prio", "CPU", NivelProntidao.Necessario, null),
        ("mmcss-games-sfio", "CPU", NivelProntidao.Necessario, null),
        ("mmcss-games-sched", "CPU", NivelProntidao.Necessario, null),
        ("mmcss-responsiveness", "CPU", NivelProntidao.Recomendado, null),
        ("priority-separation", "CPU", NivelProntidao.Recomendado, null),
        ("mouse-aceleracao-off", "Mouse", NivelProntidao.Necessario, null),
        ("mouse-limiar1-off", "Mouse", NivelProntidao.Necessario, null),
        ("mouse-limiar2-off", "Mouse", NivelProntidao.Necessario, null),
        ("hags", "GPU", NivelProntidao.Recomendado, null),
        ("amd-chill-off", "GPU", NivelProntidao.Necessario, FabricanteGpu.Amd),
        ("rede-throttle-off", "Rede", NivelProntidao.Recomendado, null),
    };

    public static int Peso(NivelProntidao n) => n == NivelProntidao.Necessario ? 3 : 1;

    /// <summary>Soma ponderada; arredonda para BAIXO (só mostra 100% com tudo aplicado). Função pura.</summary>
    public static ResultadoProntidao Calcular(IReadOnlyList<ItemProntidao> itens)
    {
        static int Pct(IEnumerable<ItemProntidao> lidos)
        {
            var l = lidos.ToList();
            int total = l.Sum(i => Peso(i.Nivel));
            return total == 0 ? 0 : l.Where(i => i.Aplicado == true).Sum(i => Peso(i.Nivel)) * 100 / total;
        }
        var lidos = itens.Where(i => i.Aplicado is not null).ToList();
        var comps = OrdemComponentes
            .Select(c => (c, l: lidos.Where(i => i.Componente == c).ToList()))
            .Where(x => x.l.Count > 0)
            .Select(x => new ComponenteProntidao(x.c, Pct(x.l), x.l.Count(i => i.Aplicado == true), x.l.Count))
            .ToList();
        return new(Pct(lidos), lidos.Count(i => i.Aplicado == true), lidos.Count, comps, itens);
    }

    /// <summary>Monta os itens a partir do estado lido. As leituras de fora do registro chegam prontas para manter
    /// isto testável: <paramref name="planoSevenzinAtivo"/> (null = não lido; ignorado na bateria) e o TCP.</summary>
    public static List<ItemProntidao> Montar(MotorTweaks motor, FabricanteGpu fabricante, bool? planoSevenzinAtivo, bool naBateria,
        (string? Autotuning, string? Congestionamento) tcp, IReadOnlyList<GrupoDriverGpu> gruposDriver)
    {
        bool? Ler(Func<bool> f) { try { return f(); } catch { return null; } }
        var r = new List<ItemProntidao>();
        foreach (var (id, comp, nivel, so) in TweaksDoCatalogo)
        {
            if (so is not null && so != fabricante) continue;
            var t = CatalogoTweaks.Todos.First(x => x.Id == id);
            r.Add(new(id, t.Nome, comp, nivel, Ler(() => motor.Aplicado(t)), t.Risco));
        }
        foreach (var g in gruposDriver.Where(g => g.Fabricante == fabricante))
            r.Add(new(g.Id, $"Driver: {g.Nome}", "GPU", NivelProntidao.Recomendado, Ler(() => GpuDriverTweaks.GrupoAplicado(motor, g)),
                "Médio — mais calor/consumo da GPU; meça A/B"));

        r.Add(new("tcp-global", "TCP global (autotuning normal + CTCP)", "Rede", NivelProntidao.Recomendado,
            tcp.Congestionamento is null ? null : tcp.Congestionamento == "ctcp" && (tcp.Autotuning is null || tcp.Autotuning == "normal"),
            "Baixo — desfazer volta ao padrão do Windows"));
        // Na bateria o plano de alto desempenho drena a carga: não vira pendência.
        if (!naBateria)
            r.Add(new("plano-energia", "Plano de energia SevenzinTwks ativo", "Energia", NivelProntidao.Necessario, planoSevenzinAtivo,
                "Baixo — mais consumo; desfazer volta ao plano Equilibrado"));
        return r;
    }

    /// <summary>O que o botão aplica: só itens lidos e pendentes; Recomendados só no modo Completo. Função pura.</summary>
    public static List<ItemProntidao> Pendentes(IEnumerable<ItemProntidao> itens, bool incluirRecomendados) =>
        itens.Where(i => i.Aplicado == false && (incluirRecomendados || i.Nivel == NivelProntidao.Necessario)).ToList();

    /// <summary>Aplica um a um; uma falha não interrompe os outros. <paramref name="aplicarUm"/> devolve null
    /// quando deu certo ou o motivo da falha.</summary>
    public static ResultadoLote AplicarLote(IReadOnlyList<ItemProntidao> pendentes, Func<ItemProntidao, string?> aplicarUm)
    {
        var ok = new List<ItemProntidao>();
        var falhas = new List<(ItemProntidao, string)>();
        foreach (var i in pendentes)
        {
            string? motivo;
            try { motivo = aplicarUm(i); } catch (Exception e) { motivo = e.Message; }
            if (motivo is null) ok.Add(i); else falhas.Add((i, motivo));
        }
        return new(ok, falhas);
    }

    /// <summary>Itens que o lote aplicou e hoje estão pendentes de novo (Windows Update, outro programa...). Função pura.</summary>
    public static List<ItemProntidao> Desvios(IEnumerable<ItemProntidao> itens, LoteProntidao lote) =>
        itens.Where(i => i.Aplicado == false && lote.Ids.Contains(i.Id)).ToList();

    /// <summary>Executa a ação real de um item (aplicar ou desfazer). Null = ok; senão, o motivo.</summary>
    public static string? Executar(string id, bool aplicar, MotorTweaks motor, IReadOnlyList<GrupoDriverGpu> grupos)
    {
        if (id == "tcp-global")
        {
            var r = aplicar ? RedeGlobal.Aplicar() : RedeGlobal.Restaurar();
            return r.Ok ? null : r.Mensagem;
        }
        if (id == "plano-energia")
        {
            var r = aplicar ? GestorEnergia.AplicarSevenzin() : GestorEnergia.Restaurar();
            return r.Ok ? null : r.Mensagem;
        }
        if (grupos.FirstOrDefault(g => g.Id == id) is { } grupo)
        {
            if (aplicar) GpuDriverTweaks.AplicarGrupo(motor, grupo); else GpuDriverTweaks.DesfazerGrupo(motor, grupo);
            return null;
        }
        var t = CatalogoTweaks.Todos.FirstOrDefault(x => x.Id == id);
        if (t is null) return "Ajuste desconhecido.";
        if (aplicar) { motor.Aplicar(t); return motor.Aplicado(t) ? null : "O valor gravado não confirmou na releitura."; }
        return motor.Desfazer(t) ? null : "Sem backup para desfazer.";
    }

    public static IReadOnlyList<GrupoDriverGpu> GruposDriver(FabricanteGpu fabricante) => fabricante switch
    {
        FabricanteGpu.Nvidia => GpuDriverTweaks.CriarGruposNvidia(GpuDriverTweaks.ResolverChavesClasseDriver(fabricante)),
        FabricanteGpu.Amd => GpuDriverTweaks.CriarGruposAmd(GpuDriverTweaks.ResolverChavesClasseDriver(fabricante)),
        _ => Array.Empty<GrupoDriverGpu>(),
    };

    public static string ArquivoLote =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin", "prontidao-lote.json");

    public static LoteProntidao LerLote(string? arquivo = null)
    {
        try
        {
            var arq = arquivo ?? ArquivoLote;
            if (File.Exists(arq) && System.Text.Json.JsonSerializer.Deserialize<LoteProntidao>(File.ReadAllText(arq)) is { } l)
                return new(l.Ids ?? new(), l.DesviosAvisados ?? new());
        }
        catch { }
        return new(new(), new());
    }

    public static void SalvarLote(LoteProntidao lote, string? arquivo = null)
    {
        var arq = arquivo ?? ArquivoLote;
        Directory.CreateDirectory(Path.GetDirectoryName(arq)!);
        File.WriteAllText(arq + ".tmp", System.Text.Json.JsonSerializer.Serialize(lote));
        File.Move(arq + ".tmp", arq, overwrite: true);
    }

    /// <summary>Lê tudo do sistema (registro, powercfg, netsh). Lento (processos externos): chame fora da UI.</summary>
    public static ResultadoProntidao Avaliar(MotorTweaks motor, FabricanteGpu fabricante, bool naBateria)
    {
        var grupos = GruposDriver(fabricante);
        (string?, string?) tcp;
        try { tcp = RedeGlobal.EstadoAtual(); } catch { tcp = (null, null); }
        return Calcular(Montar(motor, fabricante, GestorEnergia.PlanoSevenzinAtivo(), naBateria, tcp, grupos));
    }
}
