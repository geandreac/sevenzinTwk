# Roteiro de validação em máquina controlada (P0) — pronto para executar

Objetivo: transformar a paridade "em código+testes" em **paridade medida**, aplicando, relendo o estado real
e registrando o diff antes/depois. Protocolo §2.4/§11.3 do plano.

> ⚠ Rode em **máquina/VM controlada**, como administrador, com ponto de restauração. **Nunca** nesta máquina
> de desenvolvimento sem confirmação explícita. Cada grupo segue o ciclo **ler → aplicar → reler → desfazer → reler**.

---

## Meta: paridade 100,0% (definição operacional)

"100,0%" é definido como **fechamento contábil do catálogo do geandre.exe**: **zero itens pendentes**. Todo
item do catálogo termina em exatamente um destes três estados terminais:

| estado terminal | o que significa | como é fechado |
|---|---|---|
| **① Reproduzido + validado** | valor/efeito igual ao original, **releitura real confirmada** | ciclo P0 (G1–G10 abaixo) |
| **② Excluído por decisão do dono** | o Sevenzin **deliberadamente** não faz (segurança/estabilidade) | assinatura do dono no ledger |
| **③ Não aplicável** | **não existe** no original (FPS/temp, flash de BIOS, presets por jogo) ou é **irrecuperável** (listas no `#US`, valores binários ambíguos) — não há o que igualar | classificação no ledger |

**Fórmula:** `paridade = (① + ② + ③) / total do catálogo`. Quando ①+②+③ cobrem **todos** os itens (nenhum
"pendente"), o resultado é **100,0%**.

> Nota honesta (1 linha): **paridade literal byte-a-byte não é o alvo nem é alcançável** — parte do original
> está cifrada no `#US` (listas de serviços/tarefas, URLs/hashes) e outra parte é insegura (KMS, prioridade
> Realtime). 100,0% **contábil** é alcançável e é o que este roteiro entrega.

### Ledger de fechamento (o "mapa até 100,0%")

| bucket | itens hoje | contagem | fecha em |
|---|---|---|---|
| ① Reproduzido + validar | `Idêntico` 37 + `Parcial` 2 + `Divergente`-implementado 2 (Win32PrioritySeparation=38, AllowTelemetry=1) | **41** | G1–G10 |
| ② Excluir por decisão | KMS, DPC, MSI/IRQ, overclock/fan/voltagem (matriz) + ISO/USB (removido do escopo) | **5** | assinatura §decisões |
| ③ Não aplicável | IFEO PerfOptions, VisualFX/FeatureSettingsOverrideMask/MitigationOptions, extensões de arquivo, demais serviços/tarefas (`#US`), FPS/temp, otimizações por jogo, flash BIOS | **7** | classificação §decisões |
| **pendente** | nada a mais — fechado | **0** | — |

---

## Como executar

1. **Ponto de restauração** antes de começar (`Checkpoint-Computer -Description "Sevenzin-P0"`).
2. Para cada grupo, copie o bloco de comandos, rode o `ANTES`, use a **UI do Sevenzin** para aplicar,
   rode o `DEPOIS`, confirme o valor esperado, então **Desfazer** e rode o `RESTAURADO`.
3. Registre cada linha no formato de evidência:

```
E-### | [CONFIRMADO/DIVERGENTE/LACUNA] | grupo | chave/comando | antes: <valor> | depois: <valor> | desfeito: <valor>
```

---

## G1 — Plano de energia + hibernação

```powershell
# ANTES
powercfg /getactivescheme
# DEPOIS (após "Criar e ativar Sevenzin" na tela Windows)
powercfg /getactivescheme                 # esperado: (...) (Sevenzin)
$g = (powercfg /getactivescheme).Trim().Split(' ')[-2]   # GUID do plano Sevenzin
powercfg /q $g | Select-String -Pattern 'PROCTHROTTLEMIN|VIDEOIDLE|DISKIDLE' -Context 0,2   # amostra dos 134 índices
# RESTAURADO (após "Restaurar")
powercfg /getactivescheme                 # esperado: (Equilibrado)
```
Hibernação:
```powershell
# ANTES
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Power" /v HibernateEnabled
# DEPOIS ("Desativar hibernação" → powercfg /hibernate off)
powercfg /a                              # esperado: "A hibernação não foi habilitada"
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Power" /v HibernateEnabled   # esperado: 0 ou ausente
# RESTAURADO ("Reativar hibernação")
powercfg /a                              # esperado: hibernação disponível
```

## G2 — Rede global (TCP)

```powershell
# ANTES
netsh int tcp show global
# DEPOIS (após "Aplicar (normal + CTCP)" em Ajustes → REDE GLOBAL)
netsh int tcp show global   # esperado: Auto-Tuning = normal · Congestion Provider = ctcp
# RESTAURADO (após "Restaurar padrão")
netsh int tcp show global   # esperado: Congestion Provider = default
```

## G3 — Rede por adaptador (5 valores por interface)

```powershell
# ANTES — descobrir o GUID do adaptador ativo
(Get-NetAdapter | Where-Object Status -eq 'Up').InterfaceGuid
$g = (Get-NetAdapter | Where-Object Status -eq 'Up').InterfaceGuid
reg query "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" /v TcpAckFrequency
reg query "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" /v TCPNoDelay
reg query "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" /v TcpDelAckTicks
reg query "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_$g" /v NetbiosOptions
reg query "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\InterfaceSpecificParameters\$g" /v EnableMulticast
# DEPOIS (aplicar o cartão "REDE (POR ADAPTADOR)" em Ajustes)
#   esperado: TcpAckFrequency=1 · TCPNoDelay=1 · TcpDelAckTicks=0 · NetbiosOptions=2 · EnableMulticast=0
# RESTAURADO (Desfazer) → valores voltam ao anterior (ou chaves removidas)
```

## G4 — UAC completo (9) + Defender + SmartScreen + HVCI + mitigações + WU

> ⚠ **Requer reinício** para valer. Marcar evidência como "aplicado (reinício pendente)".

```powershell
$p = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
# ANTES
reg query $p /v EnableLUA
reg query $p /v ConsentPromptBehaviorAdmin
reg query $p /v ConsentPromptBehaviorUser
reg query $p /v DefaultConsent
reg query $p /v FilterAdministratorToken
reg query $p /v PromptOnSecureDesktop
reg query $p /v ValidateAdminCodeSignatures
reg query $p /v EnableUIADesktopToggle
reg query $p /v EnableSecureUIAPaths
reg query "HKLM\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection" /v DisableRealtimeMonitoring
reg query "HKLM\SOFTWARE\Microsoft\Windows Defender" /v DisableAntiSpyware
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableSmartScreen
reg query "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" /v Enabled
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" /v FeatureSettingsOverride
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" /v NoAutoUpdate
# DEPOIS (aplicar o grupo Segurança, um a um, com confirmação)
#   esperado: EnableLUA=0 · ConsentPromptBehaviorAdmin=0 · ConsentPromptBehaviorUser=0 · DefaultConsent=1
#             FilterAdministratorToken=0 · PromptOnSecureDesktop=0 · ValidateAdminCodeSignatures=0
#             EnableUIADesktopToggle=0 · EnableSecureUIAPaths=0 · DisableRealtimeMonitoring=1
#             DisableAntiSpyware=1 · EnableSmartScreen=0 · HVCI Enabled=0
#             FeatureSettingsOverride=33554435 · NoAutoUpdate=1
# Defender: anotar se o valor é respeitado com Tamper Protection ativa (se não, marcar DIVERGENTE).
# RESTAURADO (Desfazer de cada item) → valores voltam ao anterior (ou chaves removidas)
```

## G5 — GPU (TdrDelay, HAGS, AMD power gating) + modo de vídeo

```powershell
$g = "HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"
# ANTES
reg query $g /v TdrDelay
reg query $g /v HwSchMode
# DEPOIS: TdrDelay=8 · HwSchMode=2   (AMD: ver G5b)
# RESTAURADO: volta ao anterior
```
G5b — AMD (só em máquina AMD; 10 valores no driver `0000`):
```powershell
$d = "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000"
reg query $d /v DisableDynamicPstate
reg query $d /v DisableGMMPowerGating
reg query $d /v DisableSAMUPowerGating
reg query $d /v DisableVCNPowerGating
reg query $d /v DisableDrmdmaPowerGating
reg query $d /v EnableUlps
reg query $d /v AsicOnLowPower
reg query $d /v PP_GPUPowerDownEnabled
reg query $d /v PP_SclkDeepSleepDisable
reg query $d /v PP_DisablePowerContainment
reg query "HKCU\Software\AMD\DVR" /v ChillEnabled
#   esperado: Disable*PowerGating=1 · EnableUlps=0 · AsicOnLowPower=0 · PP_GPUPowerDownEnabled=0
#             PP_SclkDeepSleepDisable=1 · PP_DisablePowerContainment=1 · ChillEnabled=0
```
Modo de vídeo: conferir resolução×Hz exibidos (`LeitorModosVideo`) contra `Get-CimInstance Win32_VideoController`.

## G6 — CPU / memória / MMCSS

```powershell
$m = "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
$mm = "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
# ANTES
reg query $m /v SystemResponsiveness
reg query $m /v RMPriorityThrottleDelay
reg query $m /v NetworkThrottlingIndex
reg query "HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl" /v Win32PrioritySeparation
reg query "$m\Tasks\Games" /v "GPU Priority"
reg query "$m\Tasks\Games" /v "Priority"
reg query "$m\Tasks\Games" /v "SFIO Priority"
reg query "$m\Tasks\Games" /v "Scheduling Category"
reg query $mm /v DisablePagingExecutive
reg query $mm /v LargeSystemCache
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power" /v HiberbootEnabled
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel" /v GlobalTimerResolutionRequests
# DEPOIS: SystemResponsiveness=10 · RMPriorityThrottleDelay=0 · NetworkThrottlingIndex=10
#         Win32PrioritySeparation=38 · GPU Priority=8 · Priority=2 · SFIO Priority="Normal"
#         Scheduling Category="Medium" · DisablePagingExecutive=1 · LargeSystemCache=1
#         HiberbootEnabled=0 · GlobalTimerResolutionRequests=1
```

## G7 — Mouse / visual / privacidade

```powershell
# ANTES (HKCU — sem admin)
reg query "HKCU\Control Panel\Mouse" /v MouseSpeed
reg query "HKCU\Control Panel\Mouse" /v MouseThreshold1
reg query "HKCU\Control Panel\Mouse" /v MouseThreshold2
reg query "HKCU\Control Panel\Mouse" /v MouseHoverTime
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" /v EnableTransparency
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" /v AppsUseLightTheme
reg query "HKCU\Software\Microsoft\Windows\DWM" /v EnableAeroPeek
reg query "HKCU\Control Panel\Desktop" /v DragFullWindows
reg query "HKCU\Control Panel\Desktop" /v MenuShowDelay
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications" /v ToastEnabled
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Search" /v BingSearchEnabled
reg query "HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot" /v TurnOffWindowsCopilot
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\DeviceAccess\Global" /v DontSendAdditionalData
reg query "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting" /v Disabled
reg query "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v EnableLMHOSTS
# DEPOIS: MouseSpeed="0" (REG_SZ) · MouseThreshold1="0" · MouseThreshold2="0" · MouseHoverTime="0"
#         EnableTransparency=0 · AppsUseLightTheme=0 · EnableAeroPeek=0 · DragFullWindows="0" · MenuShowDelay="0"
#         ToastEnabled=0 · BingSearchEnabled=0 · TurnOffWindowsCopilot=1 · DontSendAdditionalData=1
#         WER Disabled=1 · EnableLMHOSTS=0
```

## G8 — Jogos (perfil aplicado ao processo correto)

1. Abrir um jogo (ex.: CS2) e "Aplicar" o perfil.
2. **DEPOIS** — validar no processo real: `Get-Process -Id <pid>` e `[System.Diagnostics.Process]::GetProcessById(<pid>).ProcessorAffinity` (afinidade) e `PriorityClass` (prioridade).
3. **CPU Sets (P-cores)** em CPU híbrida: validar via `CpuSets`/utilitário de afinidade.
4. Fechar o jogo → confirmar **reversão** (perfil temporário, não persistente).
5. Detecção Steam/Epic: conferir a lista detectada contra a biblioteca real (`DescobridorJogos`).

## G9 — Instaladores

1. Baixar 1 item (Sysinternals): validar **assinatura Microsoft** válida (`Get-AuthenticodeSignature`).
2. Forçar hash divergente → confirmar que o arquivo é **descartado** (não instala).
3. `winget` de 1 ID da allowlist (`WingetAllowlist`) → instala e reporta.

## G10 — Capturas

1. Exercitar os 4 modos (tela / monitor / região / janela) + editor + salvar/copiar.
2. Medir o tempo de desenho do lápis num cenário reproduzível (imagem 4K) — comparar com o registrado em
   `revisao_ajustes.md` F6 (preview 1600px + throttle 30fps).

## G11 — Admin único (Geandre)

1. Sem `SEVENZIN_ADMIN_EMAIL` → bootstrap **nega** com instrução.
2. Com `SEVENZIN_ADMIN_EMAIL` = e-mail do Geandre → cria o admin.
3. Outro e-mail → **nega** (gate no domínio).
4. Convite: gerar → cadastrar com o código → conta `Usuario`; reutilizar o código → **recusa**;
   conferir `convites.json` só com **hash** (nunca em claro).
5. `GarantirAdminUnico`: injetar um admin legado → **rebaixado**.

## G12 — 2FA / Supabase

1. Ativar 2FA no Perfil → login passa a pedir o código (autenticador real).
2. Código errado → falha; código correto → entra.
3. Supabase (se configurado): login + papel via RLS.

---

## Critério de conclusão (Definition of Done para 100,0%)

1. **G1–G10** executados em máquina controlada, cada item com evidência `E-### [CONFIRMADO]` (ou rebaixado a
   `DIVERGENTE` com justificativa e reentrada no ledger).
2. **Ledger assinado pelo dono**: os 5 itens de ② (KMS, ISO/USB, DPC, MSI/IRQ, overclock) com "excluído por
   decisão" explícita, e os 9 de ③ classificados "não aplicável".
3. `validacao_paridade.md` §9 atualizado com as evidências E-###, e o resumo §7 recalculado: **0 pendentes**.

Quando (1), (2) e (3) valerem, a paridade contábil sobre o catálogo do geandre.exe é **100,0%**.
