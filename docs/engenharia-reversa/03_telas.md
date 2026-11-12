# 03 — Telas

**Alvo:** `C:\re\geandre_v3\geandre.exe`
**Base:** recursos de UI (`Honest.UI.Icons.*`, `Honest.Resources.*`, `Fps.*`), nomes de controles extraídos dos blobs `.resources`, modelo de domínio do heap `#Strings` e scripts embutidos.

> Nenhum texto de rótulo foi recuperado do binário (`#US` cifrado). Os nomes de tela abaixo vêm de: (a) `label` dos 52 ícones no `Honest.UI.Icons.manifest.json` (português), (b) nomes de controles, (c) nomes de recursos. Onde o vínculo é dedutivo, está marcado `[INFERIDO]`.

Legenda: `[CONFIRMADO]` / `[INFERIDO]` / `[LACUNA]`.

---

## 1. Visão geral

| tela | origem | confiança | observações |
|---|---|---|---|
| Login | `honest_login_ascii.png`, `loginbg`, `KeyAuth_Login`, `ic_fingerprint` | [CONFIRMADO] | Entrada com autenticação KeyAuth; ícone de digital sugere HWID |
| Shell / navegação | blob `006` (`LogoPic`, `Nav.IconGames`, `Nav.IconInstallers`) | [CONFIRMADO] | Painel lateral com logo e ícones |
| Home | `Fps.HomePhotos.driver/ai/games.jpg`, blob `014` (7 picture boxes) | [CONFIRMADO] | 3 cards principais |
| Jogos | blob `009` (`cardEafc`, `cardFortnite`, `cardRust`, `cardWarzone`), `gamepad`, `play` | [CONFIRMADO] | 4 cards + "Jogar" |
| Placa de vídeo (GPU) | `nav_gpu`/`gpu`, `logo_gpuz`, `logo_ddu`, `logo_msiafterburner`, `logo_nvcleanstall`, `Honest.Nvidia.PciIds` | [CONFIRMADO] | Diagnóstico e ferramentas de GPU |
| Processador (CPU) | `cpu`, blob `008` (`IconIntel`) | [CONFIRMADO] | Página de CPU |
| Armazenamento | `hdd` | [CONFIRMADO] | Disco/SSD |
| Localização | `location` | [CONFIRMADO] | Região/país |
| Mouse | `mouse` | [CONFIRMADO] | Configuração de mouse |
| Ajustes / Tweaks | `tweaks`, `gearicon`, 12 grupos de `PostInstall.ps1` | [CONFIRMADO] | Toggles de sistema |
| Instaladores | `download`, `nav_installers`, ~40 logos | [CONFIRMADO] | Catálogo de downloads |
| Guias / Como usar | `guides`, `nav_guides` | [CONFIRMADO] | Conteúdo instrucional |
| BIOS | `bios`/`nav_bios`, `show_bios` | [CONFIRMADO] | Guia/config de BIOS |
| Assistente | `assistant_avatar`, `assistant_send`, `Fps.Assistant`, `KeyAuth_ChatSend` | [CONFIRMADO] | Chat com IA |
| Capturas | `camera`, `capture-region`, `window`, `monitor`, ferramentas de anotação, `capture-shutter.wav` | [CONFIRMADO] | Captura + editor |
| Pendrive / Windows Media | `usb`, `BuildMedia.ps1`, `IsoImage.cs` | [CONFIRMADO] | Criar ISO/USB de instalação |
| Perfil | `user`, `SavedPcProfile` | [CONFIRMADO] | Perfil do usuário e do PC |
| Configurações | `settings`, `Honest.social-endpoint.json` | [CONFIRMADO] | Preferências, idioma, conta |
| Notificações | `notifications`/`notif_icon` | [CONFIRMADO] | Grupo de tweaks de notificações |
| Tarefas agendadas | `scheduled-tasks`/`pendingtasks` | [CONFIRMADO] | Lista de tarefas pendentes/agendadas |
| Proteção | `protection`/`protecao` | [CONFIRMADO] | Segurança do sistema |
| Sair / encerrar | `power` (label "Sair") | [CONFIRMADO] | Ação de logout |

`[INFERIDO]` A UI é um único `Form` principal sem borda, com painel de navegação à esquerda e área de conteúdo à direita, trocada por `Guna2Transition` (blob `011`: `BlindCoeff`, `MosaicShift`, `SlideCoeff`). Evidência: `Nav.Icon*` indica navegação persistente; `Guna2DragControl` + `DwmSetWindowAttribute`/`CreateRoundRectRgn` indicam janela customizada.

---

## 2. Login

| item | origem | confiança | observações |
|---|---|---|---|
| Arte de fundo ASCII | `Honest.Resources.honest_login_ascii.png` (504.572 B) | [CONFIRMADO] | Imagem grande, provável painel esquerdo |
| Fundo | `loginbg` (bitmap de `Properties.Resources`) | [CONFIRMADO] | Nome recuperado na lista dos 85 bitmaps |
| Logo | `honest_dot_logo.png` / `sem_fundo.png` (1254×1254) | [CONFIRMADO] | — |
| Ícone de identificação | `ic_fingerprint` → ícone `fingerprint`, label "Identificação" | [CONFIRMADO] | Sugere login por HWID/dispositivo |
| Ícone de usuário | `user` ("Perfil") | [CONFIRMADO] | Campo de usuário |
| Autenticação | `KeyAuth_Login`, `KeyAuth_WebLogin`, `KeyAuth_Register`, `KeyAuth_License`, `KeyAuth_Enable2FA` | [CONFIRMADO] | KeyAuth é a base de login/licença |
| Credenciais nativas | `Honest.native.auth_native.dll` (3,47 MB) | [CONFIRMADO] | Camada nativa de auth; função exata [LACUNA] |
| Textos | não recuperados | [LACUNA] | `#US` cifrado |

`[INFERIDO]` A tela tem modo de login por usuário/senha **e** por licença (key), com opção de "web login" (device code / navegador), porque os quatro métodos existem no KeyAuth e todos são referenciados.

## 3. Shell de navegação

| item | origem | confiança | observações |
|---|---|---|---|
| Logo no topo | `LogoPic.Image` (blob `006`) | [CONFIRMADO] | — |
| Itens de navegação | `Nav.IconGames`, `Nav.IconInstallers` (blob `006`) + 52 ícones de UI | [CONFIRMADO] | Nomes indicam propriedades `Icon*` por destino |
| Itens visíveis no menu | `home, tweaks, gpu, download, gamepad, guides, bios, assistant, camera, usb, user, settings, chat, power` | [CONFIRMADO] | Conjunto dos ícones de navegação no manifesto |
| Controles de janela | `minimize`, `maximize`, `restore`, `close` | [CONFIRMADO] | Chrome customizado |
| Transições | blob `011` (`animation1`) | [CONFIRMADO] | Animação de troca de página |
| Dimensões da janela | — | [LACUNA] | Não há coordenadas recuperáveis sem IL |

## 4. Home

| item | origem | confiança | observações |
|---|---|---|---|
| Card "Driver" | `Fps.HomePhotos.driver.jpg` (26.804 B) | [CONFIRMADO] | Leva a drivers/instaladores |
| Card "IA" | `Fps.HomePhotos.ai.jpg` (73.548 B) | [CONFIRMADO] | Leva ao Assistente |
| Card "Jogos" | `Fps.HomePhotos.games.jpg` (40.576 B) | [CONFIRMADO] | Leva à biblioteca de jogos |
| Imagens adicionais | blob `014`: `Pic10`, `Pic3`, `pictureBox1/3/5/7/13` | [CONFIRMADO] | 7 imagens — provável home com banners |
| Ícone "Jogar" | `play` ("Jogar") | [CONFIRMADO] | Ação de iniciar jogo/perfil |
| Estado do PC | `SavedPcProfile`, `RecommendedProfile`, `HasDetails` | [CONFIRMADO] | Home pode exibir resumo do hardware |

`[INFERIDO]` A Home é um hub com três entradas principais (Driver, IA, Jogos) e um resumo do perfil do PC; a ordem exata dos cards não é recuperável.

## 5. Jogos

| item | origem | confiança | observações |
|---|---|---|---|
| Card EA FC | `cardEafc.BackgroundImage` (blob `009`) | [CONFIRMADO] | Imagem JPEG embutida |
| Card Fortnite | `cardFortnite.BackgroundImage` | [CONFIRMADO] | — |
| Card Rust | `cardRust.BackgroundImage` | [CONFIRMADO] | — |
| Card Warzone | `cardWarzone.BackgroundImage` | [CONFIRMADO] | — |
| Jogos adicionais | logos `logo_valorant`, `logo_leagueoflegends`, `logo_fivem`, `logo_minecraft`, `logo_faceit`, `logo_gamersclub`, `logo_medaltv`, `logo_steam`, `logo_epicgames`, `logo_eaapp`, `logo_ubisoftconnect`, `logo_rockstar` | [CONFIRMADO] | Logo não prova tela, mas comprova catálogo |
| Modelo de jogo | `IsCustomGame`, `CustomPath`, `SteamAppId`, `EpicAppName`, `ExecutableName`, `KnownPaths` | [CONFIRMADO] | Suporta Steam, Epic e executável manual |
| Ações por jogo | `play` ("Jogar"), `star` ("Favoritar"), `image` ("Imagem / capa"), `link` ("Vincular") | [CONFIRMADO] | Favoritar, capa e vínculo de executável |
| Otimização por jogo | `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled`, `CpuPriority`, `IoPriority`, `SelectedId` | [CONFIRMADO] | Preferências de GPU/CPU/IO por jogo |
| Modo de jogo | `GameModeLogo`, `logo_gamersclub` | [CONFIRMADO] | — |

`[INFERIDO]` A tela combina biblioteca (cards com capa) + painel de detalhe por jogo, com botão Jogar, favorito e ajustes de desempenho.

## 6. Placa de vídeo (GPU)

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone de seção | `gpu` / `nav_gpu` | [CONFIRMADO] | — |
| Base de identificação | `Honest.Nvidia.PciIds` (112.604 B) | [CONFIRMADO] | Vendor/device IDs NVIDIA offline |
| GPU-Z | `logo_gpuz` | [CONFIRMADO] | Ferramenta listada |
| DDU | `logo_ddu` | [CONFIRMADO] | Remoção de driver |
| MSI Afterburner | `logo_msiafterburner` | [CONFIRMADO] | Overclock/monitoramento |
| NVIDIA CleanStall | `logo_nvcleanstall` | [CONFIRMADO] | Limpeza de driver NVIDIA |
| ZenTimings | `logo_zentimings` | [CONFIRMADO] | Timings de memória (AMD) |
| RadeonSlimmer | `logo_radeonslimmer` | [CONFIRMADO] | Enxugar driver AMD |
| AMD Chipset | `logo_amdchipset` | [CONFIRMADO] | Driver de chipset |
| "Baixar" | `download` ("Baixar") | [CONFIRMADO] | Ação de download |
| Dados exibidos | — | [LACUNA] | Nenhum nome de contador/counter recuperado |

`[INFERIDO]` A página identifica a GPU por PCI ID e oferece pacotes de driver/utilitários; telemetria ao vivo (temperatura, uso) não pôde ser comprovada (ver `05_metricas_instaladores_jogos.md`).

## 7. Processador (CPU)

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone de seção | `cpu` | [CONFIRMADO] | — |
| Logo de fabricante | `IconIntel.Image` (blob `008`) | [CONFIRMADO] | Página com marca Intel (provável também AMD por texto) |
| Ajuste de CPU | `autocpuset_icon`, `GetSystemCpuSetInformation`, `SetProcessDefaultCpuSets`, `SetThreadSelectedCpuSets`, `SetProcessAffinityMask`, `SetPriorityClass` | [CONFIRMADO] | CPU Sets, afinidade e prioridade |
| Prioridade/IO | `CpuPriority`, `IoPriority`, `CpuIoEnabled` | [CONFIRMADO] | Configuração por jogo/processo |
| Ferramentas | `logo_cpuz`, `logo_occt`, `logo_prime95`, `logo_geek`, `logo_ocat`, `logo_testmem5`, `logo_hwinfo`, `logo_hwmonitor` | [CONFIRMADO] | Benchmarks e testes de estabilidade |
| Dados exibidos | — | [LACUNA] | Sem strings de contadores |

## 8. Armazenamento, Localização e Mouse

| tela | origem | confiança | observações |
|---|---|---|---|
| Armazenamento | `hdd` ("Armazenamento") | [CONFIRMADO] | Ícone dedicado no menu |
| Localização | `location` ("Localização") + bitmap `localizacao` | [CONFIRMADO] | Provável seleção de país/região |
| Mouse | `mouse` ("Mouse") + bitmap `mouse` | [CONFIRMADO] | Ajustes de mouse |
| Conteúdo detalhado | — | [LACUNA] | Sem rótulos recuperáveis |

`[INFERIDO]` São páginas de configuração de hardware/região, provavelmente usadas para recomendar drivers e ajustes regionais.

## 9. Ajustes / Tweaks

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `tweaks` / `nav_tweaks` | [CONFIRMADO] | — |
| Grupo 1 — Notificações | `$settings.notifications` | [CONFIRMADO] | `ToastEnabled`, `NOC_GLOBAL_SETTING_TOASTS_ENABLED`, `DisableNotificationCenter`, `NoToastApplicationNotification`, `ALLOW_NOTIFICATION_SOUND` |
| Grupo 2 — Sugestões | `$settings.suggestions` | [CONFIRMADO] | 8 valores `SubscribedContent-*`, `SilentInstalledAppsEnabled`, `SystemPaneSuggestionsEnabled`, `SoftLandingEnabled`, `ScoobeSystemSettingEnabled`, `DisableWindowsConsumerFeatures` |
| Grupo 3 — Widgets | `$settings.widgets` | [CONFIRMADO] | `TaskbarDa`, `ShellFeedsTaskbarViewMode`, `Dsh\AllowNewsAndInterests`, `EnableFeeds` |
| Grupo 4 — Destaques da busca | `$settings.searchHighlights` | [CONFIRMADO] | `IsDynamicSearchBoxEnabled`, `EnableDynamicContentInWSB` |
| Grupo 5 — Publicidade | `$settings.advertising` | [CONFIRMADO] | `AdvertisingInfo\Enabled`, `DisabledByGroupPolicy` |
| Grupo 6 — Experiências sob medida | `$settings.tailored` | [CONFIRMADO] | `TailoredExperiencesWithDiagnosticDataEnabled`, `DisableTailoredExperiences...` |
| Grupo 7 — Diagnóstico/telemetria | `$settings.diagnostics` | [CONFIRMADO] | `AllowTelemetry = 1` (mínimo, não desligado) |
| Grupo 8 — Histórico de atividades | `$settings.activityHistory` | [CONFIRMADO] | `EnableActivityFeed`, `PublishUserActivities`, `UploadUserActivities` |
| Grupo 9 — Game DVR | `$settings.gameDvr` | [CONFIRMADO] | `AppCaptureEnabled`, `HistoricalCaptureEnabled`, `GameDVR_Enabled`, `AllowGameDVR` |
| Grupo 10 — UAC | `$settings.uac` | [CONFIRMADO] | `EnableLUA = 0` |
| Grupo 11 — OneDrive | `$settings.oneDrive` | [CONFIRMADO] | `DisableFileSyncNGSC = 1` + remoção |
| Grupo 12 — Extras | `$settings.extras` | [CONFIRMADO] | Lista de apps via winget |
| Controles | `Guna2ToggleSwitch`, `Guna2CheckBox`, `Guna2TrackBar` | [CONFIRMADO] | Toggles e sliders |
| Aplicação | `ApplyPreference`, `SetValue`, `Set-ItemProperty`, `reg add` | [CONFIRMADO] | Aplicação com tratamento de erro por chave |
| Log/resultado | `Stage()`, `$warnings`, `$result.ok` | [CONFIRMADO] | Progresso e avisos |

## 10. Instaladores / Downloads

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `download` ("Baixar") + nav `IconInstallers` | [CONFIRMADO] | — |
| Logos confirmados | `logo_7zip, logo_autorun, logo_brave, logo_cpuz, logo_directx, logo_driverstools, logo_epicgames, logo_faceit, logo_gamersclub, logo_googlechrome, logo_hwinfo, logo_internetdriver, logo_latencymon, logo_logitechghub, logo_microsoftedge, logo_msiafterburner, logo_nvcleanstall, logo_ocat, logo_openshell, logo_processexplorer, logo_radeonslimmer, logo_rockstar, logo_startallback, logo_teamspeak, logo_ubisoftconnect, logo_valorant, logo_visualstudio, logo_vscode, logo_zentimings, logo_ddu, logo_prime95, logo_geek, logo_occt, logo_gpuz, logo_kmsactivator` | [CONFIRMADO] | 35 nomes de bitmap recuperados diretamente |
| Logos prováveis | `vlcplayer, spotify, everything, revouninstaller, notepad, qbittorrent, obsstudio, capframex, hwmonitor, testmem5, amdchipset, steam, eaapp, minecraft, fivem, leagueoflegends, vcredist, bluetoothdriver, unlocker` | [INFERIDO] | Presentes na lista de 85 bitmaps, mas o nome exato não foi recuperado; categorias citadas em strings/ícones |
| Métodos de instalação | `Start-Process -ArgumentList '/uninstall' -WindowStyle Hidden`, `Start-Process ... -WindowStyle Hidden`, `winget install --id ... --silent` | [CONFIRMADO] | Instalação silenciosa; desinstalação com `/uninstall` |
| Allowlist winget | `steam→Valve.Steam`, `discord→Discord.Discord`, `7zip→7zip.7zip`, `chrome→Google.Chrome`, `vlc→VideoLAN.VLC` | [CONFIRMADO] | `PostInstall.ps1` |
| Download | `WebClient.DownloadFileAsync`, `HttpClient`, `DownloadFileCompleted` | [CONFIRMADO] | Progresso por evento |
| Fila/tarefas | `pendingtasks`, `scheduled-tasks` | [CONFIRMADO] | Fila de instalação |
| Status por item | `Stage()`, `$failed`, `$warnings` | [CONFIRMADO] | — |

## 11. Guias / Como usar

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `guides` ("Como usar") | [CONFIRMADO] | — |
| Imagem grande de guia | bitmap 1672×941 (1.725.389 B) em `resources_imgs` | [CONFIRMADO] | Tamanho típico de screenshot/tutorial |
| Navegação | `back`, `next`, `expand`, `collapse` | [CONFIRMADO] | Passo a passo |
| Conteúdo textual | — | [LACUNA] | Textos cifrados |
| Vinculado à BIOS | `show_bios`, `nav_bios` | [CONFIRMADO] | Guia de BIOS provavelmente dentro de Guias |

## 12. BIOS

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `bios` ("BIOS") | [CONFIRMADO] | — |
| Bitmap | `show_bios` | [CONFIRMADO] | — |
| Conteúdo | instruções de configuração (XMP/EXPO, boot, VT-x/SVM) | [INFERIDO] | Nenhum texto recuperável; inferido do contexto de otimização |

## 13. Assistente (chat)

| item | origem | confiança | observações |
|---|---|---|---|
| Avatar | `assistant_avatar.png` | [CONFIRMADO] | — |
| Botão enviar | `assistant_send.png` / ícone `send` ("Enviar") | [CONFIRMADO] | — |
| Namespace | `Fps.Assistant` | [CONFIRMADO] | Módulo separado |
| Modelo de conversa | `Conversations`, `Messages`, `Draft`, `Role`, `Text`, `CreatedUtc`, `IncludeInContext`, `ShareWithAi`, `HumanMode`, `LastPollTimestamp` | [CONFIRMADO] | Histórico, rascunho, modo humano e compartilhamento de contexto |
| Transporte | `KeyAuth_ChatSend`, `KeyAuth_ChatGet`, `KeyAuth_FetchOnline` | [CONFIRMADO] | Chat roteado pelo backend KeyAuth |
| Anexos | `image` ("Imagem / capa"), `link` ("Vincular") | [CONFIRMADO] | Envio de imagem/links |
| Backend | `https://honest.emulationzone2026.workers.dev` | [CONFIRMADO] | Cloudflare Worker |
| Modelo de IA usado | — | [LACUNA] | Sem string de provedor/modelo; `OpenAI` no binário é nome de CA, não evidência |

`[INFERIDO]` Há dois modos: "IA" e "humano" (`HumanMode`), com suporte a incluir o perfil do PC no contexto (`IncludeInContext`) e a compartilhar com a IA (`ShareWithAi`).

## 14. Capturas

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `camera` ("Capturas") | [CONFIRMADO] | — |
| Modos | `capture-region` ("Selecionar região"), `window` ("Janela"), `monitor` ("Tela inteira") | [CONFIRMADO] | Três modos de captura |
| Anotação | `pencil` ("Caneta / editar"), `rectangle` ("Retângulo"), `annotation-arrow` ("Seta de anotação"), `text` ("Texto"), `undo` ("Desfazer") | [CONFIRMADO] | Editor embutido |
| Ações | `save` ("Salvar"), `copy` ("Copiar"), `check` ("Confirmar"), `close` ("Fechar") | [CONFIRMADO] | Salvar/copiar/confirmar |
| Som | `Fps.Captures.capture-shutter.wav` (21.656 B) | [CONFIRMADO] | Feedback sonoro do obturador |
| Captura de tela | `BitBlt`, `GetDC`, `ReleaseDC`, `CopyFromScreen` | [CONFIRMADO] | P/Invoke presente |
| Hotkeys | `RegisterHotKey`, `UnregisterHotKey`, `SetWindowsHookEx`, `GetAsyncKeyState` | [CONFIRMADO] | Atalhos globais (tecla exata [LACUNA]) |
| Render/anotação | SkiaSharp (`SKCanvas`, `SKBitmap`) | [CONFIRMADO] | Desenho das anotações |
| Salvamento | `SaveFileDialog`/`Clipboard` | [INFERIDO] | Não recuperado por nome |

## 15. Pendrive / Windows Media

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `usb` ("Pendrive", Tabler `device-usb`) | [CONFIRMADO] | — |
| Ações | `action = 'disks'` (listar discos) e `action = 'build'` | [CONFIRMADO] | `BuildMedia.ps1` |
| Destinos | `mode = 'iso'` ou `'usb'` | [CONFIRMADO] | — |
| Entrada | ISO/WIM/ESD + índice da edição | [CONFIRMADO] | `Get-WindowsImage`, `$request.index` |
| Seleção de apps/capabilities | `AnalyzeImage.ps1` → lista `appx:*`, `capability:*`, `Desktop` | [CONFIRMADO] | Itens `Desktop` não removíveis |
| Drivers | `$request.drivers` → `Add-WindowsDriver -Recurse` | [CONFIRMADO] | Pasta de drivers do host |
| Tweaks embutidos | `$request.recipe` → `Honest-config.json` | [CONFIRMADO] | Mesmos 12 grupos dos Ajustes |
| Pós-instalação | `postScript` → `sources\$OEM$\$1\HonestSetup\Configure.ps1` | [CONFIRMADO] | Executa no primeiro logon |
| Sistemas de arquivos | FAT32 (split 3800 MB) ou NTFS + UEFI:NTFS | [CONFIRMADO] | `usbFileSystem` |
| Segurança de disco | `SafeUsb`, `UsbRecord`, `Clear-Disk`, confirmação de letra | [CONFIRMADO] | Evita apagar disco errado |
| Progresso | `Stage()` + `IProgress` | [CONFIRMADO] | — |

## 16. Perfil

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `user` ("Perfil") | [CONFIRMADO] | — |
| Conta | `AccountKey`, `KeyAuth_GetUserDataJson`, `KeyAuth_ChangeUsername` | [CONFIRMADO] | Dados do usuário KeyAuth |
| Perfil do PC | `SavedPcProfile`, `RecommendedProfile`, `HasCustomSettings`, `HasDetails` | [CONFIRMADO] | Perfil salvo + recomendado |
| Campos | `DeviceType`, `Memory`, `Motherboard`, `PowerSupply`, `Case`, `Cooler`, `Storage`, `Display`, `Country`, `NotebookModel` | [CONFIRMADO] | Ficha completa do hardware |
| Identificação | `fingerprint` ("Identificação") | [CONFIRMADO] | HWID exibido/gerenciado |
| Ações | `undo` ("Desfazer"), `save` ("Salvar"), `check` ("Confirmar") | [CONFIRMADO] | Editar/salvar perfil |

## 17. Configurações

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `settings` ("Configurações") | [CONFIRMADO] | — |
| Idioma | `T(en, pt, es)` em todos os scripts + `Lang` em `MediaTransfer`/`IsoImage`/`OfflineHive` | [CONFIRMADO] | EN / PT / ES |
| Backend social | `Honest.social-endpoint.json`: `apiUrl` + `previousApiUrl` + `previousApiUrls` | [CONFIRMADO] | Migração de endpoint |
| Conta | `KeyAuth_Logout`, `KeyAuth_Enable2FA`, `KeyAuth_Disable2FA`, `KeyAuth_GetTwoFactorSecret` | [CONFIRMADO] | Segurança da conta |
| Atualização | `KeyAuth_Check`, `get_Version`, `FileVersionInfo` | [CONFIRMADO] | Verificação de versão |
| Integridade | `VerifyAssemblyIntegrity` | [CONFIRMADO] | Autoverificação |
| Ícone de vínculo | `link` ("Vincular") | [CONFIRMADO] | Vincular Discord/conta |
| Ícone de chat | `chat` ("Conversas") | [CONFIRMADO] | Acesso ao chat |
| Tema/cores | — | [LACUNA] | Sem evidência de seletor de tema |

## 18. Notificações e Tarefas agendadas

| tela | origem | confiança | observações |
|---|---|---|---|
| Notificações | `notifications`/`notif_icon` | [CONFIRMADO] | Página dedicada com toggles do grupo `notifications` |
| Tarefas agendadas | `scheduled-tasks`/`pendingtasks` | [CONFIRMADO] | Ícone `list-todo` no manifesto |
| Implementação de agendamento | — | [LACUNA] | Não há `schtasks`/`Register-ScheduledTask` nas strings; "tarefas" provavelmente = fila de downloads/instalações pendentes |

## 19. Proteção

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `protection` ("Proteção") + bitmap `protecao` | [CONFIRMADO] | Ícone `shield-check` no manifesto |
| Conteúdo | status de segurança (Defender/firewall) | [INFERIDO] | Sem strings de API de segurança; `SecHealthUI` aparece apenas na lista de apps protegidos |
| Ações | — | [LACUNA] | — |

## 20. Sair

| item | origem | confiança | observações |
|---|---|---|---|
| Ícone | `power` ("Sair") | [CONFIRMADO] | Logout da conta |
| Chamada | `KeyAuth_Logout` | [CONFIRMADO] | Encerra sessão |

---

## 21. Mapa de navegação (inferido)

```
Login
  └─ Shell (nav lateral)
       ├─ Home ──── card Driver ──► Instaladores
       │            card IA ─────► Assistente
       │            card Jogos ──► Jogos
       ├─ Ajustes (12 grupos de tweaks)
       ├─ Placa de vídeo ──► downloads/ferramentas GPU
       ├─ Processador ────► ferramentas/benchmarks CPU
       ├─ Armazenamento
       ├─ Localização
       ├─ Mouse
       ├─ Instaladores ───► fila de downloads
       ├─ Jogos ──────────► detalhe do jogo ──► Jogar
       ├─ Guias / BIOS
       ├─ Assistente (IA / humano)
       ├─ Capturas (região/janela/tela + anotação)
       ├─ Pendrive (ISO/USB)
       ├─ Perfil (conta + hardware)
       ├─ Configurações (idioma, conta, versão)
       ├─ Notificações
       ├─ Tarefas agendadas
       ├─ Proteção
       └─ Sair
```

`[INFERIDO]` O encadeamento acima é compatível com todos os ícones, recursos e modelos encontrados, mas a ordem real dos itens do menu não é recuperável sem o IL.

## 22. Lacunas desta fase

| item | confiança | observações |
|---|---|---|
| Rótulos e textos de cada tela | [LACUNA] | `#US` cifrado |
| Ordem e agrupamento exatos do menu | [LACUNA] | Depende de IL |
| Layout (coordenadas, tamanhos, cores exatas) | [LACUNA] | Sem `.resx` com posições; Guna define em código |
| Tela de licença/ativação separada do login | [LACUNA] | KeyAuth unifica, mas pode haver etapa extra |
| Tela de atualização obrigatória | [LACUNA] | Só há `KeyAuth_Check` e `get_Version` |

**Arquivo gravado:** `C:\re\geandre_v3\docs\03_telas.md`
