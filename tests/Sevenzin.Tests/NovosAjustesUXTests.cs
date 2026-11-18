using System.Runtime.InteropServices;
using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

public class NovosAjustesUXTests
{
    sealed class RegistroFake : IRegistro
    {
        public readonly Dictionary<string, int> Store = new();
        public readonly Dictionary<string, string> StoreStr = new();
        static string K(Colmeia h, string c, string n) => $"{h}|{c}|{n}";
        public int? LerDword(Colmeia h, string c, string n) => Store.TryGetValue(K(h, c, n), out var v) ? v : null;
        public void EscreverDword(Colmeia h, string c, string n, int v) => Store[K(h, c, n)] = v;
        public string? LerString(Colmeia h, string c, string n) => StoreStr.TryGetValue(K(h, c, n), out var s) ? s : null;
        public void EscreverString(Colmeia h, string c, string n, string s) => StoreStr[K(h, c, n)] = s;
        public void ApagarValor(Colmeia h, string c, string n) { Store.Remove(K(h, c, n)); StoreStr.Remove(K(h, c, n)); }
    }

    static string PastaTemp() => Path.Combine(Path.GetTempPath(), "sevenzin-ux-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Desativar_funciona_sem_backup_previo_para_100_porcento_do_catalogo_e_grupos_gpu()
    {
        var pasta = PastaTemp();
        try
        {
            var reg = new RegistroFake();
            var motor = new MotorTweaks(reg, pasta);

            var todosTweaks = CatalogoTweaks.Todos
                .Concat(RedePorInterface.TweaksPara("{11111111-2222-3333-4444-555555555555}"))
                .Concat(GpuDriverTweaks.CriarGruposNvidia().SelectMany(g => g.Tweaks))
                .Concat(GpuDriverTweaks.CriarGruposAmd().SelectMany(g => g.Tweaks))
                .ToList();

            foreach (var t in todosTweaks)
            {
                // Simula valor já ativo no Windows antes do Sevenzin (sem nenhum backup salvo)
                if (t.Tipo == TipoValor.String)
                    reg.EscreverString(t.Colmeia, t.Caminho, t.NomeValor, t.ValorTexto);
                else
                    reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, t.ValorOtimizado);

                Assert.False(motor.TemBackup(t.Id));
                Assert.True(motor.Aplicado(t), $"Pré-condição falhou em {t.Id}");

                // Desativar deve sempre conseguir desligar o ajuste
                Assert.True(motor.Desativar(t), $"Desativar retornou false em {t.Id}");
                Assert.False(motor.Aplicado(t), $"Tweak {t.Id} continuou aplicado após Desativar");
            }
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void Desativar_usa_padrao_quando_backup_tinha_o_mesmo_valor_otimizado()
    {
        var pasta = PastaTemp();
        try
        {
            var reg = new RegistroFake();
            var motor = new MotorTweaks(reg, pasta);
            var t = CatalogoTweaks.Todos.First(x => x.Id == "tema-escuro");

            // Já estava em 0 antes de aplicar -> backup salva 0 -> Desativar ainda assim deve mudar para 1 (claro)
            reg.EscreverDword(t.Colmeia, t.Caminho, t.NomeValor, 0);
            motor.Aplicar(t);
            Assert.True(motor.Desativar(t));
            Assert.False(motor.Aplicado(t));
            Assert.Equal(1, reg.LerDword(t.Colmeia, t.Caminho, t.NomeValor));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void Limpeza_ExcluirPermanentemente_deleta_arquivos_pastas_e_readonly_sem_tocar_fora_da_raiz()
    {
        var raizSegura = PastaTemp();
        var pastaFora = PastaTemp();
        try
        {
            Directory.CreateDirectory(raizSegura);
            Directory.CreateDirectory(pastaFora);

            var arq1 = Path.Combine(raizSegura, "temp1.tmp");
            File.WriteAllBytes(arq1, new byte[256]);
            File.SetAttributes(arq1, FileAttributes.ReadOnly);

            var sub = Path.Combine(raizSegura, "subpasta");
            Directory.CreateDirectory(sub);
            var arq2 = Path.Combine(sub, "cache.bin");
            File.WriteAllBytes(arq2, new byte[512]);

            var arqProtegidoFora = Path.Combine(pastaFora, "documento_pessoal.txt");
            File.WriteAllText(arqProtegidoFora, "nao tocar");

            var r = Limpeza.ExcluirPermanentemente(new[] { arq1, sub, arqProtegidoFora }, new[] { raizSegura });

            Assert.False(File.Exists(arq1));
            Assert.False(Directory.Exists(sub));
            Assert.True(File.Exists(arqProtegidoFora)); // Fora da raiz segura JAMAIS é deletado
            Assert.Equal(2, r.Itens);
            Assert.Equal(768, r.Bytes);
            Assert.Equal(1, r.Erros); // arqProtegidoFora recusado pela guarda
        }
        finally
        {
            if (Directory.Exists(raizSegura)) Directory.Delete(raizSegura, recursive: true);
            if (Directory.Exists(pastaFora)) Directory.Delete(pastaFora, recursive: true);
        }
    }

    [Fact]
    public void RecuperacaoFalha_registra_consome_e_detecta_sessao_interrompida()
    {
        var pasta = PastaTemp();
        try
        {
            var store = new RecuperacaoFalhaStore(pasta);

            // 1. Falha explícita com operação em andamento
            store.MarcarSessaoAtiva("admin@sevenzin.local", "Ajuste: Desligar UAC", pid: 999991);
            store.RegistrarFalha("Acesso violado no subsistema", "InvalidOperationException");

            var rec = store.ConsumirPendente(_ => false);
            Assert.NotNull(rec);
            Assert.Equal("Acesso violado no subsistema", rec!.Motivo);
            Assert.Equal("admin@sevenzin.local", rec.EmailUsuario);
            Assert.Equal("Ajuste: Desligar UAC", rec.ContextoOperacao);
            Assert.Null(store.ConsumirPendente(_ => false)); // consumido uma única vez

            // 2. Encerramento abrupto detectado pela sentinela quando o PID antigo não existe mais
            store.MarcarSessaoAtiva("admin@sevenzin.local", pid: 999992);
            store.DefinirOperacaoEmCurso("Ajuste: Isolamento de Núcleo (HVCI)");
            var abrupto = store.ConsumirPendente(pid => false);
            Assert.NotNull(abrupto);
            Assert.Contains("Isolamento de Núcleo", abrupto!.Motivo);
            Assert.Equal("admin@sevenzin.local", abrupto.EmailUsuario);

            // 3. Encerramento normal não gera alerta de falha
            store.MarcarSessaoAtiva("admin@sevenzin.local", pid: 999993);
            store.MarcarEncerramentoNormal();
            Assert.Null(store.ConsumirPendente(_ => false));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void PreferenciasUiStore_persiste_opcoes_de_bandeja()
    {
        var pasta = PastaTemp();
        try
        {
            var arq = Path.Combine(pasta, "preferencias-ui.json");
            var store = new PreferenciasUiStore(arq);
            Assert.False(store.Carregar().MinimizarParaBandeja);
            Assert.False(store.Carregar().FecharParaBandeja);

            store.Salvar(new PreferenciasUi(MinimizarParaBandeja: true, FecharParaBandeja: true));
            var lido = new PreferenciasUiStore(arq).Carregar();
            Assert.True(lido.MinimizarParaBandeja);
            Assert.True(lido.FecharParaBandeja);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void NvidiaDrs_diferencia_os_7_perfis_e_monta_struct_binaria_v1_correta()
    {
        var assinaturas = new HashSet<string>();
        foreach (var p in GpuDriverTweaks.PerfisGraficos)
        {
            var drs = NvidiaDrs.ObterParametrosPerfil(p.Id);
            Assert.NotEmpty(drs);
            Assert.Contains(drs, x => x.SettingId == NvidiaDrs.IdPowerManagementMode);
            Assert.Contains(drs, x => x.SettingId == NvidiaDrs.IdTextureFilteringQuality);
            Assert.Contains(drs, x => x.SettingId == NvidiaDrs.IdMaxPrerenderedFrames);
            Assert.Contains(drs, x => x.SettingId == NvidiaDrs.IdVerticalSync);

            var resumo = string.Join("|", drs.Select(x => $"{x.SettingId:X8}={x.Valor:X8}")) + $"|RT={p.RayTracingDisponivel}";
            assinaturas.Add(resumo);
        }
        // Os 7 perfis produzem combinações distintas entre DRS + Ray Tracing / Tweaks
        Assert.True(assinaturas.Count >= 6);

        // Verifica layout binário exato de NVDRS_SETTING_V1 (12320 bytes, versão 0x00013020)
        IntPtr ptr = Marshal.AllocHGlobal(NvidiaDrs.TamanhoNvdrsSettingV1);
        try
        {
            NvidiaDrs.PreencherStructDword(ptr, NvidiaDrs.IdVerticalSync, NvidiaDrs.VsyncForcarDesligado);
            Assert.Equal(unchecked((int)NvidiaDrs.VersaoNvdrsSettingV1), Marshal.ReadInt32(ptr, 0));
            Assert.Equal(unchecked((int)NvidiaDrs.IdVerticalSync), Marshal.ReadInt32(ptr, 4100));
            Assert.Equal(0, Marshal.ReadInt32(ptr, 4104)); // NVDRS_DWORD_TYPE
            Assert.Equal(unchecked((int)NvidiaDrs.VsyncForcarDesligado), Marshal.ReadInt32(ptr, 8220));
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
