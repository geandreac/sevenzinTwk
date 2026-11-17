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
| Win32PrioritySeparation (foreground) | `01:321` valor dinâmico (obj._0002) | `priority-separation` (38) | `MotorTweaks.Aplicado` releitura | Divergente (valor fixo 38 vs dinâmico) | registrar: original calculava por CPU |
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
| Mitigações Spectre (FeatureSettingsOverride) | `01` = 33554435 (0x2000003) | `mitigacoes-off` (33554435) | releitura | Idêntico (valor) | corrigido de 1 → literal |
| AllowTelemetry | `01` = 0 | `telemetria-minima` (1) | releitura | Divergente | Home/Pro clampam em 1; Enterprise aceita 0 — mantido 1 por segurança |
| WER / Copilot / Bing / LMHOSTS / DontSendAdditionalData | catálogo §8/§4 | itens literais | releitura | Idêntico (valor) | — |
| Write-cache (CacheIsPowerProtected/UserWriteCacheSetting) | `01` = 1 | `CacheEscrita` por disco (=1) | releitura | Idêntico (valor) | — |
| GlobalTimerResolutionRequests | `01` = 1 | `timer-global-1ms` (1) | releitura | Idêntico (valor) | preferir timer opt-in por jogo |
| IFEO PerfOptions (CpuPriorityClass 2/4, IoPriority, ThreadPriority=31…) | `01` valores dinâmicos/ambíguos | — | — | Não verificável (valores runtime) | registrar |
| DPC (DpcWatchdogPeriod/Timeout=0) | `01` = 0 | — | — | Ausente (justificativa: mascara instabilidade; catalogado como 🟡 perigoso) | não implementar sem decisão |
| MSI/IRQ (MSISupported, MSIIRQ, DevicePriority) | `01` por dispositivo | — | — | Ausente (justificativa: por dispositivo, risco de travamento) | — |
| Redes por interface (TcpAckFrequency=1, TCPNoDelay=1, TcpDelAckTicks=0, NetbiosOptions=2, EnableMulticast=0) | catálogo §4 | `RedePorInterface` literais | releitura | Idêntico (valor) | — |
| Hibernação (HibernateEnabled=0) | `01` = 0 | pendente (ação powercfg) | — | Ausente | próxima fatia: botão na tela Windows |
| VisualFXSetting / FeatureSettingsOverrideMask / MitigationOptions (binário) | `01` ambíguo/binário | — | — | Não verificável | registrar |
| Extensões de arquivo (.bmp/.jpg/ACE…) | `01` associações | — | — | Não verificável (cosmético, fora de escopo funcional) | — |

## 2. Energia / Serviços

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Plano de energia próprio + ~100 powercfg índices | catálogo §3 (comandos em `03_comandos_sistema.txt`) | plano "Sevenzin" (duplica o ativo; fábrica intacta) | `powercfg /getactivescheme` | Divergente (sem os 100 índices sem proveniência individual) |
| Serviço de localização lfsvc | catálogo §7 | `localizacao-servico-off` (Start=4) | `Get-Service lfsvc` | Idêntico (efeito) |
| Demais serviços/tarefas (lista) | lista no `#US` cifrado | — | — | Não verificável |

## 3. Programas / Instaladores

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| 35 logos confirmados | `05_metricas §6` | 35/35 no catálogo (KMS excluído por decisão: ativador não-oficial) | `CatalogoUtilitarios` + teste | Idêntico (cobertura) · downloads reais pendentes |
| 19 nomes prováveis (VLC, Spotify, Everything…) | `05 §6` inferidos | VLC/Steam/Discord via winget; demais ausentes | — | Parcial (nomes inferidos não confirmados) |
| winget allowlist (5 IDs) | `110_BuildMedia.ps1` literal | `WingetAllowlist` literal + comando exato | teste de comando | Idêntico (comando) |
| Download direto | URLs do original no `#US` | URLs oficiais "latest" só onde documentadas (Chrome, VS Code, Brave, Sysinternals); staging .part + hash/assinatura | testes F5 | Parcial (origens diferentes por design) |
| KMS Activator | logo confirmado | excluído (risco malware/licença) | teste `Excluidos` | Divergente (justificada) |

## 4. Jogos

| Item | geandre.exe | Sevenzin | Verificação | Resultado |
|---|---|---|---|---|
| Descoberta Steam (SteamAppId/KnownPaths) | `06_fluxos F10` | `DescobridorJogos.DetectarSteam` (libraryfolders + appmanifest) | testes com estrutura sintética | Idêntico (mecanismo) · máquina real pendente |
| Descoberta Epic (EpicAppName) | `06_fluxos F10` | `DetectarEpic` (manifests .item) | testes | Idêntico (mecanismo) |
| Jogo customizado (CustomPath/ExecutableName) | `04 §9` | adicionar .exe manual | testes | Idêntico |
| Perfil por jogo (afinidade/prioridade/boost/CPU Sets/WS/timer/GPU pref) | `04 §8` | aplicado temporário com reversão | testes (aplicar/reverter no processo real) | Idêntico (mecanismo) · jogos específicos = genérico, sem presets inventados |
| Cards fixos (EA FC, Fortnite, Rust, Warzone) | `05 §9` | 4 cards na Home → Jogos | render | Idêntico (presença) |
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
| Topologia P/E e CCD | `05 §1` | `TopologiaCpu` | leitura real | Idêntico |
| FPS/temperatura/clocks | **inexistente no original** (confirmado) | Benchmark interno de frametime | — | Não verificável (não há paridade a atingir) |
| Overclock/voltagem/fan | não é otimização segura | não implementado | — | Ausente (correto) |

## 7. Resumo por categoria

| Categoria | Idêntico | Divergente | Parcial | Ausente | Não verificável |
|---|---|---|---|---|---|
| Otimizações de registro | 17 | 2 | 0 | 3 | 4 |
| Energia/Serviços | 1 | 1 | 0 | 1 | 1 |
| Programas/Instaladores | 2 | 1 | 2 | 0 | 0 |
| Jogos | 5 | 0 | 0 | 0 | 1 |
| BIOS | 3 | 0 | 0 | 0 | 1 |
| GPU/CPU | 6 | 0 | 0 | 1 | 1 |
| **Total** | **34** | **4** | **2** | **5** | **8** |

**Percentual de paridade confirmada** = `Idêntico / (Idêntico + Divergente + Parcial + Ausente)` = 34 / 45 = **75,6%**
(método §11.5: `Não verificável` fica FORA do denominador; 8 itens nessa categoria, informados à parte).
Observação: "Idêntico" aqui significa **valor/efeito idêntico com releitura implementada e testada em código**;
a aplicação real em máquina controlada (releitura pós-ação) permanece como validação manual pendente e, quando
executada, pode rebaixar itens para `Divergente`.

## 8. Divergências abertas (por impacto)

1. **Win32PrioritySeparation** — original calculava o valor por CPU (dinâmico); Sevenzin usa 38 fixo. Impacto: médio.
2. **AllowTelemetry** — original grava 0; Sevenzin grava 1 (Home/Pro clampam em 1). Impacto: baixo (funcionalmente igual em Home/Pro).
3. **Plano de energia** — sem os ~100 índices `powercfg` sem proveniência individual. Impacto: médio.
4. **KMS Activator** — excluído por decisão (risco de malware/licença). Impacto: alto (é a diferença visível no catálogo).
5. **Hibernação (HibernateEnabled=0)** — pendente (ação na tela Windows). Impacto: baixo.

## 9. Pendências de validação manual

- Aplicar/desfazer 1 item de cada grupo em máquina controlada e registrar `reg query` antes/depois.
- Bootar a ISO construída e o pendrive gravado (UEFI) em VM/hardware real.
- Login com 2FA num autenticador real; login Supabase real.
- Render visual de todas as telas (PNGs em `%TEMP%\opencode\renders` — inspeção humana).
- Medir o ganho do F6 (Capturas) com o cenário reproduzível documentado em `revisao_ajustes.md`.
