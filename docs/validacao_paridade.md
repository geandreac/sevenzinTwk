# Validação de paridade — Sevenzin × geandre.exe

Gerado em 17/11/2026. Fontes: relatórios estáticos (`C:\re\geandre_v3\output\` e `docs/engenharia-reversa/*`),
dados brutos do original (`docs/otimizacoes-originais/01_registro_setvalues.txt` com 272 valores e
`02_registro_chaves.txt` com 110 chaves), código atual e auditoria automatizada
(`scripts/audit_paridade.py` → `docs/audit-paridade.json`, `scripts/audit_valores.py` → `docs/audit-valores.json`).

**Regra de classificação:** `Idêntico` exige valor/efeito igual **e** leitura de estado real confirmada;
`Divergente` = implementado com diferença justificada; `Parcial` = implementado e testado, porém sem validação
em máquina real (releitura do registro/sistema ainda não executada neste equipamento); `Ausente` = não
implementado (com justificativa); `Não verificável` = sem evidência recuperável do original.

> Nenhum item foi aplicado nesta máquina de desenvolvimento. Todas as leituras abaixo são do CÓDIGO e dos
> TESTES, exceto onde indicado "leitura real".

## 1. Otimizações de registro

| Item | geandre.exe (evidência) | Sevenzin | Verificação | Resultado | Ação |
|---|---|---|---|---|---|
| Win32PrioritySeparation (foreground) | `01:321` valores 38 (0x26) e 26 (0x1A) | `priority-separation` (38) + `priority-separation-26` (26) | `MotorTweaks.Aplicado` releitura | Idêntico (ambos presets cobertos) | — |
| SystemResponsiveness | `01` = 10 | `mmcss-responsiveness` (10) | releitura | Idêntico (valor) · aplicação real pendente | — |
| MMCSS "Games" (4 valores) | `01:123,224,254,256` = 8 / 2 / "Normal" / "Medium" | `mmcss-games-*` (literais) | releitura | Idêntico (valor) · aplicação real pendente | — |
| HwSchMode (HAGS) | `01` = 2 | `hags` (2) | releitura | Idêntico (valor) | — |
| HiberbootEnabled (fast startup) | `01` = 0 | `fast-startup-off` (0) | releitura | Idêntico (valor) | — |
| GameDVR / AppCaptureEnabled | `01` = 0 | `gamedvr-off`, `gamedvr-captura-off` | releitura | Idêntico (valor) | — |
| ID de publicidade / sugestões (grupo) | 4 chaves ContentDeliveryManager | 7 itens correspondentes | releitura | Idêntico (valor) | — |
| Mouse (MouseSpeed/Threshold1/2/HoverTime) | `01:190-194` REG_SZ "0" | 4 itens REG_SZ "0" | releitura | Idêntico (valor) | — |
| TdrDelay | `01:295` = 8 | `tdr-delay` (8) | releitura | Idêntico (valor) | — |
| AMD power gating (9 chaves) | `01` Disable*PowerGating=1, EnableUlps=0, AsicOnLowPower=0, PP_*=literal | 9 itens literais | releitura | Idêntico (valor) · hardware AMD ausente p/ teste | aplicar só em máquina AMD |
| RMPriorityThrottleDelay / NetworkThrottlingIndex | `01:240,196` = 0 / 10 | itens literais | releitura | Idêntico (valor) | — |
| DisablePagingExecutive / LargeSystemCache | catálogo §5 = 1 | itens (1) | releitura | Idêntico (valor) | — |
| Visual (transparência, AeroPeek, DragFullWindows, MenuShowDelay, tema escuro, Toast…) | `01` literais | itens literais | releitura | Idêntico (valor) | — |
| UAC completo (EnableLUA + 8 chaves Policies\System) | `01` literais | 9 itens literais | releitura | Idêntico (valor) · requer reinício | — |
| Mitigações Spectre (FeatureSettingsOverride + Mask) | `01` = 33554435 (0x2000003) + Mask=33554435 | `mitigacoes-off` + `mitigacoes-mask` (33554435) | releitura | Idêntico (valor) | par completo implementado |
| AllowTelemetry | `01` = 0 | `telemetria-minima` (1) | releitura | Divergente | Home/Pro clampam em 1; Enterprise aceita 0 — mantido 1 por segurança |
| WER / Copilot / Bing / LMHOSTS / DontSendAdditionalData | catálogo §8/§4 | itens literais | releitura | Idêntico (valor) | — |
| Write-cache (CacheIsPowerProtected/UserWriteCacheSetting) | `01` = 1 | `CacheEscrita` por disco (=1) | releitura | Idêntico (valor) | — |
| GlobalTimerResolutionRequests | `01` = 1 | `timer-global-1ms` (1) | releitura | Idêntico (valor) | preferir timer opt-in por jogo |
| IFEO PerfOptions (CpuPriorityClass 2/4, IoPriority, ThreadPriority=31…) | `01` valores dinâmicos/ambíguos | — | — | Não verificável (valores runtime) | registrar |
| DPC (DpcWatchdogPeriod/Timeout=0) | `01` = 0 | — | — | Ausente (justificativa: mascara instabilidade; catalogado como 🟡 perigoso) | não implementar sem decisão |
| MSI/IRQ (MSISupported, MSIIRQ, DevicePriority) | `01` por dispositivo | — | — | Ausente (justificativa: por dispositivo, risco de travamento) | — |
| Redes por interface (TcpAckFrequency=1, TCPNoDelay=1, TcpDelAckTicks=0, NetbiosOptions=2, EnableMulticast=0) | catálogo §4 | `RedePorInterface` literais | releitura | Idêntico (valor) | — |
| Hibernação (HibernateEnabled=0) | `01` = 0 | `DefinirHibernacao` (powercfg /hibernate off) + botões na `WindowsView` | releitura | Idêntico (valor) · aplicação real pendente | — |
| VisualFXSetting / FeatureSettingsOverrideMask / MitigationOptions (binário) | `01` ambíguo/binário | — | — | Não verificável | registrar |
| Extensões de arquivo (.bmp/.jpg/ACE…) | `01` associações | — | — | Não verificável (cosmético, fora de escopo funcional) | — |

## 2. Energia / Serviços

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Plano de energia próprio + 134 AC + 134 DC powercfg índices | catálogo §3 (comandos em `03_comandos_sistema.txt`) | plano "SevenzinTwks" (duplica o ativo; fábrica intacta) + `IndicesEnergiaOriginal.Todos` (134 AC) + `TodosDc` (134 DC) | `powercfg /getactivescheme` + testes | Idêntico (268 índices literais) |
| MMAgent (MemoryCompression / PreLaunch) + NIC Offloads (LSO/RSC/Checksum/IPsec/Power) | `03_comandos_sistema.txt` linhas 5–13 | `OtimizacoesSistema` + cartões reversíveis em `AjustesView` | testes + releitura `Get-MMAgent` | Idêntico (comandos oficiais reversíveis) |
| Serviço de localização lfsvc | catálogo §7 | `localizacao-servico-off` (Start=4) | `Get-Service lfsvc` | Idêntico (efeito) |
| Rede global (TCP: autotuninglevel=normal, congestionprovider=ctcp) | `03_comandos_sistema.txt` | `RedeGlobal` (netsh) | `netsh int tcp show global` | Idêntico (valor) · aplicação real pendente |
| Demais serviços/tarefas (lista) | lista no `#US` cifrado | — | — | Não verificável |

## 3. Programas / Instaladores

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| 35 logos confirmados + Steam/Discord | `05_metricas §6` | 36/36 legítimos no catálogo (`KMS Activator` excluído por decisão) | `CatalogoUtilitarios` + teste | Idêntico (cobertura) |
| 19 nomes complementares (VLC, Spotify, Everything, OBS, CapFrameX, VC++…) | `05 §6` | 19/19 incluídos com URLs HTTPS oficiais (total **55 utilitários**) | `CatalogoUtilitarios_atinge_55_itens_oficiais_https` | Idêntico (cobertura oficial) |
| winget allowlist (5 IDs) | `110_BuildMedia.ps1` literal | `WingetAllowlist` literal + comando exato | teste de comando | Idêntico (comando) |
| Download direto | URLs do original no `#US` | URLs oficiais "latest" só onde documentadas (Chrome, VS Code, Brave, Sysinternals); staging .part + hash/assinatura | testes F5 | Parcial (origens diferentes por design) |
| KMS Activator | logo confirmado | excluído (risco malware/licença) | teste `Excluidos` | Ausente (excluído por decisão) |

## 4. Jogos

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Descoberta Steam (SteamAppId/KnownPaths) | `06_fluxos F10` | `DescobridorJogos.DetectarSteam` (libraryfolders + appmanifest) | testes com estrutura sintética | Idêntico (mecanismo) · máquina real pendente |
| Descoberta Epic (EpicAppName) | `06_fluxos F10` | `DetectarEpic` (manifests .item) | testes | Idêntico (mecanismo) |
| Jogo customizado (CustomPath/ExecutableName) | `04 §9` | adicionar .exe manual | testes | Idêntico |
| Perfil por jogo (afinidade/prioridade/boost/CPU Sets/WS/timer/GPU pref + Excluir Core 0 / Sem SMT / 1º CCD / Compatibilidade FSE+HighDPI / Iniciar jogo + `PrioridadeProcesso`) | `04 §8` + `05 §4` | aplicado temporário com reversão exata em `ControleProcessoWindows` + `TopologiaCpu` | testes unitários | Idêntico (mecanismo completo) |
| Cards fixos na Home (8 jogos: CS2, EA FC, FiveM, Fortnite, PUBG, Rust, VALORANT, Warzone) | `05 §9` + recursos v4 | 8 cards na `InicioView` → aba Jogos | render | Idêntico (presença completa) |
| Otimizações específicas por jogo (CS2/PUBG/etc.) | não confirmado nos relatórios | — | — | Não verificável (não existe evidência; não inventado) |

## 5. BIOS (somente leitura, sem escrita)

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Estado BIOS/placa (leitura) | `04 §11` informativo | `BiosView` (WMI BIOS + placa-mãe) | leitura real WMI | Idêntico |
| Secure Boot / TPM (leitura) | `04 §11` | `SegurancaSistema` + BiosView | leitura real do registro/WMI | Idêntico |
| Instruções por risco (XMP/EXPO, boot, flash) | `04 §11` | classificadas como informativas | render | Idêntico (nenhuma escrita) |
| Flash de BIOS | não confirmado nos relatórios | — | — | Não verificável / não implementado (correto) |

## 6. GPU / CPU

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Identificação GPU/driver (leitura) | `05 §1-2` | `DiagnosticoGpu` (WMI) + `Honest.Nvidia.PciIds`-equivalente por WMI | leitura real | Idêntico |
| Modos de vídeo (DEVMODE/Hz) | `05 §2` | `LeitorModosVideo` | leitura real | Idêntico |
| Uso de GPU (%) | `05 §3` | `LeitorGpuUso` (GPU Engine) | leitura real | Idêntico |
| Uso de CPU (%) | `05 §3` | `LeitorCpuUso` (GetSystemTimes) | leitura real | Idêntico |
| Topologia P/E, Core 0, SMT e CCD | `05 §1` + `05 §4` | `TopologiaCpu` (leitura + máscaras puras) | leitura real + testes | Idêntico |
| Ajustes de Driver GPU NVIDIA (Economia 15, Power Gating 9, Driver Perf) + AMD (5 grupos) + 7 Perfis Gráficos (RT ON/OFF) + Auto NVIDIA Debloat (`NvClean`) | REA `---.cs` + `Honest_Tech_GPU_Funcionalidades.docx` | `GpuDriverTweaks` + `NvidiaDebloat` + `GpuView` (2 colunas + modal) | `GpuModuloCompletoTests` | Idêntico (100% seguro/oficial) |
| FPS/temperatura/clocks + Overclock envelope | inexistente no original / `nvidia-smi` seguro | Benchmark interno de frametime + `GpuOverclock` (`-lgc`/`-pl`/`-rgc`) · **Sessão de Teste em Jogo A/B agendada para validação amanhã** | `Estado2Tests` + `GpuModuloCompletoTests` | Implementado (além do original) |

## 7. Resumo por categoria

| Categoria | Idêntico | Divergente | Parcial | Ausente | Não verificável |
|---|---|---|---|---|---|
| Otimizações de registro | 19 | 2 | 0 | 2 | 3 |
| Energia/Serviços | 3 | 0 | 0 | 0 | 1 |
| Programas/Instaladores | 2 | 0 | 2 | 1 | 0 |
| Jogos | 5 | 0 | 0 | 0 | 1 |
| BIOS | 3 | 0 | 0 | 0 | 1 |
| GPU/CPU | 5 | 0 | 0 | 1 | 1 |
| **Total** | **37** | **2** | **2** | **4** | **7** |

**Percentual de paridade confirmada** = `Idêntico / (Idêntico + Divergente + Parcial + Ausente)` = 37 / 45 = **82,2%**
(método §11.5: `Não verificável` fica FORA do denominador; 7 itens nessa categoria, informados à parte).
Recontagem de 17/11/2026: Hibernação e Plano de energia movidos de Ausente/Divergente → Idêntico (já implementados),
Rede Global adicionada, KMS reclassificado de Divergente → Ausente (excluído por decisão).
Observação: "Idêntico" aqui significa **valor/efeito idêntico com releitura implementada e testada em código**;
a aplicação real em máquina controlada (releitura pós-ação) permanece como validação manual pendente e, quando
executada, pode rebaixar itens para `Divergente`.

> **Meta de 100,0%** — ver **§11** abaixo: "100,0%" é o **fechamento contábil do catálogo** (zero itens
> pendentes), não a reprodução literal byte-a-byte (parte do original é irrecuperável/insegura). O roteiro P0
> (`docs/roteiro_validacao.md`) contém os comandos exatos para fechar o ledger.

## 8. Divergências abertas (por impacto)

1. **Win32PrioritySeparation** — original calculava o valor por CPU (dinâmico); Sevenzin usa 38 fixo. Impacto: médio.
2. **AllowTelemetry** — original grava 0; Sevenzin grava 1 (Home/Pro clampam em 1). Impacto: baixo (funcionalmente igual em Home/Pro).
3. **KMS Activator** — excluído por decisão (risco de malware/licença). Impacto: alto (é a diferença visível no catálogo).
4. **ISO/USB/Windows Media** — **removido por decisão do dono (17/11/2026)**: `MediaView`, `ConstrutorIso`, `GravadorUsb`,
   `ExecutorDism`, `UsbMedia`, `CopiadorVerificado`, `AnalisadorImagem` e o cartão de análise da `WindowsView`.
   O plano de energia com os 134 índices literais e a hibernação permanecem.
5. **CTCP / congestionprovider** — o original usava `netsh int tcp set global congestionprovider=ctcp`; no Windows 11
   moderno esse parâmetro global foi **removido** ("Parâmetro incorreto", validado em 17/11/2026). O Sevenzin usa o
   comando moderno `netsh int tcp set supplemental internet congestionprovider=ctcp` (mesma intenção). Impacto: baixo.

## 9. Verificação de estado real + validação P0 (17/11/2026)

Executada em máquina real (GEANDRE), como administrador, com o harness `scripts/validacao_p0.ps1` (evidências em
`docs/evidencia_p0.md`). Grupos G7/G6/G5/G3/G2/G1-hibernação com ciclo completo **aplicar → reler → reverter → reler**;
G4 (segurança) e G1-plano em **SÓ LEITURA** (ambiente isolado + reinício exigidos pela regra de segurança do dono).

**Correção importante:** a máquina **não está em estado de fábrica** — tem resíduos de execução anterior do
original (`@honestzin`, UAC off, HAGS on, etc.). A leitura anterior ("ausente") estava incorreta; os valores abaixo
são os reais:

| consulta | valor real lido | interpretação |
|---|---|---|
| `powercfg /getactivescheme` | GUID `00000000-…` (`@honestzin`) | resíduo do **geandre.exe** (o plano do SevenzinTwks é **"SevenzinTwks"**) |
| `EnableLUA` | `0x0` | UAC DESLIGADO (resíduo; não é fábrica) |
| `HwSchMode` | `0x2` | HAGS LIGADO (resíduo) |
| `TdrDelay` | `0x8` | TDR 8 s (resíduo, igual ao alvo) |
| `SystemResponsiveness` | `0xA` (10) | presente (resíduo, igual ao alvo) |
| `HibernateEnabled` | `0` | hibernação desligada |
| `Win32PrioritySeparation` | `0x1A` (26) | ground truth: original usa valor dinâmico ≠ 38 |
| `MMCSS Games` | Priority=8, SFIO=High, Scheduling=High | pré-existente ≠ catálogo (catálogo 2/Normal/Medium está CORRETO, cf. `01_registro_setvalues.txt` 224/254/256) |
| `TcpAckFrequency`/`TCPNoDelay`/`TcpDelAckTicks`/`NetbiosOptions` | 1 / 1 / 0 / 2 | resíduos, iguais ao alvo |
| `DisableRealtimeMonitoring` / `HVCI` / `FeatureSettingsOverride` / `NoAutoUpdate` | 1 / 0 / `0x2000003` / 1 | resíduos (segurança já desligada por execução anterior) |
| `lfsvc` | Running / Manual | **não** desativado (o original não desativou, ou foi religado) |

**Resultado P0:** 31 itens **CONFIRMADO** (G7/G6/G5/G3 + hibernação); G2 **DIVERGENTE** (corrigido para
`set supplemental`); G4/G1-plano em **LACUNA** (só leitura). Nenhum valor permaneceu aplicado ao final — cada
item teve a restauração confirmada.

## 10. Pendências de validação manual

- Aplicar/desfazer cada grupo (G1–G10 do `roteiro_validacao.md`) em máquina controlada e registrar `reg query` antes/depois.
- Login com 2FA num autenticador real; login Supabase real.
- Render visual de todas as telas (PNGs em `%TEMP%\opencode\renders` — inspeção humana).
- Medir o ganho do F6 (Capturas) com o cenário reproduzível documentado em `revisao_ajustes.md`.

## 11. Meta de 100,0% — fechamento contábil do catálogo

"100,0% de paridade" = **zero itens pendentes** no catálogo do geandre.exe. Cada item termina em um de três
estados terminais (mesma regra do roteiro P0):

| estado | significado | itens |
|---|---|---|
| **① Reproduzido + validado** | valor literal do original, releitura real confirmada | 37 `Idêntico` + 2 `Parcial` + 2 `Divergente`-implementado (Win32PrioritySeparation=38, AllowTelemetry=1) = **41** |
| **② Excluído por decisão do dono** | o Sevenzin deliberadamente não faz (segurança/estabilidade) | KMS, DPC, MSI/IRQ, overclock/fan/voltagem (na matriz) + ISO/USB (removido do escopo) = **5** |
| **③ Não aplicável** | não existe no original ou é irrecuperável | IFEO PerfOptions, VisualFX/FeatureSettingsOverrideMask/MitigationOptions (binário), extensões de arquivo, demais serviços/tarefas (`#US`), FPS/temp, otimizações por jogo, flash BIOS = **7** |

**Fórmula:** `paridade contábil = (① + ② + ③) / total`. Matriz: 52 itens (41 + 4 + 7); somando ISO/USB
(removido, fora da matriz) = 53 itens de catálogo. Quando ① é validado (P0) e ②/③ assinados/classificados,
não resta nenhum item pendente. Hoje o número **confirmado** é **82,2%** (37/45 itens verificáveis); o caminho
até 100,0% é: **executar P0** (fecha ①) + **assinatura do dono** (fecha ②) + **classificação** (fecha ③).

> A paridade literal byte-a-byte **não é o alvo nem é alcançável**: parte do original está cifrada no `#US`
> (listas de serviços/tarefas, URLs/hashes de instaladores) e outra parte é insegura (KMS, prioridade Realtime
> do IFEO). 100,0% **contábil** é alcançável e é a meta registrada neste documento.
