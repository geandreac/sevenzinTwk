# 04 — Funcionalidades

**Alvo:** `C:\re\geandre_v3\geandre.exe` (SHA-256 `70DC5163…AFCB72`)
**Base:** heap `#Strings` (nomes de membros preservados), `TypeRef`/`MemberRef`, recursos embutidos (`resources_clean\`), scripts PowerShell/C# extraídos, strings ASCII/UTF-16 e o `manifest.json` de ícones.

> **Método:** análise estática. O binário **não foi executado**. Nenhum script extraído foi rodado. As funções abaixo são comprovadas por nomes de símbolos, P/Invoke, trechos de código-fonte embutido e dados de configuração; onde só existe indício, está marcado `[INFERIDO]`.
>
> **Atenção:** este documento descreve *o que o software faz*, não recomenda executá-lo. Várias rotinas alteram `HKLM`, removem aplicativos do sistema, apagam discos USB e desativam o UAC.

Legenda: `[CONFIRMADO]` = lido do arquivo/recurso; `[INFERIDO]` = deduzido com evidência citada; `[LACUNA]` = indeterminado.

---

## 1. Autenticação, licença e proteção

| item | origem | confiança | observações |
|---|---|---|---|
| Login por usuário/senha | `KeyAuth_Login` no heap `#Strings` | [CONFIRMADO] | API KeyAuth |
| Login por licença (key) | `KeyAuth_License` | [CONFIRMADO] | Segundo modo de entrada |
| Login via navegador / device code | `KeyAuth_WebLogin` | [CONFIRMADO] | Terceiro modo |
| Registro de conta | `KeyAuth_Register` | [CONFIRMADO] | Cadastro dentro do app |
| Recuperação de senha | `KeyAuth_Forgot` | [CONFIRMADO] | — |
| Sessão | `KeyAuth_Init`, `KeyAuth_Check`, `KeyAuth_Logout` | [CONFIRMADO] | `Check` valida a sessão periodicamente |
| Handshake criptográfico | `KeyAuth_HandshakeBegin`, `KeyAuth_HandshakeComplete`, `KeyAuth_EncryptString`, `KeyAuth_DecryptString`, `KeyAuth_DecryptBytes` | [CONFIRMADO] | Sessão cifrada ponta a ponta com o backend |
| 2FA | `KeyAuth_Enable2FA`, `KeyAuth_Disable2FA`, `KeyAuth_GetTwoFactorSecret` | [CONFIRMADO] | TOTP |
| Dados do usuário | `KeyAuth_GetUserDataJson`, `KeyAuth_GetAppDataJson`, `KeyAuth_GetResponseJson`, `KeyAuth_GetResponseSuccess`, `KeyAuth_GetResponseMessage` | [CONFIRMADO] | Respostas JSON |
| Expiração da licença | `KeyAuth_ExpiryDaysLeft` | [CONFIRMADO] | Dias restantes exibidos na conta |
| Variáveis remotas | `KeyAuth_SetVar`, `KeyAuth_GetVar`, `KeyAuth_Var` | [CONFIRMADO] | Configuração/feature flags vindas do servidor |
| Banimento / lista negra | `KeyAuth_Ban`, `KeyAuth_CheckBlack`, `KeyAuth_Log` | [CONFIRMADO] | Bloqueio por HWID/conta e log remoto |
| Troca de nome de usuário | `KeyAuth_ChangeUsername` | [CONFIRMADO] | — |
| Estatísticas e presença | `KeyAuth_FetchOnline`, `KeyAuth_FetchStats` | [CONFIRMADO] | Usuários online e métricas do app |
| Webhook | `KeyAuth_Webhook` | [CONFIRMADO] | Notificação externa (provavelmente Discord) |
| Download autenticado | `KeyAuth_Download`, `KeyAuth_GetDownloadLink` | [CONFIRMADO] | Arquivos liberados por licença |
| Chat no backend | `KeyAuth_ChatSend`, `KeyAuth_ChatGet` | [CONFIRMADO] | Ver seção 10 |
| Endpoint | `Honest.social-endpoint.json` → `https://honest.emulationzone2026.workers.dev` | [CONFIRMADO] | Cloudflare Worker; há `previousApiUrl` e `previousApiUrls` |
| Camada nativa de auth | `Honest.native.auth_native.dll` (3.472.388 B) | [CONFIRMADO] | Exportações e algoritmo [LACUNA] |
| Verificação de integridade | `VerifyAssemblyIntegrity` + `System.Reflection.Metadata` | [CONFIRMADO] | Lê os próprios metadados em runtime |
| Geração/checagem de licença | `GenerateLicense`, `CheckLicense` | [CONFIRMADO] | Nomes no heap; algoritmo [LACUNA] |
| Anti-debug | `IsDebuggerPresent`, `CheckRemoteDebuggerPresent`, `NtQueryInformationProcess` | [CONFIRMADO] (P/Invoke) | Chamada efetiva [INFERIDO] |
| Detecção de janelas de análise | `EnumWindows`, `GetClassName`, `GetWindowThreadProcessId` | [CONFIRMADO] (P/Invoke) | Procura por janelas de depurador/ferramentas |
| Ofuscação de controle de fluxo | Eazfuscator.NET | [CONFIRMADO] | Recurso `Eazfuscator.NET.Runtime.Licensing.WindowsForms.Error.png` |
| HWID | `fingerprint` ("Identificação"), `AccountKey` | [CONFIRMADO] | Identificador de dispositivo enviado ao backend; composição exata [LACUNA] |

`[INFERIDO]` O fluxo real é: `Init` → handshake → `Login`/`License`/`WebLogin` → `Check` periódico; falha de integridade ou depurador encerra o app antes da UI. Evidência: ordem dos símbolos e a presença de `VerifyAssemblyIntegrity` no mesmo assembly do entry point.

## 2. Perfil de hardware e recomendações

| item | origem | confiança | observações |
|---|---|---|---|
| Perfil salvo | `SavedPcProfile`, `HasDetails`, `UpdatedUtc` | [CONFIRMADO] | Persistido localmente (JSON pelo uso de Newtonsoft/System.Text.Json) |
| Perfil recomendado | `RecommendedProfile`, `HasCustomSettings` | [CONFIRMADO] | Comparação com o perfil do usuário |
| Campos | `DeviceType`, `Memory`, `Motherboard`, `PowerSupply`, `Case`, `Cooler`, `Storage`, `Display`, `Country`, `NotebookModel` | [CONFIRMADO] | Ficha completa, incluindo itens que não vêm de WMI (gabinete, fonte, cooler) |
| Edição manual | `HasCustomSettings`, `HasDetails` | [CONFIRMADO] | Usuário pode sobrescrever o detectado |
| Identificação de GPU offline | `Honest.Nvidia.PciIds` (112.604 B) | [CONFIRMADO] | Tabela `vendor device` + nome comercial; comentário no arquivo: `# NVIDIA vendor section bundled for offline GPU identification.` |
| Consulta WMI | `ManagementObjectSearcher` | [CONFIRMADO] (tipo presente) | Classes consultadas [LACUNA] — nenhuma string `Win32_*` em claro |
| Contadores de desempenho | `PerformanceCounter` | [CONFIRMADO] (tipo presente) | Nenhum nome de contador/categoria recuperado [LACUNA] |
| Sensores de temperatura/clock | nenhuma API (`NVAPI`, `NVML`, `ADL`, `IGCL`, `PDH`, `WinRing0`) em claro | [LACUNA] | Não é possível afirmar leitura de temperatura, fan ou clocks |
| Cache do perfil | `SelectedId`, `AccountKey` | [CONFIRMADO] | Chave por conta/dispositivo |

`[INFERIDO]` A página de GPU identifica o modelo por PCI ID **sem internet** e oferece o driver/utilitário correspondente; os cards da Home usam o perfil para sugerir ajustes. Não há evidência de telemetria ao vivo (ver `05_metricas_instaladores_jogos.md`).

## 3. Ajustes do Windows (tweaks)

Os 12 grupos são os mesmos no app e no `PostInstall.ps1` embutido, o que garante paridade entre "aplicar agora" e "aplicar na instalação do Windows".

| grupo (`$settings.*`) | chaves/valores | confiança | observações |
|---|---|---|---|
| `notifications` | HKCU `PushNotifications\ToastEnabled=0`; `Notifications\Settings\NOC_GLOBAL_SETTING_TOASTS_ENABLED=0`; Policies Explorer `DisableNotificationCenter=1`; `NoToastApplicationNotification=1`; `NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND=0` | [CONFIRMADO] | 5 valores |
| `suggestions` | HKCU `ContentDeliveryManager`: `SilentInstalledAppsEnabled`, `SystemPaneSuggestionsEnabled`, `SoftLandingEnabled`, `SubscribedContent-338388/338389/310093/353694/353696 = 0`; `UserProfileEngagement\ScoobeSystemSettingEnabled=0`; HKLM Policies CloudContent `DisableWindowsConsumerFeatures=1` | [CONFIRMADO] | 10 valores |
| `widgets` | HKCU `Explorer\Advanced\TaskbarDa=0`; `Feeds\ShellFeedsTaskbarViewMode=2`; HKLM Policies `Dsh\AllowNewsAndInterests=0`; `Windows Feeds\EnableFeeds=0` | [CONFIRMADO] | — |
| `searchHighlights` | HKCU `SearchSettings\IsDynamicSearchBoxEnabled=0`; HKLM Policies Windows Search `EnableDynamicContentInWSB=0` | [CONFIRMADO] | — |
| `advertising` | HKCU `AdvertisingInfo\Enabled=0`; HKLM Policies AdvertisingInfo `DisabledByGroupPolicy=1` | [CONFIRMADO] | — |
| `tailored` | HKCU `Privacy\TailoredExperiencesWithDiagnosticDataEnabled=0`; HKLM Policies CloudContent `DisableTailoredExperiencesWithDiagnosticData=1` | [CONFIRMADO] | — |
| `diagnostics` | HKLM Policies DataCollection `AllowTelemetry=1` | [CONFIRMADO] | **Não zera** a telemetria; fixa no mínimo permitido |
| `activityHistory` | HKLM Policies System `EnableActivityFeed=0`, `PublishUserActivities=0`, `UploadUserActivities=0` | [CONFIRMADO] | — |
| `gameDvr` | HKCU `GameDVR\AppCaptureEnabled=0`, `HistoricalCaptureEnabled=0`, `System\GameConfigStore\GameDVR_Enabled=0`; HKLM Policies GameDVR `AllowGameDVR=0` | [CONFIRMADO] | — |
| `uac` | HKLM `Policies\System\EnableLUA=0` | [CONFIRMADO] | **Desativa o UAC** (requer reinício) |
| `oneDrive` | HKLM Policies OneDrive `DisableFileSyncNGSC=1` + desinstalação no primeiro logon | [CONFIRMADO] | Ver seção 5 |
| `extras` | lista de IDs winget (`steam`, `discord`, `7zip`, `chrome`, `vlc`) | [CONFIRMADO] | Allowlist fechada |
| Aplicação por item | `SetValue` + `ApplyPreference` com `try/catch`, `$failed` | [CONFIRMADO] | Falha em uma chave não aborta as demais |
| Idempotência | `New-Item -Force`, `New-ItemProperty -Force`, tipo `DWord` | [CONFIRMADO] | Reaplicável |
| Estágio `specialize` | carrega `C:\Users\Default\NTUSER.DAT` em `HKLM\HonestDefUser`, aplica e descarrega | [CONFIRMADO] | Toda conta nova herda os ajustes |
| Estágio `logon` | aplica em `HKCU:` e faz OneDrive/winget | [CONFIRMADO] | Precisa de sessão e internet |

## 4. Debloat de apps e capabilities (imagem do Windows)

| item | origem | confiança | observações |
|---|---|---|---|
| Inventário | `AnalyzeImage.ps1` → lista de pacotes e capabilities | [CONFIRMADO] | Appx e `capability:*` |
| Remoção | `$request.remove` aceita `appx:*`, `capability:*`, `setup:OneDrive` | [CONFIRMADO] | Validado em `BuildMedia.ps1` |
| Apps protegidos | `MediaRules.ps1` → `ProtectedApp()` | [CONFIRMADO] | Regex por prefixo |
| Lista protegida (apps) | `Microsoft.SecHealthUI`, `WindowsStore`, `StorePurchaseApp`, `DesktopAppInstaller`, `MicrosoftEdge`, `Edge`, `VCLibs`, `NET.Native`, `UI.Xaml`, `WindowsAppRuntime`, `Win32WebViewHost`, `AAD.BrokerPlugin`, `AccountsControl`, `Windows.ShellExperienceHost`, `Windows.StartMenuExperienceHost`, `Windows.CloudExperienceHost` | [CONFIRMADO] | Não podem ser removidos |
| Prefixo protegido | qualquer pacote iniciado por `Windows.` | [CONFIRMADO] | Proteção ampla |
| Capabilities protegidas | `Language.*`, `Microsoft.Windows.Ethernet*`, `Wifi`, `Sense.Client`, `OneCore*`, `OneCoreUAP*`, `Windows.Client.ShellComponents`, `Windows.Kernel.*`, `Print.Management`, `DirectX.*` | [CONFIRMADO] | — |
| Dependências | `AppDependencies()` lê `AppxManifest.xml` e adiciona frameworks | [CONFIRMADO] | Impede remover dependência de app mantido |
| Regra de recursos | `ResourcePackage=true` → satélites de idioma compartilham a identidade do pai | [CONFIRMADO] | Comentário no script |
| Dupla validação | "Protected packages must agree in both directions" | [CONFIRMADO] | Inventário e criação usam a mesma regra |
| Servicing | apps/capabilities exigem `DISM` (`$needsServicing`) | [CONFIRMADO] | Também drivers |
| Espaço em disco | cálculo por modo: ISO ≈ `sourceSize*3 + 2 GB` com servicing, `*2 + 2 GB` sem; USB ≈ `*2`/`*1 + 2 GB` | [CONFIRMADO] | `BuildMedia.ps1` |
| Arquitetura | `0=x86`, `9=x64`, `12=ARM64`; outras → erro | [CONFIRMADO] | `$detail.Architecture` |

## 5. Remoção de Edge e OneDrive

### Edge

| item | origem | confiança | observações |
|---|---|---|---|
| Alvo | `Microsoft Edge` com `NoRemove != 1` | [CONFIRMADO] | `115_Honest.Tweaks.EdgeBrowserUninstall.ps1` |
| Exclusão de WebView2 | `UninstallString` contendo `webview` ou `edgeupdate` é ignorado | [CONFIRMADO] | Preserva WebView2 |
| Caminho aceito | `...\<versão>\Installer\setup.exe` sob `MachineRoots`/`UserRoots` | [CONFIRMADO] | Regex `^\d+\.\d+\.\d+\.\d+\\Installer\\setup\.exe$` |
| Verificação de raiz | `GetFullPath` + prefixo do root com `OrdinalIgnoreCase` | [CONFIRMADO] | Evita executar fora da árvore do Edge |
| Argumentos | `--uninstall --verbose-logging` (+ `--system-level`) | [CONFIRMADO] | — |
| Assinatura | `Test-HonestMicrosoftSignature`: `Status=Valid` e `O=Microsoft Corporation` | [CONFIRMADO] | Impede binário trocado |
| Escopo | HKLM, WOW6432Node e HKCU | [CONFIRMADO] | 4 localizações de `Uninstall` |

### OneDrive

| item | origem | confiança | observações |
|---|---|---|---|
| Plano | `Get-HonestOneDrivePlan` valida `UninstallString` | [CONFIRMADO] | `116_Honest.Tweaks.OneDriveUninstall.ps1` |
| Caminho aceito | `%ProgramFiles%\Microsoft OneDrive\...\OneDriveSetup.exe` ou `%LocalAppData%\Microsoft\OneDrive\...` | [CONFIRMADO] | — |
| Argumentos | `/uninstall` (+ `/allusers` se machine) | [CONFIRMADO] | — |
| Pré-condição | `DisplayName = Microsoft OneDrive`, `NoRemove != 1` | [CONFIRMADO] | — |
| Sem desinstalador | `exit 2` com mensagem "OneDrive preservado" | [CONFIRMADO] | Não força remoção |
| Proteção de dados | comentário explícito: não apagar raiz, dados, atalhos ou Known Folders | [CONFIRMADO] | — |
| Assinatura | mesma validação Microsoft | [CONFIRMADO] | — |
| Pós-remoção | `DisableFileSyncNGSC=1` | [CONFIRMADO] | — |

## 6. Drivers

| item | origem | confiança | observações |
|---|---|---|---|
| Injeção | `Add-WindowsDriver -Recurse` | [CONFIRMADO] | `BuildMedia.ps1`/`MediaOperations.ps1` |
| Entrada | `$request.drivers` (pasta do host) | [CONFIRMADO] | Exportação prévia pelo app |
| Alvo | imagem montada (`install.wim`) | [CONFIRMADO] | Requer servicing |
| Espaço | entra no cálculo de `$required` | [CONFIRMADO] | — |
| Falha | `$warnings` + `$result.ok=false` | [CONFIRMADO] | Não corrompe silenciosamente |

`[INFERIDO]` A exportação usa `Export-WindowsDriver` ou cópia de `DriverStore`; o nome exato não apareceu nas strings. Evidência: só `Add-WindowsDriver` está em claro.

## 7. Instaladores e downloads

| item | origem | confiança | observações |
|---|---|---|---|
| Catálogo | 50+ logos em `resources_clean` (`7zip`, `discord`, `googlechrome`, `vlcplayer`, `visualstudio`, `vscode`, `spotify`, `brave`, `everything`, `revouninstaller`, `autorun`, `processexplorer`, `notepad`, `qbittorrent`, `obsstudio`, `capframex`, `hwinfo`, `hwmonitor`, `cpuz`, `gpuz`, `occt`, `ocat`, `prime95`, `testmem5`, `geek`, `ddu`, `nvcleanstall`, `msiafterburner`, `amdchipset`, `radeonslimmer`, `logitechghub`, `teamspeak`, `medaltv`, `faceit`, `gamersclub`, `steam`, `eaapp`, `epicgames`, `ubisoftconnect`, `rockstar`, `minecraft`, `fivem`, `leagueoflegends`, `valorant`, `directx`, `vcredist`, `internetdriver`, `bluetoothdriver`, `unlocker`, `openshell`, `startallback`, `latencymon`, `kmsactivator`) | [CONFIRMADO] | Um logo por item do catálogo |
| Metadados por item | nome, URL, tamanho, hash, argumentos | [LACUNA] | Ficam em código ofuscado/`#US`; não recuperados |
| Download | `WebClient.DownloadFileAsync`, `HttpClient`, `DownloadFileCompleted` | [CONFIRMADO] | Progresso por evento |
| Instalação silenciosa | `Start-Process -WindowStyle Hidden`, `-ArgumentList`; `--silent` apenas no comando winget | [CONFIRMADO] | Flags `/S` e `/silent` **não** foram confirmadas nas strings; as ocorrências são ruído binário |
| Desinstalação | `Start-Process -ArgumentList '/uninstall' -WindowStyle Hidden` | [CONFIRMADO] | — |
| winget (allowlist) | `steam→Valve.Steam`, `discord→Discord.Discord`, `7zip→7zip.7zip`, `chrome→Google.Chrome`, `vlc→VideoLAN.VLC` | [CONFIRMADO] | Qualquer outro ID → `$failed=true` |
| Comando winget | `--exact --source winget --silent --accept-source-agreements --accept-package-agreements --disable-interactivity --no-upgrade` | [CONFIRMADO] | Código `-1978335189` (= já instalado) é aceito |
| Espera do winget | até 30 tentativas × 3 s aguardando `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` | [CONFIRMADO] | Primeiro logon |
| Fila | `pendingtasks`, `scheduled-tasks`, `Stage()` | [CONFIRMADO] | Fila interna de downloads/instalações |
| Agendador do Windows | nenhuma string `schtasks`/`Register-ScheduledTask` | [CONFIRMADO] (ausência) | "Tarefas agendadas" **não** é o Task Scheduler |
| Retomada | `Stage`, `$failed`, `$warnings` | [INFERIDO] | Fila sequencial com estado por item; sem persistência entre reinícios comprovada |

## 8. Jogos e otimização por processo

| item | origem | confiança | observações |
|---|---|---|---|
| Jogo Steam | `SteamAppId`, `KnownPaths` | [CONFIRMADO] | Descoberta por biblioteca |
| Jogo Epic | `EpicAppName` | [CONFIRMADO] | Manifestos Epic |
| Jogo custom | `IsCustomGame`, `CustomPath`, `ExecutableName` | [CONFIRMADO] | Executável escolhido à mão |
| Capa/favorito | `image`, `star`, `link` | [CONFIRMADO] | Personalização do card |
| Iniciar | `play`, `Process.Start` | [CONFIRMADO] | Botão "Jogar" |
| CPU Sets | `GetSystemCpuSetInformation`, `GetProcessDefaultCpuSets`, `SetProcessDefaultCpuSets`, `GetThreadSelectedCpuSets`, `SetThreadSelectedCpuSets` | [CONFIRMADO] | P/Invoke completo |
| Afinidade | `GetProcessAffinityMask`, `SetProcessAffinityMask` | [CONFIRMADO] | — |
| Prioridade | `GetPriorityClass`, `SetPriorityClass`, `GetThreadPriority`, `SetThreadPriority`, `SetPriorityClass`, `PriorityClass` | [CONFIRMADO] | `CpuPriority` |
| Boost | `GetProcessPriorityBoost`, `SetProcessPriorityBoost`, `GetThreadPriorityBoost`, `SetThreadPriorityBoost` | [CONFIRMADO] | — |
| Prioridade de I/O | `IoPriority` (propriedade) | [CONFIRMADO] | Implementação [LACUNA] — sem `NtSetInformationProcess` em claro |
| Working set | `SetProcessWorkingSetSize` | [CONFIRMADO] | Redução de memória |
| Toggles | `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled` | [CONFIRMADO] | Por jogo |
| Presets | `SelectedId`, `RecommendedProfile` | [CONFIRMADO] | — |
| Catálogo de cards | `cardEafc`, `cardFortnite`, `cardRust`, `cardWarzone` | [CONFIRMADO] | 4 cards com imagem |
| Logos de jogos/plataformas | `valorant`, `leagueoflegends`, `fivem`, `minecraft`, `faceit`, `gamersclub`, `medaltv`, `steam`, `epicgames`, `eaapp`, `ubisoftconnect`, `rockstar` | [CONFIRMADO] | — |
| Game Mode | `GameModeLogo`, `logo_gamersclub` | [INFERIDO] | Provável atalho/estado do modo jogo |

`[INFERIDO]` A otimização é aplicada no processo já em execução (ou no lançamento), pois todos os P/Invoke recebem handles de processo/thread — não é um serviço residente. Evidência: conjunto de APIs `SetProcess*`/`SetThread*` e a ausência de driver/serviço próprio.

## 9. Captura de tela e anotação

| item | origem | confiança | observações |
|---|---|---|---|
| Modos | `capture-region`, `window`, `monitor` | [CONFIRMADO] | Região, janela, tela inteira |
| Captura | `BitBlt`, `GetDC`, `ReleaseDC`, `CopyFromScreen` | [CONFIRMADO] | GDI |
| Hotkeys globais | `RegisterHotKey`, `UnregisterHotKey` | [CONFIRMADO] | Combinação exata [LACUNA] |
| Hook de teclado | `SetWindowsHookEx`, `GetAsyncKeyState` | [CONFIRMADO] | Captura de atalho/ESC |
| Seleção de janela | `EnumWindows`, `GetClassName`, `GetWindowRect` | [CONFIRMADO] | Escolha por clique |
| DPI | `SetThreadDpiAwarenessContext` | [CONFIRMADO] | Correção de escala por monitor |
| Editor | `pencil`, `rectangle`, `annotation-arrow`, `text`, `undo` | [CONFIRMADO] | Ferramentas de anotação |
| Render | SkiaSharp `SKBitmap`, `SKCanvas`, `SKCodec` | [CONFIRMADO] | Desenho vetorial sobre o bitmap |
| Ações | `save`, `copy`, `check`, `close` | [CONFIRMADO] | Salvar, copiar, confirmar, fechar |
| Som | `Fps.Captures.capture-shutter.wav` (21.656 B) | [CONFIRMADO] | Obturador |
| Salvamento | `SaveFileDialog`/`Clipboard` | [INFERIDO] | Tipos não recuperados por nome |
| Overlay | janela sem borda em tela cheia | [INFERIDO] | Compatível com `CreateRoundRectRgn`/`SetWindowPos` |

## 10. Assistente (IA e modo humano)

| item | origem | confiança | observações |
|---|---|---|---|
| Namespace | `Fps.Assistant` | [CONFIRMADO] | Módulo separado do resto |
| Conversas | `Conversations`, `Messages`, `Draft`, `Role`, `Text`, `CreatedUtc` | [CONFIRMADO] | Histórico local |
| Sincronização | `LastPollTimestamp`, `KeyAuth_ChatGet` | [CONFIRMADO] | Polling do backend |
| Envio | `KeyAuth_ChatSend` | [CONFIRMADO] | — |
| Modo humano | `HumanMode` (get/set) | [CONFIRMADO] | Alterna IA ↔ atendente |
| Contexto do PC | `IncludeInContext` | [CONFIRMADO] | Anexa o perfil de hardware à conversa |
| Compartilhar com IA | `ShareWithAi` | [CONFIRMADO] | Consentimento explícito por conversa |
| Anexos | `image` ("Imagem / capa"), `link` ("Vincular") | [CONFIRMADO] | Envio de imagem/URL |
| Backend | `honest-social-v25-858dca.emulationzone2026.workers.dev` | [CONFIRMADO] | Nome com sufixo de versão |
| Modelo/provedor de IA | — | [LACUNA] | Nenhuma string de modelo; `OpenAI` no binário é nome de CA de certificado |
| Avatar/envio | `assistant_avatar.png` (1.368 B), `assistant_send.png` (2.368 B) | [CONFIRMADO] | — |

`[INFERIDO]` O assistente não fala direto com um provedor de LLM: toda mensagem passa pelo Worker, que decide entre IA e atendimento humano. Evidência: `ChatSend`/`ChatGet` são métodos KeyAuth, e `HumanMode` é estado do lado do servidor.

## 11. Windows Media (ISO e pendrive)

| item | origem | confiança | observações |
|---|---|---|---|
| Listar discos | `action='disks'` | [CONFIRMADO] | Retorna discos/letras |
| Construir | `action='build'` | [CONFIRMADO] | `mode` `iso` ou `usb` |
| Entrada | ISO ou instalação do host + `index` da edição | [CONFIRMADO] | `Get-WindowsImage` |
| Edição | `$request.edition` | [CONFIRMADO] | — |
| Idiomas | `$request.lang` + `T(en,pt,es)` | [CONFIRMADO] | 0/1/2 |
| ISO | `HonestIsoImage` (IMAPI2FS) → UDF El Torito com entradas BIOS e EFI | [CONFIRMADO] | `Honest.WindowsMedia.IsoImage.cs` |
| USB | particionamento + cópia + verificação | [CONFIRMADO] | `MediaOperations.ps1` |
| FAT32 | split de `install.wim` em 3.800 MB | [CONFIRMADO] | Limite de 4 GB |
| NTFS | requer UEFI:NTFS (`ntfs_x64.efi` etc.) | [CONFIRMADO] | `$result.usbBoot` |
| Arquivos UEFI | `bootx64.efi`, `bootia32.efi`, `bootaa64.efi`, `ntfs_x64/ia32/aa64.efi` | [CONFIRMADO] | Origem Rufus/UEFI:NTFS, com licenças e fontes GPL embutidas |
| Segurança de disco | `SafeUsb`, `UsbRecord`, `CheckUsb`, `ValidateUsbBoot`, `Clear-Disk` | [CONFIRMADO] | Recusa disco do sistema/removível errado |
| Cópia | 1 MB por bloco + SHA-256 + verificação | [CONFIRMADO] | `MediaTransfer.cs`; progresso a cada 200 ms |
| Cancelamento | `CheckCancel`, `Stage()` | [CONFIRMADO] | — |
| Resultado | `ok`, `error`, `warnings`, `cleanupSafe`, `usbFileSystem`, `usbBoot`, `output`, `mode`, `removed`, `edition` | [CONFIRMADO] | Contrato do script |
| Pós-instalação | `Honest-config.json` = receita; `Configure.ps1` em `sources\$OEM$\$1\HonestSetup` | [CONFIRMADO] | Executa no primeiro logon |
| Offline | tweaks aplicados direto no hive (`OfflineHive.cs`, `offreg.dll`) | [CONFIRMADO] | `ORGetValue`/`ORSetValue` |
| Limpeza | `cleanupSafe` indica se a pasta temporária pode ser apagada | [CONFIRMADO] | — |
| Aviso legal | "Do not claim universal Secure Boot compatibility or BIOS/Legacy support." | [CONFIRMADO] | Comentário no script |

## 12. WIM, disco virtual e reciclagem

| item | origem | confiança | observações |
|---|---|---|---|
| WIM | `wimgapi.dll` + `WIMCreateFile`, `WIMGetImageInformation`, `WIMSetTemporaryPath`, `WIMCloseHandle` | [CONFIRMADO] | Leitura de `install.wim`/`install.esd` |
| Disco virtual | `OpenVirtualDisk`, `AttachVirtualDisk`, `DetachVirtualDisk` | [CONFIRMADO] | Montagem de VHD/VHDX para inspecionar a imagem |
| Reciclagem — consulta | `SHQueryRecycleBin` | [CONFIRMADO] | Tamanho/itens da lixeira |
| Reciclagem — limpar | `SHEmptyRecycleBin` | [CONFIRMADO] | Ação de limpeza |
| Lixeira na UI | provável card de "Armazenamento" | [INFERIDO] | Ícone `hdd` e P/Invoke de reciclagem |

## 13. Fontes e identidade visual

| item | origem | confiança | observações |
|---|---|---|---|
| Instalação de fontes | `AddFontResource`, `AddFontResourceEx` | [CONFIRMADO] | P/Invoke |
| Fontes embutidas | Inter (Regular/Bold/WPF) e Geist/Geist Mono (Regular/Medium/SemiBold/Bold) | [CONFIRMADO] | 13 recursos TTF |
| Duplicação | `Honest.Native.Fonts.*` e `Honest.Resources.Fonts.*` | [CONFIRMADO] | Caminho nativo vs. UI |
| Registro de fontes | `WM_FONTCHANGE`/broadcast | [INFERIDO] | Necessário após `AddFontResource` |
| Ícones | 52 PNG Lucide/Tabler, cor `#E2E2E2`, stroke 1,6 | [CONFIRMADO] | `Honest.UI.Icons.manifest.json` |
| Compatibilidade legada | `legacyResources` no manifesto | [CONFIRMADO] | Mapeia bitmaps antigos → ícones novos |

## 14. Sistema, versão e diagnóstico

| item | origem | confiança | observações |
|---|---|---|---|
| Versão exibida | `get_Version`, `FileVersionInfo` | [CONFIRMADO] | Lê a própria versão |
| Verificação de atualização | `KeyAuth_Check`, `Version: 2026.10.07` | [CONFIRMADO] | Comparação com a versão publicada |
| Atualização | `KeyAuth_Download`, `KeyAuth_GetDownloadLink` | [CONFIRMADO] | Download autenticado; execução automática [LACUNA] |
| Serviços | `System.ServiceProcess.ServiceController` | [CONFIRMADO] | Consulta/controle de serviços |
| Event Log | `System.Diagnostics.EventLog` | [CONFIRMADO] | Leitura de eventos |
| Contadores | `System.Diagnostics.PerformanceCounter` | [CONFIRMADO] | Uso efetivo [LACUNA] |
| Elevação | `requireAdministrator` no manifesto | [CONFIRMADO] | Necessária para HKLM/DISM/disco |
| Instância única | mutex/nome de processo | [LACUNA] | Não confirmado |
| Localização | `TwoLetterISOLanguageName`, `T(en,pt,es)` | [CONFIRMADO] | 3 idiomas |
| Backend social | `Honest.social-endpoint.json` com 3 URLs | [CONFIRMADO] | Migração de endpoint |
| Assinatura Authenticode | ausente | [CONFIRMADO] | Binário não assinado |

## 15. Mapa funcional resumido

```
Auth (KeyAuth + HWID + integridade + anti-debug)
  └─ Shell
       ├─ Perfil de hardware (WMI + PCI IDs offline) ──► recomendações
       ├─ Tweaks (12 grupos) ──► HKCU/HKLM imediato
       ├─ Windows Media
       │    ├─ Analisar imagem (appx/capabilities) ──► seleção
       │    ├─ Drivers (Add-WindowsDriver)
       │    ├─ ISO (IMAPI2FS) | USB (FAT32 split / NTFS + UEFI:NTFS)
       │    └─ PostInstall (specialize + logon)
       ├─ Instaladores (fila + winget allowlist + download)
       ├─ Jogos (Steam/Epic/custom) ──► CPU Sets/afinidade/prioridade/IO
       ├─ Captura (BitBlt + hotkeys + anotação Skia)
       ├─ Assistente (ChatSend/ChatGet, HumanMode, contexto do PC)
       └─ Manutenção (WIM, VHD, lixeira, fontes, serviços/eventos)
```

`[INFERIDO]` O app é um "all-in-one" de preparação de PC: o mesmo estado (perfil + receita de tweaks) alimenta a otimização local, a criação de mídia e o pós-instalação. Evidência: `Honest-config.json` reutiliza os 12 grupos de `$settings`, e o perfil do PC é citado tanto na Home quanto no contexto do assistente.

## 16. Lacunas desta fase

| item | confiança | observações |
|---|---|---|
| Catálogo exato de instaladores (URL, hash, argumentos) | [LACUNA] | Em `#US`/código ofuscado |
| Implementação de `IoPriority` | [LACUNA] | Só a propriedade, sem P/Invoke em claro |
| Sensores de GPU/CPU (temperatura, clocks, uso) | [LACUNA] | Sem NVAPI/NVML/ADL/PDH em claro |
| Algoritmo de HWID | [LACUNA] | `auth_native.dll` não analisada |
| Regras exatas de atualização obrigatória | [LACUNA] | Só `KeyAuth_Check` |
| Persistência da fila de tarefas | [LACUNA] | Sem armazenamento identificado |
| Textos e mensagens de erro | [LACUNA] | `#US` cifrado |

**Arquivo gravado:** `C:\re\geandre_v3\docs\04_funcionalidades.md`
