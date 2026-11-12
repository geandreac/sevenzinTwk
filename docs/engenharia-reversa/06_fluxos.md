# 06 — Fluxos

**Alvo:** `C:\re\geandre_v3\geandre.exe` (SHA-256 `70DC5163…AFCB72`)
**Base:** heap `#Strings` (nomes de membros), recursos `110_Honest.WindowsMedia.BuildMedia.ps1`, `111_Honest.WindowsMedia.MediaOperations.ps1`, `113_Honest.WindowsMedia.PostInstall.ps1`, `114_Honest.WindowsMedia.AnalyzeImage.ps1`, `115/116` (Edge/OneDrive), `MediaTransfer.cs`, `IsoImage.cs`, `OfflineHive.cs`, `Honest-config.json`/`Honest.social-endpoint.json` e strings de UI.

> **Método:** análise estática; nada foi executado. Os textos de etapa entre aspas simples são **literais** recuperados dos scripts embutidos. A ordem dos passos é comprovada por essas etapas e pela ordem das chamadas no script.

Legenda: `[CONFIRMADO]` = lido do arquivo; `[INFERIDO]` = deduzido com evidência; `[LACUNA]` = indeterminado.

---

## F1. Inicialização e "portão" de segurança

```
Processo (requireAdministrator)
   │
   ├─ manifest: requireAdministrator + longPathAware   [CONFIRMADO]
   ├─ mutex/instância única                            [LACUNA]
   ├─ AddFontResource (Inter/Geist)                    [CONFIRMADO]
   ├─ VerifyAssemblyIntegrity                          [CONFIRMADO]
   ├─ anti-debug (IsDebuggerPresent, EnumWindows)      [CONFIRMADO P/Invoke; chamada INFERIDO]
   └─ KeyAuth_Init → HandshakeBegin/Complete            [CONFIRMADO]
            │
            └─► tela de login
```

| passo | origem | confiança | observações |
|---|---|---|---|
| Elevação obrigatória | `manifest.xml` → `requestedExecutionLevel level="requireAdministrator"` | [CONFIRMADO] | Sem elevação o app não inicia |
| DPI | `dpiAware=false`, `dpiAwareness=unaware` | [CONFIRMADO] | Escala tratada em runtime (`SetThreadDpiAwarenessContext`) |
| Registro de fontes | `AddFontResource`, `Honest.Native.Fonts.*`, `Honest.Resources.Fonts.*` | [CONFIRMADO] | 13 TTF |
| Verificação de integridade | `VerifyAssemblyIntegrity` + `System.Reflection.Metadata` | [CONFIRMADO] | Lê os próprios metadados |
| Handshake | `KeyAuth_HandshakeBegin`, `KeyAuth_HandshakeComplete` | [CONFIRMADO] | Sessão cifrada |
| Instância única | — | [LACUNA] | Nenhum mutex nomeado recuperado |
| Falha de integridade/depurador | — | [INFERIDO] | Ausência de mensagem; provável encerramento silencioso |

## F2. Autenticação (três modos) e pós-login

```
[Login/Register/License]
   ├─ Login      (usuário+senha)  → KeyAuth_Login
   ├─ License    (chave)          → KeyAuth_License
   ├─ WebLogin   (device code)    → KeyAuth_WebLogin
   ├─ Register   (cadastro)       → KeyAuth_Register
   ├─ Forgot     (recuperação)    → KeyAuth_Forgot
   └─ após sucesso:
        ├─ KeyAuth_Check (periódico)
        ├─ KeyAuth_GetUserDataJson / ExpiryDaysLeft
        ├─ KeyAuth_SetVar/GetVar (flags remotas)
        ├─ SavedPcProfile (carrega/cria perfil)
        └─ Shell (Home)
```

| passo | origem | confiança | observações |
|---|---|---|---|
| Login usuário/senha | `KeyAuth_Login` | [CONFIRMADO] | — |
| Login por licença | `KeyAuth_License` | [CONFIRMADO] | — |
| Login por navegador | `KeyAuth_WebLogin` | [CONFIRMADO] | Device code |
| Cadastro | `KeyAuth_Register` | [CONFIRMADO] | — |
| Recuperação | `KeyAuth_Forgot` | [CONFIRMADO] | — |
| 2FA | `KeyAuth_Enable2FA`, `KeyAuth_Disable2FA`, `KeyAuth_GetTwoFactorSecret` | [CONFIRMADO] | TOTP; exigência por conta [LACUNA] |
| Sessão | `KeyAuth_Check` | [CONFIRMADO] | Frequência [LACUNA] |
| HWID | `AccountKey`, `fingerprint` ("Identificação") | [CONFIRMADO] (campos) / [LACUNA] (algoritmo) | Enviado ao backend |
| Dados do usuário | `KeyAuth_GetUserDataJson`, `KeyAuth_ExpiryDaysLeft` | [CONFIRMADO] | Alimentam a tela de conta |
| Features remotas | `KeyAuth_Var`, `KeyAuth_SetVar`, `KeyAuth_GetVar` | [CONFIRMADO] | Podem desligar módulos por licença |
| Banimento | `KeyAuth_Ban`, `KeyAuth_CheckBlack` | [CONFIRMADO] | Bloqueio por conta/HWID |
| Falha de login | — | [LACUNA] | Texto em `#US` cifrado |
| Token persistido | `get_Token` | [INFERIDO] | Provável token de sessão em memória/config local |

## F3. Home e perfil do PC

| passo | origem | confiança | observações |
|---|---|---|---|
| Carregar perfil salvo | `SavedPcProfile`, `SelectedId`, `UpdatedUtc` | [CONFIRMADO] | Se vazio, gera novo |
| Coletar hardware | `GlobalMemoryStatusEx`, `EnumDisplayDevices`, `EnumDisplaySettingsW`, `IO.DriveInfo` | [CONFIRMADO] (APIs) / [INFERIDO] (quais campos) | Provedor de CPU/placa-mãe [LACUNA] |
| Identificar GPU | `Honest.Nvidia.PciIds` | [CONFIRMADO] | Offline, por vendor/device |
| Comparar com recomendado | `RecommendedProfile`, `HasCustomSettings`, `HasDetails` | [CONFIRMADO] | Gera "ajustes sugeridos" |
| Cards da Home | `cardEafc/cardFortnite/cardRust/cardWarzone`, `Fps.HomePhotos.*` | [CONFIRMADO] | 4 jogos + 3 fotos de seção |
| Detalhes/edição manual | `HasDetails`, `HasCustomSettings` | [CONFIRMADO] | Fonte/cooler/gabinete são digitados |

`[INFERIDO]` A Home não bloqueia: o perfil é atualizado em background e a UI mostra o último estado salvo. Evidência: `UpdatedUtc` e a ausência de etapa de "coleta obrigatória".

## F4. Aplicar tweaks

```
Seleção de grupos (12)
   → ApplyPreference{ try/catch } por chave
   → grava HKCU/HKLM imediatamente
   → $failed acumula falhas (não aborta)
   → UI reflete estado por grupo
```

| passo | origem | confiança | observações |
|---|---|---|---|
| Grupos | `activityHistory, advertising, diagnostics, extras, gameDvr, notifications, oneDrive, searchHighlights, suggestions, tailored, uac, widgets` | [CONFIRMADO] | Mesmos nomes no app e no `PostInstall.ps1` |
| Escrita | `SetValue` + `New-ItemProperty -Force` (DWord) | [CONFIRMADO] | Idempotente |
| Tratamento de erro | `ApplyPreference` com `try/catch` e `$failed` | [CONFIRMADO] | Uma chave negada não interrompe as outras |
| UAC | `HKLM\...\Policies\System\EnableLUA=0` | [CONFIRMADO] | Requer reinício; risco alto |
| Telemetria | `AllowTelemetry=1` | [CONFIRMADO] | Fixa no mínimo, **não** desliga |
| OneDrive | `DisableFileSyncNGSC=1` + desinstalação no logon | [CONFIRMADO] | Dois momentos distintos |
| Extras (winget) | allowlist de 5 IDs | [CONFIRMADO] | Ver F9 |
| Rollback | — | [LACUNA] | Não há restauração de valores anteriores comprovada |
| Log | `$warnings`, `$failed`, `KeyAuth_Log` | [INFERIDO] | Log remoto existe, mas não se sabe se recebe tweaks |

## F5. Windows Media — análise da imagem

Sequência literal de `114_Honest.WindowsMedia.AnalyzeImage.ps1`:

| # | etapa | confiança | observações |
|---|---|---|---|
| 1 | `Reading the ISO image…` | [CONFIRMADO] | Monta a ISO somente leitura |
| 2 | `Identifying the Windows editions…` | [CONFIRMADO] | `Get-WindowsImage` |
| 3 | `Converting the ESD edition for reading. This step may take a few minutes…` | [CONFIRMADO] | Só quando a fonte é `.esd` |
| 4 | `Preparing a temporary copy of the image. Your ISO will be preserved…` | [CONFIRMADO] | Cópia de trabalho |
| 5 | `Opening the edition in the temporary copy…` | [CONFIRMADO] | — |
| 6 | `Reading provisioned apps and components…` | [CONFIRMADO] | Appx + capabilities |
| 7 | `Reading desktop programs from the image…` | [CONFIRMADO] | Programas desktop detectados no `install.wim` |
| 8 | `Reading installed Windows features…` | [CONFIRMADO] | Features opcionais |
| 9 | `Unmounting the temporary image. The list can now be customized…` | [CONFIRMADO] | UI libera seleção |
| 10 | `Releasing the read image…` | [CONFIRMADO] | — |
| 11 | `Ejecting the read ISO…` | [CONFIRMADO] | ISO original preservada |

Regras aplicadas na seleção: `MediaRules.ps1` → `ProtectedApp`, `ProtectedCapability`, `AppDependencies` (ver `04_funcionalidades.md`, seção 4).

`[INFERIDO]` A lista marcada por padrão segue o "debloat leve": apps de terceiros e jogos pré-instalados saem, tudo que é framework/Store/WebView2/idioma fica. Evidência: as regras de proteção são amplas e bidirecionais.

## F6. Windows Media — construir ISO

| # | etapa (literal) | origem | confiança | observações |
|---|---|---|---|---|
| 0 | Validação de destino | `$request.mode -eq 'iso'` | [CONFIRMADO] | Extensão `.iso`, não sobrescreve origem nem arquivo existente |
| 0b | Arquivo parcial | `$destination + '.' + guid + '.partial'` | [CONFIRMADO] | Escrita atômica; só renomeia no fim |
| 1 | `Opening the original ISO for reading…` | `BuildMedia.ps1` | [CONFIRMADO] | `Mount-DiskImage -Access ReadOnly -NoDriveLetter` |
| 2 | `Copying the installation files…` | [CONFIRMADO] | `CopyMedia` |
| 3 | `Exporting only the selected edition…` | [CONFIRMADO] | `Export-WindowsImage` por `index` |
| 4 | `Preparing the custom image…` | [CONFIRMADO] | Montagem do WIM |
| 5 | `Removing <app> from the image…` / `Removing the feature <nome>…` | [CONFIRMADO] | Uma etapa por item |
| 6 | `Adding the signed INF drivers to the edition…` | [CONFIRMADO] | `Add-WindowsDriver -Recurse` |
| 7 | `Saving the changes to the image…` | [CONFIRMADO] | `Dismount-WindowsImage -Save` |
| 8 | `Compacting the custom edition…` | [CONFIRMADO] | `Export-WindowsImage -Compress max` |
| 9 | `Applying the installation preferences…` | [CONFIRMADO] | `OfflineHive`/`offreg.dll` no registro offline |
| 10 | `Saving the changes to the image…` | [CONFIRMADO] | Segunda gravação, após tweaks |
| 11 | `Generating the installation configuration…` | [CONFIRMADO] | `Honest-config.json` + `Configure.ps1` em `sources\$OEM$\$1\HonestSetup` |
| 12 | `Splitting the image for FAT32 media…` | [CONFIRMADO] | Só quando necessário (3.800 MB) |
| 13 | `Creating the ISO with BIOS and UEFI boot…` | [CONFIRMADO] | `HonestIsoImage` (IMAPI2FS), El Torito BIOS+EFI |
| 14 | `Custom media created.` | [CONFIRMADO] | `$result.ok=true`, `output=caminho` |
| 15 | `Discarding the temporary image…` | [CONFIRMADO] | `cleanupSafe` decide se apaga a pasta |

Falha em qualquer etapa → `$result.ok=false`, `error` preenchido, `warnings`, `cleanupSafe` indica se a limpeza é segura `[CONFIRMADO]`.

## F7. Windows Media — construir USB

| # | etapa (literal) | origem | confiança | observações |
|---|---|---|---|---|
| 0 | `action='disks'` | `BuildMedia.ps1` | [CONFIRMADO] | Retorna `number, uniqueId, serial, size, name` por disco |
| 0b | Filtro `SafeUsb` | `MediaOperations.ps1` | [CONFIRMADO] | `BusType=USB`, `!IsBoot`, `!IsSystem`, `!IsOffline`, `!IsReadOnly`, `Size ≥ 8 GB`, `UniqueId` não vazio |
| 0c | Proteção de caminho | `SafeUsb` | [CONFIRMADO] | Recusa disco com letra = raiz de `%windir%`, ISO, drivers ou `hostPath` |
| 1 | Confirmação do dispositivo | `CheckUsb($expected)` | [CONFIRMADO] | Reidentifica por `UniqueId` + `Serial` + `Size` (números mudam ao replugar) |
| 2 | `Preparing <fs> on the confirmed device: <nome>` | [CONFIRMADO] | `Clear-Disk`, `New-Partition`, `Format-Volume`, `Set-Partition` (ativo) |
| 3 | `Extracting the ISO files directly to the USB…` | [CONFIRMADO] | Cópia direta da ISO montada |
| 4 | `Preparing the UEFI:NTFS boot partition…` | [CONFIRMADO] | Só no modo NTFS (`ntfs_x64/ia32/aa64.efi`) |
| 5 | `Applying preferences to the offline image…` | [CONFIRMADO] | Tweaks no `install.wim` antes de copiar |
| 6 | `Copying media files… N / M` | [CONFIRMADO] | Progresso por arquivo; blocos de 1 MB + SHA-256 |
| 7 | Resultado | [CONFIRMADO] | `usbFileSystem` (FAT32/NTFS), `usbBoot` (BIOS/UEFI) |

Regras de boot `[CONFIRMADO]`: FAT32 → `install.wim` dividido (`.swm`, 3.800 MB); NTFS → boot via UEFI:NTFS. Aviso embutido: *"Do not claim universal Secure Boot compatibility or BIOS/Legacy support."*

## F8. Primeiro logon na máquina instalada (pós-instalação)

```
Windows recém-instalado
   └─ sources\$OEM$\$1\HonestSetup\Configure.ps1   [CONFIRMADO]
        └─ PostInstall.ps1 -Stage specialize   (antes do OOBE)
        └─ PostInstall.ps1 -Stage logon        (primeiro login)
              ├─ lê options.json (12 grupos)
              ├─ HKCU: usuário atual
              ├─ HKLM: políticas
              ├─ OneDrive: desinstala
              └─ winget: allowlist (espera até 30×3 s)
```

| etapa | origem | confiança | observações |
|---|---|---|---|
| `specialize` | carrega `C:\Users\Default\NTUSER.DAT` em `HKLM\HonestDefUser`, aplica e descarrega | [CONFIRMADO] | Toda conta nova herda |
| `specialize` sem internet | comentário literal no script | [CONFIRMADO] | OneDrive/extras ficam para o logon |
| `logon` | aplica em `HKCU:` | [CONFIRMADO] | Estágio padrão (`-Stage logon`) |
| OneDrive | `Get-HonestOneDrivePlan` → `/uninstall` (`/allusers` se machine) | [CONFIRMADO] | Valida assinatura Microsoft |
| Extras | `winget install --id ... --exact --silent ...` | [CONFIRMADO] | `-1978335189` = já instalado |
| Falha | `$failed=true`, script continua | [CONFIRMADO] | `$ErrorActionPreference='Stop'` por comando |
| Reexecução | idempotente (`-Force`) | [CONFIRMADO] | Pode rodar de novo sem duplicar |

## F9. Instaladores — fila de download/instalação

```
Catálogo (cards com logo)
   → seleção
   → Stage(item)  [fila interna]
        ├─ URL/tamanho/hash ................ [LACUNA]
        ├─ WebClient.DownloadFileAsync → progresso
        ├─ Start-Process -WindowStyle Hidden -ArgumentList ...
        └─ estado: pendente/baixando/instalando/falhou
   → "Tarefas agendadas" (histórico da fila)   [INFERIDO]
```

| item | origem | confiança | observações |
|---|---|---|---|
| Catálogo | 35 logos confirmados (ver `05`, seção 6) | [CONFIRMADO] | Metadados [LACUNA] |
| Fila | `pendingtasks`, `scheduled-tasks`, `Stage()` | [CONFIRMADO] | Não é o Task Scheduler |
| Download | `WebClient.DownloadFileAsync`, `HttpClient` | [CONFIRMADO] | Progresso por evento |
| Execução | `Start-Process -WindowStyle Hidden`, `-ArgumentList`, `ProcessStartInfo` | [CONFIRMADO] | Flags por produto [LACUNA] |
| Verificação de assinatura do instalador | — | [LACUNA] | Só Edge/OneDrive têm checagem comprovada |
| Retomada após reinício | — | [LACUNA] | Sem persistência identificada |
| Concorrência | `Stage()` sequencial | [INFERIDO] | Nome e uso sugerem um item por vez |

## F10. Jogos — adicionar, configurar e jogar

```
Adicionar jogo
  ├─ Steam:  SteamAppId + KnownPaths
  ├─ Epic:   EpicAppName (manifestos)
  └─ Custom: CustomPath + ExecutableName
        → card (image/star/link)
        → configuração (GpuEnabled, CompatEnabled, CpuIoEnabled,
                         CpuPriority, pCores, excludeCore0, IoPriority)
        → Salvar (SelectedId)
Jogar
  ├─ Process.Start
  ├─ OpenProcessToken + AdjustTokenPrivileges (SeDebugPrivilege)   [INFERIDO]
  └─ aplicar: CPU Sets → afinidade → prioridade → boost → working set
```

| passo | origem | confiança | observações |
|---|---|---|---|
| Descoberta Steam | `SteamAppId`, `KnownPaths` | [CONFIRMADO] | Caminhos de biblioteca [LACUNA] |
| Descoberta Epic | `EpicAppName` | [CONFIRMADO] | — |
| Jogo custom | `IsCustomGame`, `CustomPath`, `ExecutableName` | [CONFIRMADO] | — |
| Card | `image`, `star`, `link`, `play` | [CONFIRMADO] | — |
| Ajustes | `CpuPriority`, `pCores`, `excludeCore0`, `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled`, `IoPriority` | [CONFIRMADO] | Nomes de membro |
| Aplicação | `SetProcessDefaultCpuSets`, `SetProcessAffinityMask`, `SetPriorityClass`, `SetProcessPriorityBoost`, `SetProcessWorkingSetSize` | [CONFIRMADO] | Requer handle de processo |
| Permissão | `OpenProcessToken`, `AdjustTokenPrivileges` | [INFERIDO] | Necessário para jogos elevados/anticheat |
| Sessão | `ProcessIdToSessionId`, `get_TerminalServerSession` | [INFERIDO] | Evita agir em sessão de outro usuário |
| Monitor contínuo | — | [LACUNA] | Não há watcher de processo comprovado |

## F11. Captura de tela e anotação

```
Hotkey global (RegisterHotKey)
   → escolha: capture-region | window | monitor
   → BitBlt/CopyFromScreen (+ SetThreadDpiAwarenessContext)
   → editor SkiaSharp (pencil, rectangle, arrow, text, undo)
   → save | copy | check | close
   → som capture-shutter.wav
```

| passo | origem | confiança | observações |
|---|---|---|---|
| Atalho | `RegisterHotKey`, `UnregisterHotKey` | [CONFIRMADO] | Combinação [LACUNA] |
| Modos | ícones `capture-region`, `window`, `monitor` | [CONFIRMADO] | — |
| Seleção de janela | `EnumWindows`, `GetClassName`, `GetWindowRect` | [CONFIRMADO] | — |
| Captura | `BitBlt`, `GetDC`, `ReleaseDC`, `CopyFromScreen` | [CONFIRMADO] | GDI |
| Editor | `pencil`, `rectangle`, `annotation-arrow`, `text`, `undo` | [CONFIRMADO] | SkiaSharp |
| Ações | `save`, `copy`, `check`, `close` | [CONFIRMADO] | — |
| Som | `Fps.Captures.capture-shutter.wav` (21.656 B) | [CONFIRMADO] | `SoundPlayer` |
| Destino do "save" | — | [LACUNA] | `SaveFileDialog` [INFERIDO] |

## F12. Assistente

```
Conversa (Fps.Assistant)
   → Draft
   → KeyAuth_ChatSend  ──► Worker honest-social-v25-…
   ← KeyAuth_ChatGet (LastPollTimestamp)
   ├─ HumanMode = false → resposta automática
   └─ HumanMode = true  → atendente humano
   Anexos: image | link ; IncludeInContext → perfil do PC ; ShareWithAi → consentimento
```

| item | origem | confiança | observações |
|---|---|---|---|
| Envio | `KeyAuth_ChatSend` | [CONFIRMADO] | — |
| Recebimento | `KeyAuth_ChatGet`, `LastPollTimestamp` | [CONFIRMADO] | Polling |
| Alternância IA/humano | `HumanMode` (get/set) | [CONFIRMADO] | Estado do servidor |
| Contexto | `IncludeInContext` | [CONFIRMADO] | Anexa perfil de hardware |
| Consentimento | `ShareWithAi` | [CONFIRMADO] | Por conversa |
| Anexos | `image`, `link` | [CONFIRMADO] | — |
| Modelo de IA | — | [LACUNA] | Nenhuma string de provedor/modelo |
| Backend | `honest-social-v25-858dca.emulationzone2026.workers.dev` | [CONFIRMADO] | Nome versionado |

## F13. Atualização

| passo | origem | confiança | observações |
|---|---|---|---|
| Versão local | `FileVersionInfo`, `get_Version` (`2.5.1.0`) | [CONFIRMADO] | — |
| Versão publicada | `Version: 2026.10.07` + `KeyAuth_Check` | [INFERIDO] | Comparação feita no cliente ou no servidor [LACUNA] |
| Download | `KeyAuth_GetDownloadLink`, `KeyAuth_Download` | [CONFIRMADO] | Autenticado |
| Aplicação do update | — | [LACUNA] | Execução automática não comprovada |
| Atualização obrigatória | `KeyAuth_Var` | [INFERIDO] | Flags remotas podem bloquear a UI |

## F14. Cancelamento, erro e limpeza

| item | origem | confiança | observações |
|---|---|---|---|
| Cancelar operação | `CheckCancel` no início das etapas | [CONFIRMADO] | Verificado entre etapas, não dentro de uma cópia |
| Progresso | `Stage()` (texto trilíngue) + `MediaTransfer` (200 ms) | [CONFIRMADO] | — |
| Erro de espaço | `Not enough temporary space…` | [CONFIRMADO] | Calculado antes de tocar o destino |
| Erro de destino | `Choose a new ISO file; the source and existing files will not be overwritten.` | [CONFIRMADO] | — |
| Erro de USB | `CheckUsb` falha se `Count != 1` ou `SafeUsb=false` | [CONFIRMADO] | Reidentificação obrigatória |
| Limpeza segura | `cleanupSafe` | [CONFIRMADO] | Só apaga temporários quando verdadeiro |
| Arquivo parcial | `.partial` renomeado só no sucesso | [CONFIRMADO] | Evita ISO corrompida |
| Log de falha | `$warnings`, `KeyAuth_Log` | [CONFIRMADO] (existência) | Conteúdo [LACUNA] |

---

## Resumo visual dos fluxos

```
Auth ──► Home ──┬──► Tweaks ──────────────► HKCU/HKLM
                ├──► Media ──► Analisar ──► Edição/apps/drivers
                │              └─► Build ──► ISO | USB ──► PostInstall (1º logon)
                ├──► Instaladores ──► fila ──► download ──► setup
                ├──► Jogos ──► perfil ──► play + CPU/IO/working set
                ├──► Captura ──► editor ──► save/copy
                └──► Assistente ──► Worker ──► IA | humano
```

**Arquivo gravado:** `C:\re\geandre_v3\docs\06_fluxos.md`
