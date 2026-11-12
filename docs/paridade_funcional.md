# Paridade funcional — Sevenzin vs programa original (`geandre.exe`)

Fonte: análise estática já produzida — [`catalogo-otimizacoes.md`](catalogo-otimizacoes.md) e [`otimizacoes-originais/`](otimizacoes-originais/) (338 escritas de registro, 110 chaves, 299 comandos). **O executável não foi executado.** O `honest (2)/geandre.exe` é byte‑idêntico ao já analisado (SHA256 `70dc5163…`).

## Como ler a classificação
**a)** Implementada e validada (teste automatizado/evidência) · **b)** Implementada, não validada (manual) · **c)** Parcial · **d)** Ausente · **e)** Não aplicável (fora de escopo, com justificativa).

> **Resumo honesto:** o Sevenzin **não busca paridade 1:1**. Reconstruiu um subconjunto **reversível e auditado** e, por decisão do dono (12/11/2026), o conjunto antes "proibido" passou a ser implementado por fases com **aviso, confirmação explícita e desfazer** (`revisao_ajustes.md` §4). O que continua fora (coluna **e**) tem justificativa registrada — nunca é esquecimento.

## 1. Otimizações (tweaks de registro/sistema)

| Funcionalidade no original | App novo | Classe | Evidência / nota |
|---|---|---|---|
| Win32PrioritySeparation (foreground) | `priority-separation` (HKLM, A/B) em Ajustes | **b** | `CatalogoTweaks`; aplica/desfaz/auditado; efeito real exige admin |
| SystemResponsiveness (MMCSS) | `mmcss-responsiveness` em Ajustes | **b** | idem |
| HAGS (HwSchMode) | `hags` em Ajustes | **b** | idem |
| Fast Startup off (HiberbootEnabled) | `fast-startup-off` em Ajustes | **b** | idem |
| GameDVR/Game Bar off | `gamedvr-off` + `gamedvr-captura-off` | **b** | HKCU, sem admin |
| ID de publicidade off | `advertising-id-off` | **b** | HKCU |
| Sugestões de conteúdo off | `sugestoes-off`, `sugestoes-config-off`, `apps-promovidos-off`, `dicas-windows-off` | **b** | HKCU — 4 chaves do grupo `suggestions` |
| Animações da barra off | `taskbar-animations-off` | **b** | HKCU |
| **Mouse (aceleração/limiares)** | `mouse-aceleracao-off`, `mouse-limiar1-off`, `mouse-limiar2-off` (**REG_SZ**, como o original) | **b** | F4; motor com suporte a string |
| **Localização** | `localizacao-ocultar` (HideSCALocation=1) + `localizacao-servico-off` (lfsvc Start=4, aviso de jogos) | **b** | F4; confirmados no original |
| Motor reversível (prévia, backup, desfazer, auditoria) | `MotorTweaks` (DWORD + string) | **a** | testes do motor; trava antiga removida por decisão (12/11/2026) |
| **Plano de energia "Sevenzin"** | `GestorEnergia` (duplica o plano ativo; fábrica intacta; restaurar) | **b** | `Energia.cs` + testes; admin + reversível |
| Perfil MMCSS "Games" (GPU Priority etc.) | — | **d** | não implementado |
| Afinidade/prioridade por jogo (per‑título) | Jogos: perfil por `.exe`, temporário, + working set/timer opt-in | **b** | revertido ao fechar; nunca Realtime |
| IFEO PerfOptions por processo | — | **d** | risco em processos do sistema |
| TdrDelay / Preferência de GPU / Power gating AMD / AMD DVR‑Chill | — | **d** | não implementados (risco/baixo retorno) |
| Overclock de clock/memória do driver | — | **e** | não é otimização segura |
| Limpeza de driver (DDU) | GPU **somente leitura** | **e** | ação destrutiva; app não usa DDU (DDU fica só no catálogo de downloads) |
| Rede/latência (TcpAckFrequency, throttling, offloads…) | — | **d** | A/B dependente de NIC |
| Memória/armazenamento (DisablePagingExecutive, MMAgent) | — | **d** | debatível; não implementado |
| DPC/MSI/IRQ affinity | — | **d** | mascara instabilidade |
| Serviços/tarefas não essenciais off | — | **d** | não implementado como toggles |
| Telemetria/privacidade | `telemetria-minima` (AllowTelemetry=1, mínimo), `historico-atividades-off`, `experiencias-sob-medida-off`, `destaques-busca-off`, `advertising-id-off`, sugestões | **c** | subconjunto; não zera telemetria |
| Visual/UI | `taskbar-animations-off`, `widgets-barra-off`, GameDVR | **c** | subconjunto |
| Debloat/Appx (remover apps, OneDrive, Edge) | — | **e** | alto impacto sem opt-in (registrado; não implementado) |

### Segurança (UAC/Defender/HVCI/SmartScreen/mitigações/WU/write-cache) — em escopo desde 12/11/2026
Implementados na F1 com aviso ALTO/CRÍTICO, confirmação explícita e desfazer:

| Mecanismo | Sevenzin | Classe |
|---|---|---|
| UAC (EnableLUA=0) | `uac-off` (requer reinício) | **b** |
| Defender (tempo real / anti-spyware) | `defender-rt-off`, `defender-antispyware-off` (Tamper Protection pode ignorar) | **b** |
| SmartScreen | `smartscreen-off` (política EnableSmartScreen=0) | **b** |
| HVCI (Isolamento de Núcleo) | `hvci-off` (CRÍTICO, requer reinício) | **b** |
| Mitigações Spectre/Meltdown | `mitigacoes-off` (FeatureSettingsOverride) | **b** |
| Windows Update | `wu-pausar` (NoAutoUpdate=1) | **b** |
| Write-cache forçado | `CacheEscrita` (CacheIsPowerProtected por disco, via Enum) + seção na Proteção | **b** |

## 2. Telas / fluxos

| Tela/fluxo | App | Classe | Evidência |
|---|---|---|---|
| Login (e-mail+senha, bcrypt, força bruta, bootstrap, **MFA TOTP**, **Supabase opcional**, DPAPI) | `LoginWindow` | **a** | `AutenticacaoTests`, `TotpELicencaTests`; render `v3_login` |
| Início (CPU `GetSystemTimes`, GPU, memória, latência) | `InicioView` | **a** | leituras reais; testes de delta |
| Ajustes (28 tweaks, motor DWORD+string reversível) | `AjustesView` | **a/b** | motor testado; efeito por tweak exige admin |
| Proteção (estado real + toggles confirmados) | `ProtecaoView` | **b** | F1; validação manual |
| GPU (driver, modos DEVMODE, link oficial) | `GpuView` | **a/b** | `ModosVideoTests` |
| Instaladores (36 utilitários oficiais + winget allowlist + fila persistida) | `InstaladoresView` | **b** | F3; `WingetTests`, `FilaInstaladoresTests` |
| Jogos (perfil temporário + **detecção Steam/Epic só leitura**) | `JogosView` | **b** | F8; `DescobridorJogosTests` (parsers puros) |
| Capturas (4 modos + editor + envio explícito) | `CapturasView` | **b** | F2 |
| BIOS (informativa: Secure Boot/TPM) | `BiosView` | **a/b** | leitura real |
| **Windows / Media** (estado + plano de energia + **análise de ISO só leitura via DISM**) | `WindowsView` | **c** | F7.1; construção de ISO/USB = lacuna registrada (F7.2) |
| **Assistente** (endpoint HTTPS do usuário, envio explícito, contexto com consentimento) | `AssistenteView` | **b** | F6; `AssistenteTests`; HumanMode = lacuna (comportamento de servidor) |
| Guias (6) | `GuiasView` | **b** | F4 |
| Notificações/Tarefas (histórico local + fila) | `NotificacoesView` | **b** | F4 |
| Perfil (conta, 2FA, licença local, resumo) | `PerfilView` | **b** | F5 |
| Configurações (iniciar com Windows, webhook de captura, endpoint do assistente) | `ConfiguracoesView` | **b** | F2/F6 |
| Inventário / Telemetria / Benchmark | `InventarioView`, `TelemetriaView`, `BenchmarkView` | **a/b** | testado; render |
| Sair | `Sair_Click` (relogin) | **a** | — |
| Área ADM | **App separado** `Sevenzin.Admin` (auditoria + acessos + segurança; local, fora do GitHub) | **b** | `AcessosTests`; commits `5b33972`/`ad7403f` |

## 3. Cruzamento com o inventário de engenharia reversa

| inventário (doc) | item `[CONFIRMADO]` | classe | no Sevenzin |
|---|---|---|---|
| 04 §1 | KeyAuth (login/licença/2FA/HWID/webhook) | **e→b** | substituído: login próprio (bcrypt + MFA TOTP local + Supabase opcional + licença local) — **sem HWID/Discord** |
| 04 §2 / 05 §1 | Perfil de hardware + WMI + PCI IDs | **a/b** | `Coletores`, `Inventario`, `TopologiaCpu` |
| 04 §3 | 12 grupos de tweaks | **a/b** | `CatalogoTweaks` (28, inclui Segurança/Mouse/Localização) |
| 04 §4-5 | Debloat Appx + remover Edge/OneDrive | **e** | não implementado (alto impacto sem opt-in) — registrado |
| 04 §6 / 11 | Injeção de drivers, ISO/USB, OfflineHive | **c/e** | **análise de ISO só leitura** implementada (F7.1); **construção/edição = lacuna registrada** (F7.2: ADK/IMAPI2FS) |
| 04 §7 / 05 §6-8 | Catálogo de instaladores + winget allowlist | **b** | 36 utilitários oficiais + allowlist literal de 5 IDs |
| 04 §8 / 05 §4 | CPU Sets, afinidade, prioridade, boost, working set, `timeBeginPeriod` | **b** | afinidade+prioridade+WS+timer opt-in por jogo, revertidos ao fechar |
| 04 §9 | Captura + anotação | **b** | 4 modos + editor + envio explícito (webhook do usuário) |
| 04 §10 / 06 F12 | Assistente IA/humano via webhook | **b/e** | endpoint HTTPS **configurado pelo usuário**, envio explícito, contexto com consentimento; **HumanMode = lacuna** (lado servidor) |
| 05 §2 | Enumeração de modos de vídeo (DEVMODE/Hz) | **b** | `LeitorModosVideo` + cartão na `GpuView` |
| 05 §3 | FPS/frametime, temp/clock de GPU/CPU | **d** | **confirmado inexistente no original**; seria feature nova (Benchmark interno existe) |
| 05 §3 | Uso de GPU (%) | **b** | `LeitorGpuUso` (GPU Engine) |
| 05 §3 | Uso de CPU (%) | **b** | `LeitorCpuUso` (GetSystemTimes) |
| 05 §7 (lacuna) | Fila de instaladores persistida | **b** | `FilaInstaladores` (JSON local atômico) |
| 06 F10 | Descoberta de jogos (SteamAppId/KnownPaths, EpicAppName) | **b** | `DescobridorJogos` (libraryfolders.vdf + appmanifest_*.acf + manifestos .item) — **novo, só leitura** |
| 06 F13 | Atualização autenticada | **d** | não implementado |

## Pendências / lacunas registradas
- **F7.2 (construção ISO/USB/debloat)** — não implementada: exige DISM de serviço (ADK), boot BIOS/UEFI (oscdimg/IMAPI2FS) e reaproveita código GPL do original (Rufus/UEFI:NTFS). Registrado no plano; entra só com decisão explícita.
- **HumanMode do assistente** — comportamento de servidor; o Sevenzin só envia/recebe via endpoint do usuário.
- **Validação manual**: efeito real dos tweaks de segurança (admin + reinício), winget real, captura nos 4 modos, análise de ISO com uma ISO real, fluxo Supabase/MFA num projeto real, render visual de todas as telas novas (Assistente, Perfil, Proteção, Guias, Notificações).
- Categorias **d** (rede, DPC/MSI, serviços, MMCSS Games, IFEO) permanecem ausentes por risco/retorno — decisão registrada, não esquecimento.
- Nenhuma funcionalidade foi declarada "paridade completa" só por existir tela/botão: a coluna Classe reflete o que executa e é verificável.
