# 00 — Resumo consolidado

**Alvo:** `Z:\Computador\Downloads\honest\geandre.exe` → cópia de trabalho `C:\re\geandre_v3\geandre.exe`
**SHA-256:** `70DC5163CF1343D22484D0B2702AFDA125D37A38904D74EE9F90FAA489AFCB72`
**MD5:** `bd5834f260b34e5a11220d5a83b5e6d4` · **Tamanho:** 68.518.400 bytes
**Timestamp PE:** `2026-10-23T18:16:19Z` · **Versão:** `2.5.1` / `2.5.1.0`
**Método:** engenharia reversa **estática**. O binário **não foi executado** em nenhum momento.

Legenda: `[CONFIRMADO]` = lido do arquivo/recurso; `[INFERIDO]` = deduzido com evidência citada; `[LACUNA]` = indeterminado pelo método estático.

---

## 1. O que é o aplicativo

`[CONFIRMADO]` Aplicativo Windows desktop (WinForms + Guna.UI2, com ilhas WPF e render SkiaSharp), alvo de build `.NETFramework,Version=v4.8`, assembly `Honest Tech, Version=2.5.1.0`, `ProductName=Fps`, `FileDescription=Fps`, copyright 2026, string de build `Version: 2026.10.07`.

`[CONFIRMADO]` Funcionalmente é um **utilitário tudo-em-um gamer/PC** com seis blocos:

| bloco | função | confiança |
|---|---|---|
| Autenticação | Login/registro/licença/2FA via KeyAuth | [CONFIRMADO] |
| Perfil do PC | Inventário estático + comparação com perfil recomendado | [CONFIRMADO] |
| Tweaks | 12 grupos de ajustes de registro (privacidade, telemetria, UAC, OneDrive…) | [CONFIRMADO] |
| Windows Media | Analisar ISO do Windows, remover apps/features, injetar drivers, gerar ISO ou USB bootável | [CONFIRMADO] |
| Instaladores | Catálogo de ~85 logos, download e instalação silenciosa | [CONFIRMADO] |
| Jogos | Descoberta Steam/Epic/custom + perfil de CPU/IO por jogo | [CONFIRMADO] |

`[CONFIRMADO]` Acessórios: captura de tela com editor de anotação, assistente de chat (IA/humano), guias, BIOS, notificações, tarefas agendadas internas e tela de proteção.

`[INFERIDO]` O nome comercial é "Honest" (recursos `Honest.*`, chaves `HKCU\...\Honest`, endpoint `honest.*`), enquanto `Fps` é apenas o nome do produto no PE — **não há medição de FPS**.

## 2. Stack e proteções

| item | origem | confiança | observações |
|---|---|---|---|
| PE32+ x64 ILONLY | cabeçalho PE + CLR header RVA `0x2000` | [CONFIRMADO] | `Machine 0x8664`, linker 6.0, Subsystem 2 (GUI) |
| Sem imports/exports/TLS/debug | diretório de dados PE | [CONFIRMADO] | EntryPoint RVA 0 no PE externo |
| Metadados CLR `v4.0.30319` | CLR header | [CONFIRMADO] | `BSJB` em offset `0x25524D8`, 29.257.708 bytes |
| Costura.Fody | 22 assemblies embutidos, `141_costura.metadata` | [CONFIRMADO] | Carregamento em memória |
| Eazfuscator.NET | `Eazfuscator.NET.Runtime.Licensing.WindowsForms.Error.png` + padrões de ofuscação | [CONFIRMADO] | Nomes de TypeDef obfuscados |
| `#US` cifrado | heap de strings de usuário ilegível | [CONFIRMADO] | Impede recuperar textos de UI |
| Manifesto | `requireAdministrator`, `PermissionSet Unrestricted`, `longPathAware=true`, `dpiAwareness=unaware` | [CONFIRMADO] | Execução sempre elevada |
| Assinatura digital | ausente | [CONFIRMADO] | Sem `WinVerifyTrust` válido; 9 PEs embutidos |
| Integridade | `VerifyAssemblyIntegrity` + `System.Reflection.Metadata` | [CONFIRMADO] | Lê os próprios metadados em runtime |

`[CONFIRMADO]` P/Invoke relevante mapeado: CPU Sets, afinidade, prioridade/boost, `SetProcessWorkingSetSize`, `BitBlt`, `RegisterHotKey`, `AddFontResource`, `wimgapi.dll`, `OpenVirtualDisk`/`AttachVirtualDisk`/`DetachVirtualDisk`, `SHQueryRecycleBin`/`SHEmptyRecycleBin`, `GlobalMemoryStatusEx`, `EnumDisplayDevices`, `EnumDisplaySettingsW`, `GetThreadTimes`, `Thread32First/Next`, `NtQueryInformationThread`, `OpenProcessToken`/`AdjustTokenPrivileges`, `ProcessIdToSessionId`, `timeBeginPeriod`/`timeEndPeriod`, `DwmGet/SetWindowAttribute`.

## 3. Arquitetura em camadas

`[CONFIRMADO]` Os 142 recursos gerenciados revelam a divisão:

| camada | evidência | confiança |
|---|---|---|
| `Honest.UI` | 52 ícones `Honest.UI.Icons.*.png`, `Honest.UI.Icons.manifest.json` | [CONFIRMADO] |
| `Honest.WindowsMedia` | `BuildMedia.ps1`, `MediaOperations.ps1`, `AnalyzeImage.ps1`, `PostInstall.ps1`, `MediaRules.ps1`, `MediaTransfer.cs`, `IsoImage.cs`, `OfflineHive.cs` | [CONFIRMADO] |
| `Honest.Tweaks` | `EdgeBrowserUninstall.ps1`, `OneDriveUninstall.ps1` + 12 grupos | [CONFIRMADO] |
| `Honest.Native` | `auth_native.dll`, `clrjit_host.dll`, 13 fontes Geist/Inter, `Nvidia.PciIds` (112.604 B) | [CONFIRMADO] |
| `Honest.Properties.Resources` | blob `BEEFCACE` de 4.042.152 B com imagens | [CONFIRMADO] |
| `Honest.social-endpoint.json` | `honest-social-v25-858dca.emulationzone2026.workers.dev` | [CONFIRMADO] |
| `Fps.*` | `Fps.HomePhotos.*.jpg`, `Fps.Captures.capture-shutter.wav`, `Fps.Assistant` | [CONFIRMADO] |

`[CONFIRMADO]` Backends: `honest.emulationzone2026.workers.dev`, `honestapp.emulationzone2026.workers.dev`, `honest-social-v25-858dca.emulationzone2026.workers.dev` (Cloudflare Workers).

`[CONFIRMADO]` Localização trilíngue EN/PT/ES via `T(en, pt, es)` — cada string de UI nos scripts aparece nos três idiomas lado a lado.

`[INFERIDO]` A UI principal é WinForms com formulários por página; o conteúdo de imagem/vídeo usa ilhas WPF e o editor de captura usa SkiaSharp.

## 4. As 22 telas

`[CONFIRMADO]` Mapeadas em `03_telas.md` com mapa de navegação:

1. Visão geral · 2. Login · 3. Shell de navegação · 4. Home · 5. Jogos · 6. Placa de vídeo · 7. Processador · 8. Armazenamento/Localização/Mouse · 9. Ajustes/Tweaks · 10. Instaladores · 11. Guias · 12. BIOS · 13. Assistente · 14. Capturas · 15. Pendrive/Windows Media · 16. Perfil · 17. Configurações · 18. Notificações e Tarefas agendadas · 19. Proteção · 20. Sair.

`[INFERIDO]` Navegação por barra lateral com ícones `nav_home`, `nav_gpu`, `nav_bios8`, `nav_tweaks`, `Nav.IconGames`, `Nav.IconInstallers`; `seta`/`seta_direita` para avançar/voltar.

## 5. Funcionalidades-chave

| funcionalidade | detalhe confirmado | confiança |
|---|---|---|
| Autenticação KeyAuth | Login, Register, License, WebLogin, Check, 2FA (Enable/Disable/Secret), Forgot, Ban, Chat, Download, Expiry, Stats | [CONFIRMADO] |
| Tweaks | 12 grupos; `diagnostics` grava `AllowTelemetry=1` (mínimo, não desliga); `uac` grava `EnableLUA=0`; estágios `specialize` (hive `DEFAULT`) e `logon` | [CONFIRMADO] |
| Windows Media | `action=disks|build`, `mode=iso|usb`; contrato `$request`/`$result` completo; ISO via IMAPI2FS com El Torito BIOS+UEFI; split de WIM em 3.800 MB para FAT32 | [CONFIRMADO] |
| Segurança de USB | `BusType=USB`, `!IsBoot/IsSystem/IsOffline/IsReadOnly`, `Size ≥ 8 GB`, `UniqueId` não vazio, recusa raiz de `%windir%`/ISO/drivers/hostPath; reidentificação por `UniqueId`+`Serial`+`Size` | [CONFIRMADO] |
| winget | allowlist fechada: `steam, discord, 7zip, chrome, vlc`; comando com `--exact --silent --no-upgrade`; espera 30×3 s | [CONFIRMADO] |
| Jogos | `SteamAppId`, `EpicAppName`, `IsCustomGame`/`CustomPath`/`ExecutableName`; controles `CpuPriority`, `pCores`, `excludeCore0`, `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled`, `IoPriority` | [CONFIRMADO] |
| Captura | Hotkey global, região/janela/monitor, `BitBlt`+`CopyFromScreen`, editor (pencil/rectangle/arrow/text/undo), save/copy/check/close, som `capture-shutter.wav` | [CONFIRMADO] |
| Assistente | `KeyAuth_ChatSend`/`ChatGet`, `LastPollTimestamp`, `HumanMode` (IA ↔ atendente), `IncludeInContext`, `ShareWithAi` | [CONFIRMADO] |

`[CONFIRMADO]` Origens GPL embutidas: Rufus, UEFI:NTFS, ntfs-3g, efifs (textos de licença no blob). Relevante para o licenciamento do Sevenzin.

## 6. Métricas: prometido vs. comprovado

`[CONFIRMADO]` `metricas.json` cataloga 38 entradas — **13 confirmadas, 13 inferidas, 12 lacunas** — em quatro seções: perfil estático (13), sistema dinâmico (15), controles de desempenho (6), aplicação (4).

**Existe de verdade (`[CONFIRMADO]` / `[INFERIDO]` com API)**
- Memória: `GlobalMemoryStatusEx` `[CONFIRMADO]` (API)
- Espaço livre em disco: `DriveInfo.AvailableFreeSpace` `[CONFIRMADO]`
- Lixeira: `SHQueryRecycleBin` / `SHEmptyRecycleBin` `[CONFIRMADO]`
- Tempo de CPU por thread: `GetThreadTimes` + `Thread32First/Next` `[CONFIRMADO]` (API)
- Modos de vídeo: `EnumDisplaySettingsW` + DEVMODE (`dmPelsWidth`, `dmDisplayFrequency`, `dmBitsPerPel`) `[CONFIRMADO]`
- Identificação de GPU: tabela `Honest.Nvidia.PciIds` `[CONFIRMADO]`
- Dias de licença, versão, usuários online, progresso de download/cópia `[CONFIRMADO]`

**Não existe (`[LACUNA]`)** — nenhum número foi inventado:
- FPS / frametime (sem PresentMon, ETW, DXGI ou hook de `Present`)
- Uso, temperatura ou clock de GPU (sem NVML/NVAPI/ADL/IGCL/PDH)
- Temperatura de CPU (sem `MSAcpi_ThermalZoneTemperature`/LibreHardwareMonitor)
- VRAM, fans, rede, throughput de disco, latência DPC/ISR, uptime

`[INFERIDO]` Conclusão dura: os "medidores" da Home são, no máximo, memória + disco + lixeira + estado do perfil. **Qualquer FPS/temperatura na reimplementação seria feature nova.**

## 7. Instaladores e jogos

`[CONFIRMADO]` 35 logos de instaladores com nome legível (7-Zip, Autoruns, Brave, CPU-Z, DirectX, DriverStore, Epic, FACEIT, Gamers Club, Chrome, HWiNFO, driver de rede, LatencyMon, G HUB, Edge, Afterburner, NVCleanstall, OCAT, Open-Shell, Process Explorer, Radeon Slimmer, Rockstar, StartAllBack, TeamSpeak, Ubisoft Connect, VALORANT, Visual Studio, VS Code, ZenTimings, DDU, Prime95, Geekbench, OCCT, GPU-Z, KMS) + `ic_discord` + `steam.png`.

`[INFERIDO]` Outros ~19 nomes prováveis na região de 85 bitmaps (VLC, Spotify, Everything, Revo, Notepad, qBittorrent, OBS, CapFrameX, HWMonitor, TestMem5, chipset AMD, Steam, EA App, Minecraft, FiveM, LoL, VC++ Redist, driver Bluetooth, Unlocker) — apenas 35 nomes completos decodificados.

`[LACUNA]` URL, tamanho, hash e argumentos de cada instalador (ficam em `#US` cifrado). As flags `/S` e `/silent` **não** foram confirmadas; `--silent` existe apenas no comando winget.

`[CONFIRMADO]` Jogos: 4 cards fixos com arte própria (EA FC, Fortnite, Rust, Warzone) + 3 fotos de seção (`games.jpg`, `ai.jpg`, `driver.jpg`); plataformas Steam/Epic/custom com descoberta por biblioteca/manifesto/caminho manual.

## 8. Fluxos

14 fluxos documentados em `06_fluxos.md`:

| # | fluxo | confiança do núcleo |
|---|---|---|
| F1 | Inicialização e portão de segurança (elevação → fontes → integridade → handshake) | [CONFIRMADO] |
| F2 | Autenticação em três modos + pós-login | [CONFIRMADO] |
| F3 | Home e perfil do PC | [CONFIRMADO] |
| F4 | Aplicar tweaks (12 grupos, tolerante a falha) | [CONFIRMADO] |
| F5 | Media — analisar ISO (11 etapas literais) | [CONFIRMADO] |
| F6 | Media — construir ISO (15 etapas literais) | [CONFIRMADO] |
| F7 | Media — construir USB (7 etapas + `SafeUsb`) | [CONFIRMADO] |
| F8 | Primeiro logon na máquina instalada (`specialize`/`logon`) | [CONFIRMADO] |
| F9 | Instaladores — fila de download/instalação | [CONFIRMADO] (metadados [LACUNA]) |
| F10 | Jogos — adicionar, configurar, jogar | [CONFIRMADO] |
| F11 | Captura de tela e anotação | [CONFIRMADO] |
| F12 | Assistente (IA/humano) | [CONFIRMADO] |
| F13 | Atualização | parcial |
| F14 | Cancelamento, erro e limpeza | [CONFIRMADO] |

## 9. Limites do método

`[CONFIRMADO]` Estas são as fronteiras reais da análise estática — não falhas de execução:

| limite | causa | consequência |
|---|---|---|
| Sem descompilação IL | `ilspycmd` 8.2/11.2 e `dnfile` falham com `BadImageFormatException: Read out of bounds` (AssemblyRef falsos); sem .NET SDK (só runtime 8.0.34) | Lógica interna é reconstruída por nomes de membros, não por corpo de método |
| Textos de UI ilegíveis | `#US` cifrado + TypeDefs obfuscados | Rótulos exatos, mensagens de erro e textos de contrato são [LACUNA] |
| Catálogo de instaladores incompleto | dados em `#US` cifrado | URLs/hashes/args [LACUNA] |
| 85 → 35 nomes de bitmap | parser do `ResourceReader` falhou; nomes recuperados por varredura UTF-16LE e `manifest.json` | Associação nome→imagem aproximada |
| Sem prova de runtime | proibido executar | Métricas e comportamento em execução [LACUNA] |

`[CONFIRMADO]` Armadilha de offsets documentada: streams são relativos à raiz MD `0x25524D8`; `AssemblyRef` ocupa ~28 MB como padding e as tabelas reais ficam perto de rel `0x1B7C900`.

## 10. Lacunas prioritárias

| prioridade | lacuna | impacto no Sevenzin |
|---|---|---|
| 1 | Textos e mensagens de UI (`#US` cifrado) | Precisa reescrever todo copy — não é bloqueante, mas impede paridade textual |
| 2 | Catálogo de instaladores (URL/hash/args) | Reimplementação exige criar e manter catálogo próprio |
| 3 | Regras de recomendação de perfil (limiares) | Sem isso, "PC recomendado" é heurística nova |
| 4 | Detecção de hardware (provedor CPU/placa-mãe) | Sem `Win32_*` em claro; escolher WMI ou setupapi no novo app |
| 5 | Associação dos 50 bitmaps restantes | Afeta fidelidade visual do catálogo |
| 6 | Métricas dinâmicas (FPS/temp/clocks) | Confirmado que **não existem** no original — decisão de produto, não de engenharia reversa |
| 7 | Persistência da fila de tarefas | Necessário definir armazenamento no novo app |
| 8 | Combinação do hotkey de captura e destino do "save" | Detalhe de UX a definir |

## 11. Plano de recriação no Sevenzin

**Fase A — Núcleo replicável com paridade (dados confirmados)**
1. Modelo de perfil: 9 campos (`DeviceType`, `NotebookModel`, `Motherboard`, `Memory`, `Storage`, `Display`, `Country`, `PowerSupply`, `Case`, `Cooler`) + `RecommendedProfile`/`HasCustomSettings`/`UpdatedAt`.
2. Enumeração de vídeo por DEVMODE (`dmPelsWidth`, `dmDisplayFrequency`, `dmBitsPerPel`) — permite escolher Hz antes do jogo.
3. Controles por processo: CPU Sets → afinidade → prioridade → boost → working set, com `excludeCore0` e `pCores`.
4. Tweaks: 12 grupos com os mesmos nomes e escrita idempotente tolerante a falha.
5. Catálogo de instaladores com os 35 logos confirmados + allowlist winget de 5 IDs.
6. Windows Media: contrato `$request`/`$result`, 11 etapas de análise, 15 de ISO, 7 de USB, `SafeUsb`/`CheckUsb`, fórmulas de espaço e split de 3.800 MB.
7. Cards fixos (EA FC, Fortnite, Rust, Warzone) + descoberta Steam/Epic/custom.
8. Captura com hotkey, três modos, editor e som.
9. Assistente com `HumanMode`, `IncludeInContext` e `ShareWithAi`.

**Fase B — Exige implementação nova**
1. FPS/frametime: ETW `Microsoft-Windows-DXGI`, PresentMon ou hook de `Present`.
2. GPU: NVML/NVAPI (NVIDIA), ADL/IGCL (AMD/Intel) ou PDH `GPU Engine`.
3. CPU: `GetSystemTimes`/`PerformanceCounter` para uso; `MSAcpi_ThermalZoneTemperature` ou driver de EC para temperatura.
4. Catálogo de downloads com URLs/hashes próprios e verificação de assinatura.
5. Armazenamento persistente da fila de tarefas e do histórico.

**Fase C — Não replicar**
1. `EnableLUA=0` (desliga UAC) e remoção forçada de Edge/OneDrive sem opt-in explícito.
2. `timeBeginPeriod` global permanente e trim agressivo de working set vendidos como "otimização".
3. Instalação de terceiros sem verificação de assinatura/hash.
4. Reaproveitar binários GPL (Rufus/UEFI:NTFS/ntfs-3g/efifs) sem cumprir a licença.

## 12. Índice de arquivos e contagens

| arquivo | conteúdo | CONFIRMADO | INFERIDO | LACUNA | linhas |
|---|---|---|---|---|---|
| `01_triagem.md` | PE, CLR, metadados, recursos, proteções | 80 | 2 | 7 | 159 |
| `02_estrutura.md` | Camadas, recursos, P/Invoke, backend, domínio | 125 | 3 | 6 | 246 |
| `03_telas.md` | 22 telas + mapa de navegação | 142 | 14 | 18 | 326 |
| `04_funcionalidades.md` | 16 seções de funcionalidades | 163 | 15 | 21 | 309 |
| `05_metricas_instaladores_jogos.md` | Métricas, instaladores, jogos, mídia | 103 | 59 | 33 | 253 |
| `06_fluxos.md` | 14 fluxos (F1–F14) | 116 | 15 | 22 | 327 |
| **Total** | | **729** | **108** | **107** | **1.620** |

Apoio:
- `metricas.json` — 38 entradas (13/13/12), JSON validado
- `output/resource_inventory.json` — 142 recursos classificados
- `output/strings_heap.txt` — 21.775 linhas do heap `#Strings`
- `output/strings_ascii.txt` — 378.239 linhas
- `output/res_manifest.json` / `res_names.txt` — nomes de bitmap recuperados
- `output/triage.txt`, `triage.json`, `manifest.xml` — metadados PE/CLR e manifesto
- `resources_clean/` — 142 recursos limpos (scripts, fontes, ícones, EFI, DLLs, JSON)
- `resources_imgs/` — 86 imagens extraídas

## 13. Veredito de paridade

`[CONFIRMADO]` Há material suficiente para reproduzir **estrutura, telas, fluxos, contratos de script, regras de segurança de mídia e controles de processo** com alta fidelidade.

`[LACUNA]` Não há material para reproduzir **textos exatos de UI**, **catálogo de downloads** e **qualquer medição de desempenho em tempo real** — para esses três, o Sevenzin terá conteúdo novo, não paridade.

`[INFERIDO]` A promessa implícita do produto ("Fps", medidores na Home) é maior que a implementação comprovada. Reproduzir a promessa exigiria adicionar instrumentação que o binário original não possui.

---

**Arquivo gravado:** `C:\re\geandre_v3\docs\00_resumo.md`
**Entrega completa:** 7 documentos (00–06) + `metricas.json`
