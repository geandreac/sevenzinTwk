# 05 — Métricas, instaladores e jogos

**Alvo:** `C:\re\geandre_v3\geandre.exe` (SHA-256 `70DC5163…AFCB72`)
**Catálogo legível por máquina:** `C:\re\geandre_v3\docs\metricas.json` (38 entradas: 13 `[CONFIRMADO]`, 13 `[INFERIDO]`, 12 `[LACUNA]`)

> **Regra de honestidade desta fase:** números de hardware, taxas, temperaturas e tamanhos de download **não foram inventados**. Quando não existe API, campo de modelo ou arquivo que prove a coleta, a linha é `[LACUNA]`. Onde o campo existe mas o método de coleta não foi recuperado, a linha é `[INFERIDO]` com a evidência citada.
>
> **Método:** leitura estática do PE, do heap `#Strings` (nomes de tipos/membros preservados), dos 142 recursos gerenciados e das strings ASCII/UTF-16. O binário **não foi executado**.

Legenda: `[CONFIRMADO]` = lido do arquivo/recurso; `[INFERIDO]` = deduzido com evidência; `[LACUNA]` = indeterminado.

---

## 1. Perfil estático do PC

Modelo de dados: `Honest`/`Fps` com `SavedPcProfile`, `RecommendedProfile`, `SelectedId`, `AccountKey`, `HasDetails`, `HasCustomSettings`, `UpdatedUtc` — todos `[CONFIRMADO]` por nome de membro no heap.

| item | origem | confiança | observações |
|---|---|---|---|
| Estado do perfil | `SavedPcProfile`, `RecommendedProfile`, `HasCustomSettings`, `HasDetails`, `SelectedId`, `UpdatedUtc` | [CONFIRMADO] | Permite comparar "meu PC" com "PC recomendado" e marcar perfil editado à mão |
| Tipo de dispositivo | `SavedPcProfile.DeviceType` | [INFERIDO] | Campo existe; detecção por chassi/bateria não recuperada |
| Modelo do notebook | `SavedPcProfile.NotebookModel` | [INFERIDO] | Campo separado de `Motherboard`, típico de portáteis |
| Placa-mãe | `SavedPcProfile.Motherboard` | [INFERIDO] | Nenhuma string `Win32_*` em claro; provedor real [LACUNA] |
| Memória RAM | `SavedPcProfile.Memory` + `GlobalMemoryStatusEx` | [INFERIDO] | API e campo confirmados; conversão/exibição não comprovadas |
| Armazenamento | `SavedPcProfile.Storage` + `IO.DriveInfo` | [INFERIDO] | `DriveInfo` comprovado no script de mídia; uso no perfil inferido |
| Monitor | `SavedPcProfile.Display` + `EnumDisplayDevices` | [INFERIDO] | Resolução/Hz podem vir de DEVMODE (seção 2) |
| País/região | `SavedPcProfile.Country`, `TwoLetterISOLanguageName` | [INFERIDO] | Mesma cultura usada no seletor en/pt/es |
| Fonte (PSU) | `SavedPcProfile.PowerSupply` | [INFERIDO] | Inexistente como sensor; campo é manual |
| Gabinete | `SavedPcProfile.Case` | [INFERIDO] | Manual |
| Cooler | `SavedPcProfile.Cooler` | [INFERIDO] | Manual; não implica leitura de RPM |
| GPU — identificação | `Honest.Nvidia.PciIds` (112.604 B) | [CONFIRMADO] | Tabela `vendor device` → nome comercial; comentário no recurso: `# NVIDIA vendor section bundled for offline GPU identification.` |
| CPU — identificação | ícone `IconIntel.Image` + API de CPU Sets | [INFERIDO] | Não há campo `Cpu` de perfil comprovado (`<Cpu>k__BackingField` existe, mas o tipo dono é [LACUNA]) |
| Propriedades de hardware `<Cpu>` / `<Gpu>` | backing fields `k__BackingField` | [CONFIRMADO] | Existem propriedades `Cpu` e `Gpu` em tipos do assembly; qual entidade as expõe [LACUNA] |

`[INFERIDO]` O perfil é a fonte única de verdade: alimenta a Home, a recomendação de ajustes e o `Honest-config.json` da mídia. Evidência: os mesmos nomes de campo aparecem nas telas (`DeviceType`→"notebook/desktop") e no contexto do assistente (`IncludeInContext`).

## 2. Enumeração de vídeo (o que é medido de verdade)

| item | origem | confiança | observações |
|---|---|---|---|
| Enumeração de modos | `EnumDisplaySettingsW`, `EnumDisplayDevices` | [CONFIRMADO] | P/Invoke no heap `#Strings` |
| Estrutura DEVMODE | `dmDeviceName`, `dmSpecVersion`, `dmDriverVersion`, `dmSize`, `dmDriverExtra`, `dmFields`, `dmPositionX/Y`, `dmDisplayOrientation`, `dmDisplayFixedOutput`, `dmColor`, `dmDuplex` | [CONFIRMADO] | Struct completa declarada no assembly |
| Resolução | `dmPelsWidth`, `dmYResolution`, `dmBitsPerPel` | [CONFIRMADO] | Campos presentes; valor exibido [INFERIDO] |
| Taxa de atualização | `dmDisplayFrequency` | [CONFIRMADO] | **Enumeração de Hz confirmada**; não é medição de FPS |
| Geometria de monitor | `MonitorFromPoint`, `MonitorFromWindow`, `GetMonitorInfo`, `GetSystemMetrics` | [CONFIRMADO] | Usado também na captura de tela (região/monitor) |
| DPI por monitor | `SetThreadDpiAwarenessContext` | [CONFIRMADO] | Correção de escala na captura |

`[INFERIDO]` A tela de vídeo/monitor lista os modos suportados (resolução × Hz) e deixa o usuário escolher; é leitura de configuração do Windows, não telemetria de jogo.

## 3. Métricas dinâmicas

| item | origem | confiança | observações |
|---|---|---|---|
| Memória física/uso | `GlobalMemoryStatusEx` | [CONFIRMADO] (API) / [INFERIDO] (exibição) | `MEMORYSTATUSEX` traz total, livre e carga de memória |
| Espaço livre em disco | `IO.DriveInfo.AvailableFreeSpace` | [CONFIRMADO] | Usado como pré-requisito na criação de ISO/USB (`BuildMedia.ps1`: `$drive.AvailableFreeSpace -lt $required`) |
| Lixeira | `SHQueryRecycleBin`, `SHEmptyRecycleBin` | [CONFIRMADO] | Consulta tamanho/itens e permite esvaziar |
| Tempo de CPU por thread | `GetThreadTimes`, `OpenThread`, `Thread32First/Next`, `NtQueryInformationThread` | [CONFIRMADO] (API) | Permite calcular CPU% de um processo específico; fazer isso na UI [INFERIDO] |
| Uso de CPU do sistema | `PerformanceCounter` (tipo presente) | [LACUNA] | Nenhuma categoria/contador nomeado recuperado |
| Clock da CPU | — | [LACUNA] | Sem `PDH`, `CallNtPowerInformation` ou leitura de MSR |
| Temperatura da CPU | — | [LACUNA] | Sem `MSAcpi_ThermalZoneTemperature`, WinRing0, LibreHardwareMonitor |
| Uso da GPU | — | [LACUNA] | Sem `GPU Engine` (PDH), NVML, NVAPI, DXGI, D3DKMT |
| Temperatura / clock da GPU | — | [LACUNA] | Sem NVAPI/NVML/ADL/IGCL |
| VRAM | — | [LACUNA] | Sem campo no modelo; capacidade só aparece embutida no nome comercial da tabela PCI |
| Rotação de fans | — | [LACUNA] | Sem EC/SMBus |
| I/O de disco | — | [LACUNA] | App ajusta prioridade de I/O, mas não mede throughput |
| Rede | — | [LACUNA] | Sem `NetworkInterface`/`GetAdaptersInfo`; progresso de download é por byte, não por taxa de link |
| FPS / frametime | — | [LACUNA] | **`Fps` é apenas `ProductName`.** Sem PresentMon, ETW, hook de `Present`, DXGI ou overlay de terceiros |
| Uptime | — | [LACUNA] | Sem `GetTickCount64` em claro |

`[INFERIDO]` Os "medidores" da Home são, no máximo, memória + espaço em disco + lixeira + estado do perfil. Qualquer número de FPS, temperatura ou uso de GPU exibido por uma reimplementação **teria de ser novo**, não copiado deste binário.

## 4. Controles de desempenho por processo

Todos confirmados por P/Invoke no heap `#Strings` (detalhe em `04_funcionalidades.md`, seção 8).

| controle | origem | confiança | observações |
|---|---|---|---|
| CPU Sets | `GetSystemCpuSetInformation`, `Get/SetProcessDefaultCpuSets`, `Get/SetThreadSelectedCpuSets` | [CONFIRMADO] | Base para fixar threads em CCDs/P-cores |
| Afinidade | `Get/SetProcessAffinityMask` | [CONFIRMADO] | Máscara por processo |
| Prioridade | `Get/SetPriorityClass`, `Get/SetThreadPriority`, `PriorityClass` | [CONFIRMADO] | Campo `CpuPriority` por jogo |
| Priority boost | `Get/SetProcessPriorityBoost`, `Get/SetThreadPriorityBoost` | [CONFIRMADO] | Ajuste fino de escalonamento |
| Prioridade de I/O | propriedade `IoPriority`, flag `CpuIoEnabled` | [CONFIRMADO] (propriedade) | Chamada nativa usada [LACUNA] |
| Working set | `SetProcessWorkingSetSize` | [CONFIRMADO] | Reduz memória residente |
| Excluir núcleo 0 | `excludeCore0` | [CONFIRMADO] | Nome de membro no assembly |
| Seleção de P-cores | `pCores` | [CONFIRMADO] | Nome de membro no assembly |
| Resolução de timer | `winmm.dll`, `timeBeginPeriod`, `timeEndPeriod` | [CONFIRMADO] | Ajuste clássico de timer (0,5 ms) para latência |
| Toggles por jogo | `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled` | [CONFIRMADO] | Três eixos independentes |
| Aplicação | handles de processo/thread (`OpenProcess`, `OpenThread`) | [CONFIRMADO] | Efeito só existe enquanto o app está aberto; não há serviço/driver residente |

> **Aviso técnico:** `timeBeginPeriod` é global para o sistema e afeta consumo de energia; `SetProcessWorkingSetSize` com valores `-1` força trim de páginas (pode causar stutter). Não são "ganhos garantidos".

## 5. Números que a UI realmente pode mostrar

| número | origem | confiança | observações |
|---|---|---|---|
| Dias restantes de licença | `KeyAuth_ExpiryDaysLeft` | [CONFIRMADO] | Vem do backend |
| Versão do app | `FileVersionInfo`, `get_Version`, `FileVersion 2.5.1.0` | [CONFIRMADO] | `ProductVersion 2.5.1`; string de build `Version: 2026.10.07` |
| Usuários online / estatísticas | `KeyAuth_FetchOnline`, `KeyAuth_FetchStats` | [CONFIRMADO] | Dado do servidor |
| Progresso de download | `DownloadProgressChanged`, `DownloadFileCompleted` | [CONFIRMADO] | Percentual/bytes por evento do `WebClient` |
| Progresso de cópia de mídia | `MediaTransfer.cs` (blocos de 1 MB, relatório a cada 200 ms) | [CONFIRMADO] | Bytes copiados + verificação SHA-256 |
| Espaço necessário para mídia | fórmula em `BuildMedia.ps1` | [CONFIRMADO] | Ver seção 11 |
| HWID | `fingerprint` ("Identificação"), `AccountKey` | [INFERIDO] | Composição [LACUNA] |

## 6. Catálogo de instaladores — logos confirmados

35 bitmaps com nome recuperado diretamente + ícone `ic_discord` + ícone `Honest.UI.Icons.steam.png`.

| # | bitmap (nome confirmado) | produto provável | confiança do nome | confiança do produto |
|---|---|---|---|---|
| 1 | `logo_7zip` | 7-Zip | [CONFIRMADO] | [INFERIDO] |
| 2 | `logo_autorun` | Sysinternals Autoruns | [CONFIRMADO] | [INFERIDO] |
| 3 | `logo_brave` | Brave Browser | [CONFIRMADO] | [INFERIDO] |
| 4 | `logo_cpuz` | CPU-Z | [CONFIRMADO] | [INFERIDO] |
| 5 | `logo_directx` | DirectX End-User Runtime | [CONFIRMADO] | [INFERIDO] |
| 6 | `logo_driverstools` | DriverStore Explorer / Drivers Tools | [CONFIRMADO] | [INFERIDO] |
| 7 | `logo_epicgames` | Epic Games Launcher | [CONFIRMADO] | [INFERIDO] |
| 8 | `logo_faceit` | FACEIT Anti-Cheat | [CONFIRMADO] | [INFERIDO] |
| 9 | `logo_gamersclub` | Gamers Club | [CONFIRMADO] | [INFERIDO] |
| 10 | `logo_googlechrome` | Google Chrome | [CONFIRMADO] | [INFERIDO] |
| 11 | `logo_hwinfo` | HWiNFO | [CONFIRMADO] | [INFERIDO] |
| 12 | `logo_internetdriver` | Driver de rede/Wi-Fi | [CONFIRMADO] | [INFERIDO] |
| 13 | `logo_latencymon` | LatencyMon | [CONFIRMADO] | [INFERIDO] |
| 14 | `logo_logitechghub` | Logitech G HUB | [CONFIRMADO] | [INFERIDO] |
| 15 | `logo_microsoftedge` | Microsoft Edge | [CONFIRMADO] | [INFERIDO] |
| 16 | `logo_msiafterburner` | MSI Afterburner | [CONFIRMADO] | [INFERIDO] |
| 17 | `logo_nvcleanstall` | NVCleanstall | [CONFIRMADO] | [INFERIDO] |
| 18 | `logo_ocat` | OCAT | [CONFIRMADO] | [INFERIDO] |
| 19 | `logo_openshell` | Open-Shell | [CONFIRMADO] | [INFERIDO] |
| 20 | `logo_processexplorer` | Process Explorer | [CONFIRMADO] | [INFERIDO] |
| 21 | `logo_radeonslimmer` | Radeon Slimmer | [CONFIRMADO] | [INFERIDO] |
| 22 | `logo_rockstar` | Rockstar Games Launcher | [CONFIRMADO] | [INFERIDO] |
| 23 | `logo_startallback` | StartAllBack | [CONFIRMADO] | [INFERIDO] |
| 24 | `logo_teamspeak` | TeamSpeak | [CONFIRMADO] | [INFERIDO] |
| 25 | `logo_ubisoftconnect` | Ubisoft Connect | [CONFIRMADO] | [INFERIDO] |
| 26 | `logo_valorant` | VALORANT | [CONFIRMADO] | [INFERIDO] |
| 27 | `logo_visualstudio` | Visual Studio | [CONFIRMADO] | [INFERIDO] |
| 28 | `logo_vscode` | Visual Studio Code | [CONFIRMADO] | [INFERIDO] |
| 29 | `logo_zentimings` | ZenTimings | [CONFIRMADO] | [INFERIDO] |
| 30 | `logo_ddu` | Display Driver Uninstaller | [CONFIRMADO] | [INFERIDO] |
| 31 | `logo_prime95` | Prime95 | [CONFIRMADO] | [INFERIDO] |
| 32 | `logo_geek` | Geekbench | [CONFIRMADO] | [INFERIDO] |
| 33 | `logo_occt` | OCCT | [CONFIRMADO] | [INFERIDO] |
| 34 | `logo_gpuz` | GPU-Z | [CONFIRMADO] | [INFERIDO] |
| 35 | `logo_kmsactivator` | KMS Activator | [CONFIRMADO] | [INFERIDO] |
| 36 | `ic_discord` | Discord | [CONFIRMADO] | [INFERIDO] |
| 37 | `Honest.UI.Icons.steam.png` | Steam | [CONFIRMADO] | [CONFIRMADO] (nome contém "steam") |

**Prováveis (nome do bitmap não recuperado):** `vlcplayer, spotify, everything, revouninstaller, notepad, qbittorrent, obsstudio, capframex, hwmonitor, testmem5, amdchipset, steam, eaapp, minecraft, fivem, leagueoflegends, vcredist, bluetoothdriver, unlocker` — `[INFERIDO]`, presentes na região de 85 nomes, mas só 25 nomes completos foram decodificados.

**Outros nomes de bitmap confirmados que não são instaladores:** `GameModeLogo, nav_home, nav_gpu, nav_bios8, nav_tweaks, pendingtasks, show_driver, seta, seta_direita, localizacao, protecao, mouse, ic_copy` — `[CONFIRMADO]`.

## 7. Instaladores — como são instalados

| item | origem | confiança | observações |
|---|---|---|---|
| Download HTTP | `WebClient.DownloadFileAsync`, `DownloadFileTaskAsync`, `HttpClient`, `HttpClientHandler` | [CONFIRMADO] | Progresso e conclusão por evento |
| Download autenticado | `KeyAuth_Download`, `KeyAuth_GetDownloadLink` | [CONFIRMADO] | Arquivos liberados por licença |
| Metadados por item | nome, URL, tamanho, hash, argumentos | [LACUNA] | Ficam em `#US` cifrado / constantes ofuscadas |
| Execução | `Start-Process -WindowStyle Hidden`, `-ArgumentList`, `ProcessStartInfo` | [CONFIRMADO] | Padrão usado nos scripts embutidos |
| Flags de silêncio genéricas | `/S`, `/silent`, `/quiet` | [LACUNA] | Não há ocorrência legítima dessas flags nas strings (as que existem são ruído binário) |
| Desinstalação | `Start-Process -ArgumentList '/uninstall'` | [CONFIRMADO] | Usado nos fluxos de Edge/OneDrive |
| winget | `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` | [CONFIRMADO] | Espera até 30 × 3 s no primeiro logon |
| Fila | `pendingtasks`, `scheduled-tasks`, `Stage()`, `$failed`, `$warnings` | [CONFIRMADO] | Fila interna do app; **não** é o Agendador de Tarefas do Windows |
| Persistência da fila | — | [LACUNA] | Nenhum armazenamento identificado |
| Histórico | `Honest.UI.Icons.scheduled-tasks.png` | [CONFIRMADO] | Página "Tarefas agendadas" existe |

## 8. Allowlist do winget (fechada)

Fonte literal: `110_Honest.WindowsMedia.BuildMedia.ps1` / `113_Honest.WindowsMedia.PostInstall.ps1` → `$allowed=@{...}`.

| chave | ID winget | confiança | observações |
|---|---|---|---|
| `steam` | `Valve.Steam` | [CONFIRMADO] | — |
| `discord` | `Discord.Discord` | [CONFIRMADO] | — |
| `7zip` | `7zip.7zip` | [CONFIRMADO] | — |
| `chrome` | `Google.Chrome` | [CONFIRMADO] | — |
| `vlc` | `VideoLAN.VLC` | [CONFIRMADO] | — |
| qualquer outro | — | [CONFIRMADO] | `$failed = $true`; nada fora da lista é instalado |

Comando exato: `winget install --id <ID> --exact --source winget --silent --accept-source-agreements --accept-package-agreements --disable-interactivity --no-upgrade`.
Código de retorno aceito como "já instalado": `-1978335189` `[CONFIRMADO]`.

## 9. Jogos

| item | origem | confiança | observações |
|---|---|---|---|
| Cards fixos na Home | `cardEafc.BackgroundImage`, `cardFortnite.BackgroundImage`, `cardRust.BackgroundImage`, `cardWarzone.BackgroundImage` | [CONFIRMADO] | 4 jogos com arte própria (EA FC, Fortnite, Rust, Warzone) |
| Capas da Home | `Fps.HomePhotos.games.jpg` (40.576 B), `ai.jpg` (73.548 B), `driver.jpg` (26.804 B) | [CONFIRMADO] | Três fotos de seção |
| Jogo Steam | `SteamAppId`, `KnownPaths` | [CONFIRMADO] | Descoberta por biblioteca |
| Jogo Epic | `EpicAppName` | [CONFIRMADO] | Manifestos Epic |
| Jogo custom | `IsCustomGame`, `CustomPath`, `ExecutableName` | [CONFIRMADO] | Executável escolhido à mão |
| Campos de UI por jogo | `image`, `star`, `link` | [CONFIRMADO] | Capa, favorito, URL |
| Início | `play`, `Process.Start` | [CONFIRMADO] | Botão "Jogar" |
| Preset | `SelectedId`, `RecommendedProfile`, `CpuPriority` | [CONFIRMADO] | Perfil aplicado ao iniciar |
| Plataformas com logo | `steam, epicgames, eaapp, ubisoftconnect, rockstar` | [CONFIRMADO] | Bitmaps/ícones |
| Competitivos com logo | `valorant, faceit, gamersclub, medaltv, leagueoflegends, fivem, minecraft` | [CONFIRMADO] | `medaltv`/`leagueoflegends` via lista provável |
| Modo jogo | `GameModeLogo` | [INFERIDO] | Provável indicador de estado, sem API de Game Mode comprovada |
| Detecção automática de partida | — | [LACUNA] | Sem monitor de processo em background comprovado |

## 10. Espaço e tamanho (mídia)

| item | origem | confiança | observações |
|---|---|---|---|
| ISO com servicing | `detail.ImageSize + sourceSize*3 + 2 GB` | [CONFIRMADO] | Fórmula literal em `BuildMedia.ps1` |
| ISO sem servicing | `sourceSize*2 + 2 GB` | [CONFIRMADO] | — |
| USB com servicing | `ImageSize + sourceSize*2 + 2 GB` | [CONFIRMADO] | — |
| USB sem servicing | `sourceSize*1 + 2 GB` | [CONFIRMADO] | — |
| Erro de espaço | `"Not enough temporary space. Reserve approximately N GB on drive X"` | [CONFIRMADO] | Mensagem trilíngue via `T(en,pt,es)` |
| Split de WIM | 3.800 MB por parte em FAT32 | [CONFIRMADO] | Limite de 4 GB |
| Bloco de cópia | 1 MB + SHA-256 | [CONFIRMADO] | `MediaTransfer.cs` |
| Tamanho de downloads de instaladores | — | [LACUNA] | Sem catálogo recuperado |

## 11. Lacunas desta fase

| item | confiança | observações |
|---|---|---|
| Qualquer valor de FPS/frametime | [LACUNA] | Sem captura de apresentação; nome do produto engana |
| Temperatura/clock/uso de GPU e CPU | [LACUNA] | Sem APIs de sensor |
| VRAM, fans, rede, I/O medido | [LACUNA] | — |
| URL/hash/argumentos de cada instalador | [LACUNA] | `#US` cifrado |
| Lista completa de 85 bitmaps | [LACUNA] | 35 nomes legíveis de 85 |
| Regras de recomendação (limiares) | [LACUNA] | `RecommendedProfile` só por nome |
| Detecção de hardware (provedor) | [LACUNA] | Sem `Win32_*` em claro |
| Persistência da fila de tarefas | [LACUNA] | — |
| Medição de latência (DPC/ISR) | [LACUNA] | LatencyMon aparece apenas como download |

## 12. O que replicar no Sevenzin

**Replicável com paridade comprovada**
1. Perfil estático com os 9 campos + `RecommendedProfile`/`HasCustomSettings` (seção 1).
2. Enumeração de modos de vídeo via DEVMODE (`dmPelsWidth`, `dmDisplayFrequency`) — inclusive para escolher Hz antes do jogo.
3. Controles por processo: CPU Sets, afinidade, prioridade, boost, working set, `excludeCore0`, `pCores`, `timeBeginPeriod`.
4. Cards fixos (EA FC, Fortnite, Rust, Warzone) + descoberta Steam/Epic/custom.
5. Catálogo de instaladores com 35 logos confirmados e allowlist winget de 5 itens.
6. Fórmulas de espaço de mídia e split de WIM em 3.800 MB.

**Exige implementação nova (não copiar promessa)**
1. FPS/frametime: precisa de ETW (`Microsoft-Windows-DXGI`), PresentMon ou hook próprio.
2. Temperatura/clock/uso de GPU: NVML/NVAPI (NVIDIA), ADL/IGCL (AMD/Intel) ou PDH `GPU Engine`.
3. Temperatura de CPU: `MSAcpi_ThermalZoneTemperature` (limitado) ou driver de EC (LibreHardwareMonitor).
4. Uso de CPU: `PerformanceCounter`/`GetSystemTimes` — trivial, mas **não comprovado** no original.
5. Catálogo de downloads: URLs/hashes teriam de ser criados do zero e mantidos.

**Não replicar**
1. `EnableLUA=0` (desliga UAC) e remoção forçada de Edge/OneDrive sem opt-in claro.
2. `timeBeginPeriod` global permanente e trim agressivo de working set como "otimização".
3. Instaladores de terceiros sem verificação de assinatura/hash (o original valida assinatura só em Edge/OneDrive).

---

**Arquivo gravado:** `C:\re\geandre_v3\docs\05_metricas_instaladores_jogos.md`
**Catálogo:** `C:\re\geandre_v3\docs\metricas.json` (JSON validado)
