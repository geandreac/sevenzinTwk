# Evidência P0 — validação em máquina real (aplicar → reler → reverter → reler)

Gerado em 2026-11-17 por `scripts/validacao_p0.ps1` (máquina: GEANDRE, usuário: geandreac, como administrador).
Grupos SEGUROS aplicados/revertidos com ciclo completo; G4 (segurança) e G1-plano em SÓ LEITURA
(exigem ambiente isolado, conforme regra de segurança do dono).

## Resultados

```
E-001 | CONFIRMADO | G7 | HKCU\Control Panel\Mouse\MouseSpeed | antes: String:0 | depois: String:0 | restaurado: String:0
E-002 | CONFIRMADO | G7 | HKCU\Control Panel\Mouse\MouseThreshold1 | antes: String:0 | depois: String:0 | restaurado: String:0
E-003 | CONFIRMADO | G7 | HKCU\Control Panel\Mouse\MouseThreshold2 | antes: String:0 | depois: String:0 | restaurado: String:0
E-004 | CONFIRMADO | G7 | HKCU\Control Panel\Mouse\MouseHoverTime | antes: String:0 | depois: String:0 | restaurado: String:0
E-005 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-006 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-007 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\DWM\EnableAeroPeek | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-008 | CONFIRMADO | G7 | HKCU\Control Panel\Desktop\DragFullWindows | antes: String:0 | depois: String:0 | restaurado: String:0
E-009 | CONFIRMADO | G7 | HKCU\Control Panel\Desktop\MenuShowDelay | antes: String:0 | depois: String:0 | restaurado: String:0
E-010 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications\ToastEnabled | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-011 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\CurrentVersion\Search\BingSearchEnabled | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-012 | CONFIRMADO | G7 | HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot | antes: DWord:1 | depois: DWord:1 | restaurado: DWord:1
E-013 | CONFIRMADO | G7 | HKCU\Software\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\DontSendAdditionalData | antes: AUSENTE | depois: DWord:1 | restaurado: AUSENTE
E-014 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\SystemResponsiveness | antes: DWord:10 | depois: DWord:10 | restaurado: DWord:10
E-015 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\RMPriorityThrottleDelay | antes: AUSENTE | depois: DWord:0 | restaurado: AUSENTE
E-016 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\NetworkThrottlingIndex | antes: DWord:10 | depois: DWord:10 | restaurado: DWord:10
E-017 | CONFIRMADO | G6 | HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl\Win32PrioritySeparation | antes: DWord:26 | depois: DWord:38 | restaurado: DWord:26
E-018 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games\GPU Priority | antes: DWord:8 | depois: DWord:8 | restaurado: DWord:8
E-019 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games\Priority | antes: DWord:8 | depois: DWord:2 | restaurado: DWord:8
E-020 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games\SFIO Priority | antes: String:High | depois: String:Normal | restaurado: String:High
E-021 | CONFIRMADO | G6 | HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games\Scheduling Category | antes: String:High | depois: String:Medium | restaurado: String:High
E-022 | CONFIRMADO | G6 | HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\DisablePagingExecutive | antes: DWord:0 | depois: DWord:1 | restaurado: DWord:0
E-023 | CONFIRMADO | G6 | HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\LargeSystemCache | antes: DWord:0 | depois: DWord:1 | restaurado: DWord:0
E-024 | CONFIRMADO | G6 | HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power\HiberbootEnabled | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-025 | CONFIRMADO | G5 | HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\TdrDelay | antes: DWord:8 | depois: DWord:8 | restaurado: DWord:8
E-026 | CONFIRMADO | G5 | HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode | antes: DWord:2 | depois: DWord:2 | restaurado: DWord:2
E-027 | CONFIRMADO | G3 | HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{E35C4319-5664-4C23-B9E2-79088FC76B71}\TcpAckFrequency | antes: DWord:1 | depois: DWord:1 | restaurado: DWord:1
E-028 | CONFIRMADO | G3 | HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{E35C4319-5664-4C23-B9E2-79088FC76B71}\TCPNoDelay | antes: DWord:1 | depois: DWord:1 | restaurado: DWord:1
E-029 | CONFIRMADO | G3 | HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{E35C4319-5664-4C23-B9E2-79088FC76B71}\TcpDelAckTicks | antes: DWord:0 | depois: DWord:0 | restaurado: DWord:0
E-030 | CONFIRMADO | G3 | HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_{E35C4319-5664-4C23-B9E2-79088FC76B71}\NetbiosOptions | antes: DWord:2 | depois: DWord:2 | restaurado: DWord:2
E-031 | CONFIRMADO | G3 | HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\InterfaceSpecificParameters\{E35C4319-5664-4C23-B9E2-79088FC76B71}\EnableMulticast | antes: AUSENTE | depois: DWord:0 | restaurado: AUSENTE
E-032 | DIVERGENTE | G2 | netsh int tcp (autotuning/congestion) | antes: autotuning=normal,congestion=default | depois: autotuning=normal,congestion=default | restaurado: autotuning=normal,congestion=default
E-033 | CONFIRMADO | G1 | hibernacao (powercfg off/on + restauracao) | antes: original=0 | depois: off=0 ; on=1 | restaurado: restaurado=0
E-034 | LACUNA | G4 | SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\EnableLUA | (nao aplicado) leitura=0x0
E-035 | LACUNA | G4 | SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\ConsentPromptBehaviorAdmin | (nao aplicado) leitura=0x0
E-036 | LACUNA | G4 | SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\PromptOnSecureDesktop | (nao aplicado) leitura=0x0
E-037 | LACUNA | G4 | SOFTWARE\Microsoft\Windows Defender\Real-Time Protection\DisableRealtimeMonitoring | (nao aplicado) leitura=0x1
E-038 | LACUNA | G4 | SOFTWARE\Microsoft\Windows Defender\DisableAntiSpyware | (nao aplicado) leitura=AUSENTE
E-039 | LACUNA | G4 | SOFTWARE\Policies\Microsoft\Windows\System\EnableSmartScreen | (nao aplicado) leitura=AUSENTE
E-040 | LACUNA | G4 | SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled | (nao aplicado) leitura=0x0
E-041 | LACUNA | G4 | SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\FeatureSettingsOverride | (nao aplicado) leitura=0x2000003
E-042 | LACUNA | G4 | SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoUpdate | (nao aplicado) leitura=0x1
E-043 | LACUNA | G1 | plano de energia (getactivescheme) | (nao aplicado) leitura=GUID 00000000-0000-0000-0000-000000000000 (@honestzin)
```

## Resumo

- **Grupos com ciclo completo (aplicar → reler → reverter → reler):** G7 (HKCU, 13 itens), G6 (HKLM, 11),
  G5 (HKLM, 2), G3 (HKLM por adaptador, 5), G2 (netsh), G1-hibernação. **Todos CONFIRMADO** exceto G2.
- **Grupos só leitura:** G4 (segurança, 9 itens) e G1-plano — **não aplicados** (ambiente isolado exigido).
- **Nenhum valor permaneceu aplicado** — cada item foi restaurado e a restauração confirmada (a coluna
  "restaurado" iguala o "antes" em todos os CONFIRMADO).

## Descobertas (validação real, 17/11/2026)

1. **[DIVERGÊNCIA CORRIGIDA] G2 — congestionprovider=ctcp.** O comando literal do original
   (`netsh int tcp set global congestionprovider=ctcp`) **falha** no Windows 11 moderno
   (`Parâmetro incorreto`, exit=1): o parâmetro global `congestionprovider` foi **removido** e movido para
   `netsh int tcp set supplemental internet congestionprovider=ctcp`. `autotuninglevel=normal` (global) ainda
   funciona ("Ok.", exit=0). **Correção aplicada**: `RedeGlobal` agora usa `set supplemental` (preserva a
   intenção do original — CTCP) e lê `show supplemental` para a releitura. Comando antigo mantido como
   divergência documentada em `validacao_paridade.md` §8.

2. **[EVIDÊNCIA] A máquina NÃO está em estado de fábrica.** Diversas chaves já estavam com valores "otimizados"
   (resíduo de execução anterior do original ou de outra ferramenta): `EnableLUA=0`, `HwSchMode=2`,
   `TdrDelay=8`, `SystemResponsiveness=10`, `TcpAckFrequency=1`, `TCPNoDelay=1`, `TcpDelAckTicks=0`,
   `NetbiosOptions=2`, plano de energia ativo `@honestzin`. Por isso vários "antes" já equivalem ao alvo.

3. **[GROUND TRUTH] Win32PrioritySeparation.** A máquina tem `0x1A` (26), confirmando que o original usa
   **valor dinâmico por CPU** e que o 38 fixo do Sevenzin é uma aproximação (divergência já registrada).

4. **[CONFIRMAÇÃO] Catálogo MMCSS Games correto.** A máquina tem `Priority=8 / SFIO=High / Scheduling=High`
   (estado pré-existente, não o do original), mas o catálogo do Sevenzin (`Priority=2 / Normal / Medium`) está
   **correto**, confirmado diretamente em `01_registro_setvalues.txt` linhas 224/254/256. O ciclo P0 aplicou
   2/Normal/Medium e restaurou 8/High/High corretamente.

5. **[LACUNA] G4 (segurança) e G1-plano** ficaram em SÓ LEITURA (exigem ambiente isolado + reinício, conforme
   regra de segurança). Valores atuais lidos: `EnableLUA=0`, `HVCI=0`, `FeatureSettingsOverride=0x2000003`,
   `NoAutoUpdate=1`, `DisableRealtimeMonitoring=1` — coerentes com uma execução anterior do original.
