# Paridade funcional — Sevenzin vs programa original (`geandre.exe`)

Fonte: análise estática já produzida — [`catalogo-otimizacoes.md`](catalogo-otimizacoes.md) e [`otimizacoes-originais/`](otimizacoes-originais/) (338 escritas de registro, 110 chaves, 299 comandos). **O executável não foi executado.** O `honest (2)/geandre.exe` é byte‑idêntico ao já analisado (SHA256 `70dc5163…`).

## Como ler a classificação
**a)** Implementada e validada (com teste automatizado ou evidência) · **b)** Implementada, não validada · **c)** Parcial · **d)** Ausente · **e)** Incompatível/não aplicável (proibida pelo charter ou fora do escopo seguro).

> **Resumo honesto:** o Sevenzin **não busca paridade 1:1** com o original. Ele reconstruiu um **subconjunto seguro e reversível** de otimizações e **excluiu** deliberadamente o que é perigoso. **Atualização (12/11/2026):** por decisão do dono, a trava `GuardaSeguranca` foi removida e o conjunto antes "fora do escopo" passou a ser **em escopo** — implementado por fases com aviso, confirmação e reversão (ver `revisao_ajustes.md` §4 e o plano em `roadmap-fases-telas.md`). As linhas **e** abaixo vão migrando para **b** conforme as fases concluem.

## 1. Otimizações (tweaks de registro/sistema)

| Funcionalidade no original | App novo | Classe | Evidência / nota |
|---|---|---|---|
| Win32PrioritySeparation (foreground) | `priority-separation` (HKLM, A/B) em Ajustes | **b** | `CatalogoTweaks`; aplica/desfaz/auditado; efeito real exige admin (não validado automaticamente) |
| SystemResponsiveness (MMCSS) | `mmcss-responsiveness` em Ajustes | **b** | idem |
| HAGS (HwSchMode) | `hags` em Ajustes | **b** | idem |
| Fast Startup off (HiberbootEnabled) | `fast-startup-off` em Ajustes | **b** | idem |
| GameDVR/Game Bar off | `gamedvr-off` + `gamedvr-captura-off` (AppCaptureEnabled) | **b** | HKCU, sem admin |
| ID de publicidade off | `advertising-id-off` | **b** | HKCU |
| Sugestões de conteúdo off | `sugestoes-off`, `sugestoes-config-off`, `apps-promovidos-off`, `dicas-windows-off` | **b** | HKCU — 4 chaves do grupo `suggestions` do original |
| Animações da barra off | `taskbar-animations-off` | **b** | HKCU |
| Motor reversível (prévia, backup, desfazer, auditoria) | `MotorTweaks` | **a** | testes automatizados do motor; a antiga trava `GuardaSeguranca` foi removida em 12/11/2026 (ver `revisao_ajustes.md` §4) |
| Perfil MMCSS "Games" (GPU Priority etc.) | — | **d** | não implementado |
| Afinidade/prioridade por jogo (per‑título) | Jogos: perfil por `.exe`, **temporário** | **c** | escopo reduzido e reversível (não aplica afinidade permanente) |
| IFEO PerfOptions por processo | — | **d** | risco em processos do sistema |
| TdrDelay / Preferência de GPU / Power gating AMD / AMD DVR‑Chill | — | **d** | não implementados (risco/baixo retorno) |
| Overclock de clock/memória do driver | — | **e** | não é "otimização segura" (charter) |
| Limpeza de driver (DDU) | GPU é **somente leitura** | **e** | ação destrutiva; app não usa DDU |
| Plano de energia (~100 `powercfg /setacvalueindex`) | Windows: recomenda "Alto desempenho" | **c** | sem os 100 índices sem proveniência |
| Rede/latência (TcpAckFrequency, throttling, offloads…) | — | **d** | A/B dependente de NIC; não implementado |
| Memória/armazenamento (DisablePagingExecutive, MMAgent) | — | **d** | debatível; não implementado |
| Write cache forçado (`CacheIsPowerProtected`) | — | **e** | risco de perda de dados (charter) |
| DPC/MSI/IRQ affinity | — | **d** | mascara instabilidade; não implementado |
| Serviços/tarefas não essenciais off | — | **d** | não implementado como toggles |
| Telemetria/privacidade (AllowTelemetry, Copilot, WER…) | `telemetria-minima` (AllowTelemetry=1, mínimo), `historico-atividades-off`, `experiencias-sob-medida-off`, `destaques-busca-off`, `advertising-id-off`, sugestões | **c** | subconjunto seguro ampliado; **não** zera telemetria (Home/Pro clampa em 1) nem desliga Copilot por política agressiva |
| Visual/UI (efeitos, transparência, tema, ponteiro) | `taskbar-animations-off`, `widgets-barra-off`, GameDVR | **c** | subconjunto |
| Debloat/Appx (remover apps, OneDrive, Edge) | — | **e** | alto impacto; charter cauteloso |
| Construtor de ISO/debloat do Windows | Windows: **excluído**, com aviso | **e** | instalação de SO; proibido pelo charter |

### 🔴 Conjunto antes proibido do original (Defender/UAC/HVCI/Spectre/Exploit Protection/SmartScreen/Windows Update/write‑cache)
Classe **e** até 12/11/2026 (a trava `GuardaSeguranca` recusava qualquer tweak nesses mecanismos). **Após a remoção
da trava por decisão do dono, foram implementados na F1.1** (com aviso de risco, confirmação explícita e desfazer):

| Mecanismo | Sevenzin | Classe | Evidência |
|---|---|---|---|
| UAC (EnableLUA=0) | `uac-off` em Ajustes + tela Proteção | **b** | motor testado; efeito real exige admin + reinício (manual) |
| Defender (tempo real / anti-spyware) | `defender-rt-off`, `defender-antispyware-off` | **b** | idem; Windows pode ignorar com Tamper Protection |
| SmartScreen | `smartscreen-off` (política EnableSmartScreen=0) | **b** | idem |
| HVCI (Isolamento de Núcleo) | `hvci-off` | **b** | risco CRÍTICO; requer reinício |
| Mitigações Spectre/Meltdown | `mitigacoes-off` (FeatureSettingsOverride) | **b** | requer reinício |
| Windows Update | `wu-pausar` (NoAutoUpdate=1) | **b** | política de máquina |
| Write-cache forçado | — | **d** | F1.2 (per-disk via Enum) |

Nenhum desses itens roda sem o usuário confirmar; todos mantêm prévia/backup/desfazer/auditoria do `MotorTweaks`.

## 2. Telas / fluxos

| Tela/fluxo | App | Classe | Evidência |
|---|---|---|---|
| Login + Admin (contas, papéis, auditoria) | Implementado | **a** | 11 testes de autenticação; render `v3/v4` |
| Início — monitores CPU/Memória/Latência | Implementado | **a** | leituras reais; render |
| Início — monitor **GPU (%)** | Implementado | **b** | `LeitorGpuUso` (GPU Engine); lê valor real; sob carga = validação manual |
| Ajustes (23 tweaks, motor reversível) | Implementado | **a**/**b** | motor testado; grupos seguros + segurança (7) com confirmação; efeito por tweak exige admin |
| Proteção (estado real + toggles de segurança) | Implementado | **b** | `ProtecaoView` (UAC, Defender, SmartScreen, HVCI, mitigações, WU, Secure Boot, TPM) com confirmação; validação manual |
| GPU (diagnóstico só leitura) | Implementado | **a** | WMI; render |
| Instaladores (fontes oficiais, assinatura) | Implementado | **c** | fluxo seguro; catálogo reduzido |
| Jogos (perfil por `.exe` temporário) | Implementado | **c** | escopo seguro |
| Capturas (local, sem webhook) | Implementado | **b** | captura local; modos ricos ausentes |
| BIOS / Windows (informativas) | Implementado | **a** | estado só‑leitura real + instruções; render `v6` |
| Configurações / Inventário / Telemetria / Benchmark | Implementado | **b** | funcionais; render |

## 3. Cruzamento com o inventário de engenharia reversa

Mapa dos documentos de [`engenharia-reversa/`](engenharia-reversa/) (itens `[CONFIRMADO]`) para a classe de paridade.
`a/b` = implementado · `c` = parcial/subconjunto seguro · `d` = ausente (decisão de risco) · `e` = fora do escopo (charter).

| inventário (doc) | item `[CONFIRMADO]` | classe | no Sevenzin |
|---|---|---|---|
| 04 §1 | KeyAuth (login/licença/2FA/HWID/webhook) | **e** | login próprio e-mail+senha (bcrypt); sem KeyAuth/HWID/Discord |
| 04 §2 / 05 §1 | Perfil de hardware + WMI + PCI IDs | **a/b** | `Coletores`, `Inventario`, `TopologiaCpu` |
| 04 §3 | 12 grupos de tweaks | **a/b** (seguros) · **e** (uac) | `CatalogoTweaks` (16) — ver §1; grupo `uac` recusado pela trava |
| 04 §4-5 | Debloat Appx + remover Edge/OneDrive | **e** | não replicado (alto impacto sem opt-in) |
| 04 §6 / 11 | Injeção de drivers, ISO/USB, OfflineHive | **e** | instalação de SO + GPL; fora do escopo |
| 04 §7 / 05 §6-8 | Catálogo de instaladores + winget allowlist | **c** | `CatalogoUtilitarios` (fontes oficiais + assinatura); só benignos |
| 04 §8 / 05 §4 | CPU Sets, afinidade, prioridade, boost, working set, `timeBeginPeriod` | **b** | `Jogos.cs`: afinidade+prioridade, revertido ao fechar; working set (`TrimWorkingSet`) e timer 1 ms (`TimerAltaResolucao`) opt-in por jogo, revertidos ao fechar |
| 04 §9 | Captura + anotação | **b** | `Capturas` local, sem webhook |
| 04 §10 / 06 F12 | Assistente IA/humano via webhook | **e** | não replicado (exfiltração) |
| 05 §2 | Enumeração de modos de vídeo (DEVMODE/Hz) | **b** | `LeitorModosVideo` (`EnumDisplaySettingsW`) + cartão na `GpuView`; só leitura |
| 05 §3 | FPS/frametime, temp/clock de GPU/CPU | **d** | **confirmado inexistente no original**; seria feature nova (`BenchmarkView` já faz frametime interno A/B) |
| 05 §3 | Uso de GPU (%) | **b** | `LeitorGpuUso` (GPU Engine) |
| 05 §3 | Uso de CPU (%) | **b** | `LeitorCpuUso`/`MedidorCpuUso` (`GetSystemTimes`, delta puro) no cartão da Home |
| 05 §7 (lacuna) | Fila de instaladores persistida | **b** | `FilaInstaladores` (JSON local atômico) + seção "Fila" na `InstaladoresView`; o original não persistia |
| 06 F13 | Atualização autenticada | **d** | não implementado |

Detalhe e passo a passo em [`engenharia-reversa/roadmap_implementacao.md`](engenharia-reversa/roadmap_implementacao.md).

## Pendências / lacunas registradas
- Validar os 16 tweaks aplicando/desfazendo em máquina real com admin (efeito no registro) — hoje só o **motor** é testado automaticamente.
- Monitor de GPU: confirmar que o `%` acompanha carga real (jogo/benchmark) — teste manual.
- Categorias **d** (rede, memória, DPC/MSI, serviços) não foram implementadas por decisão de risco/retorno; adicionar só com A/B e reversão testada, se o produto exigir.
- Nenhuma funcionalidade foi declarada "paridade completa" apenas por existir tela/botão: a coluna Classe reflete o que executa e é verificável.
