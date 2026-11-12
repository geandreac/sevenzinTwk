# Catálogo de otimizações do binário original (para o Sevenzin)

Extraído do código descompilado do `geandre.exe` (Honest / "Fps.Assistant"). O `honest (2)/geandre.exe`
é **byte‑idêntico** ao já analisado (mesmo SHA256 `70dc5163…`), então não há versão nova — isto consolida
tudo o que o original faz.

Objetivo: registrar **cada métrica/ajuste de otimização** para alimentar a Fase 4 do Sevenzin
(§21.4–21.7 "recomendações reversíveis") e marcar o que a carta segura **proíbe** (§23 Fase 0).

- Dados brutos completos (nada omitido): [`otimizacoes-originais/`](otimizacoes-originais/)
  - `01_registro_setvalues.txt` — 338 escritas de registro (nome · valor · tipo)
  - `02_registro_chaves.txt` — 110 caminhos de chave usados
  - `03_comandos_sistema.txt` — 299 comandos (`powercfg`, `netsh`, `Set-MpPreference`, `Disable-NetAdapter*`, `Remove-AppxPackage`…)

## Como o Sevenzin deve usar isto

Cada ajuste recebe uma **classe**:

- 🟢 **Reversível** — candidato à Fase 4: prévia, cópia do estado anterior, desfazer, auditoria, A/B antes de recomendar.
- 🟡 **Hardware/Inferência** — pode piorar; só com A/B no PC concreto e aviso de risco. Nunca preset universal.
- 🔴 **Proibido** — viola §23 (segurança). O Sevenzin **não aplica**; no máximo **detecta** se já está ligado e **oferece restaurar** o estado protegido.

Nenhum ajuste entra sem comparação A/B (§21.3) e sem reversão testada (§24).

---

## 1. CPU — prioridade e escalonamento 🟢/🟡

| Ajuste | Onde | Valor no original | Classe |
|---|---|---|---|
| Win32PrioritySeparation | `SYSTEM\CCS\Control\PriorityControl` | preset (ex. 0x26) | 🟡 A/B — afeta foreground/background |
| Perfil MMCSS "Games" | `…\Multimedia\SystemProfile\Tasks\Games` | GPU Priority=8, Priority=2, Scheduling Category=Medium, SFIO Priority=Normal | 🟢 padrão do Windows, reversível |
| SystemResponsiveness | `…\Multimedia\SystemProfile` | 10 | 🟢 reversível (padrão 20) |
| Afinidade + prioridade por jogo | per‑título, aplicado quando o jogo abre | CpuPriorityClass 2/4, Affinity, IO priority | 🟡 §21.4 — só com topologia confirmada; restringir threads pode causar stutter |
| IFEO PerfOptions por processo | `…\Image File Execution Options\<exe>\PerfOptions` | ex. Discord.exe, lsass.exe | 🟡 por app; cuidado com processos do sistema |

## 2. GPU — driver, energia e agendamento 🟡

| Ajuste | Onde | Valor | Classe |
|---|---|---|---|
| HAGS (agendamento por hardware) | `…\GraphicsDrivers` HwSchMode | 2 | 🟡 depende de GPU/driver, A/B |
| TdrDelay | `…\GraphicsDrivers` | 8 | 🟡 mascara travas de driver |
| Preferência de GPU alto desempenho | `…\DirectX\UserGpuPreferences` | — | 🟢 reversível |
| Power gating / pstate off (AMD) | `…\amdkmdap`, FpsAmd | DisableDynamicPstate=1, Disable*PowerGating=1, AsicOnLowPower=0 | 🟡 risco de calor/instabilidade |
| AMD DVR / Chill | `Software\AMD\DVR`, ChillEnabled | gravação off | 🟢 reversível |
| Overclock de clock/memória do driver | FpsNvidia / FpsAmd | — | 🔴/🟡 overclock não é "otimização segura"; fora do escopo |
| Limpeza de driver (DDU) | — | remove driver de vídeo | 🟡 ação destrutiva, exige confirmação e ponto de restauração (§21.5) |

## 3. Plano de energia 🟢/🟡

- Cria esquema próprio (GUID `00000000-…`) e aplica **~100 `powercfg /setacvalueindex`** (lista completa em `03_comandos_sistema.txt`): desliga economia de energia de PCIe (ASPM), USB selective suspend, processor idle/parking, etc.
- `powercfg /hibernate off`, `HiberbootEnabled=0` (fast startup off) — 🟢 reversível, geralmente recomendado.
- `StorPortPower`, `USBPowerTweak`, `PowerTweak` — economia de energia de disco/USB off — 🟡 A/B (consumo ↑).
- **Sevenzin**: em vez de 100 índices sem proveniência, selecionar "Alto desempenho" e, se A/B justificar, ajustar poucos valores com prévia e reversão.

## 4. Rede / latência 🟡

Script em lote completo em `03_comandos_sistema.txt`. Principais:

| Ajuste | Valor | Classe |
|---|---|---|
| NetworkThrottlingIndex | 10 | 🟡 |
| TcpAckFrequency / TCPNoDelay / TcpDelAckTicks | 1 / 1 / 0 (por interface) | 🟡 pode piorar fora de jogo |
| autotuning=normal, congestionprovider=ctcp | — | 🟡 |
| MaxUserPort=65534, TcpTimedWaitDelay=30 | — | 🟡 |
| DNS multicast / NetBIOS / LMHOSTS off | EnableMulticast=0, NetbiosOptions=2 | 🟢 privacidade, baixo risco |
| Offloads de NIC (`Disable-NetAdapter*`), EEE/FlowControl/GreenEthernet off | — | 🟡 depende da NIC |

## 5. Memória / armazenamento 🟡 (cuidado: risco de dados)

| Ajuste | Onde | Valor | Classe |
|---|---|---|---|
| DisablePagingExecutive / LargeSystemCache | `…\Memory Management` (MemoryManagementTweak) | 1 | 🟡 debatível, A/B |
| `Disable-MMAgent -MemoryCompression -ApplicationPreLaunch` | — | — | 🟡 ruim em pouca RAM |
| Write cache forçado | disk `CacheIsPowerProtected=1` | 1 | 🔴 **risco de perda de dados** sem no‑break; só com consentimento explícito |

## 6. DPC / interrupções / MSI 🟡

- `SOFTWARE\DpcOptimization`, DpcWatchdogPeriod/DpcTimeout/DpcWatchdogProfileOffset=0 — 🟡 **mascara instabilidade**.
- `SOFTWARE\MSIIRQ`: MSI mode + IRQ affinity por dispositivo (`MessageSignaledInterruptProperties`, `Affinity Policy`, DevicePriority) — 🟡 pode causar travas; A/B obrigatório.

## 7. Serviços e tarefas 🟢

- Desativa serviços não essenciais em segundo plano (restaurável — "Serviços restaurados ao padrão").
- `Disable-ScheduledTask` / `schtasks` de tarefas de telemetria/manutenção.
- Serviço de localização (`lfsvc`) — avisa que alguns jogos não abrem sem ele. 🟢 reversível, com aviso.

## 8. Telemetria / privacidade 🟢

AllowTelemetry=0, DataCollection, ID de publicidade off, histórico de atividades off, Copilot/WindowsCopilot off,
Windows Error Reporting off, PushNotifications off, Bing/destaques de pesquisa off, experiências personalizadas off.
→ 🟢 reversível, baixo risco — bom alinhamento com a postura do Sevenzin.

## 9. Visual / UI 🟢 (cosmético, totalmente reversível)

VisualEffectsTweak: animações/transições off, transparência off, AeroPeek, `DragFullWindows`, MenuShowDelay,
tema escuro (AppsUseLightTheme=0), GameDVR_Enabled=0 / AllowGameDVR=0 / Game Bar off, precisão aprimorada do ponteiro off.

## 10. Debloat / Appx 🟡

`Remove-AppxPackage` (Copilot e outros), desinstala OneDrive (`OneDriveUninstall.ps1`) e Edge
(`EdgeBrowserUninstall.ps1`), `winget install 9NF8H0H7WMLT` (loja). → 🟡 reversível por reinstalação; alto impacto.

## 11. Construtor de mídia do Windows (fora do escopo do Sevenzin)

`OfflineHive` + `MediaTransfer` + `IsoImage` + scripts `BuildMedia.ps1`/`PostInstall.ps1`/`AnalyzeImage.ps1`:
monta uma ISO do Windows "debloated" editando hives de registro offline. É um recurso grande e de risco alto
(instalação de SO); **não** faz parte da reconstrução segura.

---

## 🔴 Proibido pela carta segura (§23 Fase 0) — Sevenzin nunca aplica

Estes existem no original e o Sevenzin deve, no máximo, **detectar e oferecer restaurar o estado protegido**:

| Ajuste | Onde (no original) | Por quê é proibido |
|---|---|---|
| Desligar Microsoft Defender (agressivo) | `WinDefenderTweak`, `Real-Time Protection`, `Set-MpPreference`, Spynet SubmitSamplesConsent=0, DefenderApiLogger/AuditLogger Start=0 | remove proteção antivírus |
| Desativar UAC | `Policies\System` ConsentPromptBehaviorAdmin/User=0 | remove consentimento de elevação |
| Desligar HVCI / Isolamento de Núcleo / VBS | `Control\DeviceGuard`, `…\HypervisorEnforcedCodeIntegrity`, EnableVirtualizationBasedSecurity=0 | remove proteção de integridade de código |
| Desligar mitigações Spectre/Meltdown | `Memory Management` FeatureSettingsOverride=33554435 (0x2000003) | reexpõe CVEs de execução especulativa |
| Desligar Exploit Protection de processos críticos | `MitigationOptions` / IFEO em lsass, dwm, svchost, winlogon, csrss | remove mitigações de exploits do núcleo do SO |
| SmartScreen off | `Policies\…\SmartScreen` ConfigureAppInstallControl=Anywhere | remove filtro de reputação |
| Pausar Windows Update | `WindowsUpdate\UX\Settings` | deixa o SO sem correções de segurança |

Regra do Sevenzin (§23/§24): **medir antes de otimizar, padrões seguros, uma mudança por vez, reversão testada,
sem promessa de FPS fixo** — e segurança nunca é desligada em nome de desempenho.
