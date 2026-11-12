# 02 — Estrutura interna

**Alvo:** `C:\re\geandre_v3\geandre.exe` (SHA-256 `70DC5163…AFCB72`)
**Método:** parsing estático do cabeçalho CLR, streams de metadados, tabelas Ecma-335 e tabela `ManifestResource`.

Legenda: `[CONFIRMADO]` = lido do arquivo; `[INFERIDO]` = deduzido com evidência citada; `[LACUNA]` = indeterminado.

---

## 1. Mapa de offsets

| região | RVA | Offset no arquivo | Tamanho | confiança | observações |
|---|---|---|---|---|---|
| Cabeçalho PE | — | `0x000000` | 512 | [CONFIRMADO] | `DOS` + `NT` + optional header |
| Seção `.text` | `0x2000` | `0x2000` | 68.391.936 | [CONFIRMADO] | Contém CLR header + metadados + recursos |
| CLR header | `0x2000` | `0x2000` | 72 | [CONFIRMADO] | `IMAGE_COR20_HEADER` |
| Recursos gerenciados | `0x8D6024` | `0x8D4224` | 29.876.916 | [CONFIRMADO] | Blobs de `ManifestResource` |
| Raiz dos metadados (`BSJB`) | `0x25542D8` | `0x25524D8` | 29.257.708 | [CONFIRMADO] | Termina em `0x413B284` |
| Seção `.rsrc` | `0x413C000` | `0x413B400` | 125.952 | [CONFIRMADO] | Ícones/versão/manifesto |

Cálculo dos offsets: `offset_arquivo = RVA − VirtualAddress_da_seção + PointerToRawData_da_seção`. Para `.text` as três grandezas coincidem (`0x2000`), então RVA = offset nessa seção.

## 2. Cabeçalho CLR

| campo | valor | confiança | observações |
|---|---|---|---|
| `cb` | 72 | [CONFIRMADO] | — |
| `MajorRuntimeVersion` / `Minor` | 2 / 5 | [CONFIRMADO] | Cabeçalho de runtime 2.5 (ILONLY) |
| `MetaData` (RVA/tamanho) | `0x25542D8` / 29.257.708 | [CONFIRMADO] | — |
| `Flags` | `ILONLY` | [CONFIRMADO] | Sem `NATIVE_ENTRYPOINT`, sem `STRONGNAMESIGNED` |
| `EntryPointToken` | `0x06006E1D` | [CONFIRMADO] | MethodDef `0x6E1D` (28.189 decimal) |
| `Resources` (RVA/tamanho) | `0x8D6024` / 29.876.916 | [CONFIRMADO] | Região dos `ManifestResource` |
| `StrongNameSignature` | RVA `0`, tamanho `0` | [CONFIRMADO] | Assembly não possui nome forte |

## 3. Streams de metadados

Todos os offsets abaixo são **relativos à raiz `0x25524D8`** (armadilha comum: leitores que tratam como absolutos falham).

| stream | offset relativo | tamanho | confiança | observações |
|---|---|---|---|---|
| `#-` (tabelas não comprimidas) | `0x6C` | 28.833.412 | [CONFIRMADO] | Stream dominante; contém as tabelas reais + padding |
| `#Strings` | `0x1B7F6F0` | 332.900 | [CONFIRMADO] | 21.775 strings; nomes de tipos/membros preservados parcialmente |
| `#US` | `0x1BD0B54` | 3.304 | [CONFIRMADO] | **Cifrado** — nenhum literal UTF-16 legível |
| `#GUID` | `0x1BDD87C` | 16 | [CONFIRMADO] | Um GUID |
| `#Blob` | `0x1BDD88C` | 87.968 | [CONFIRMADO] | Assinaturas, constantes, atributos |
| Cabeçalho | `0x0` | 108 | [CONFIRMADO] | Versão `v4.0.30319`, `heapSizes = 0x05` |

### Distribuição do stream `#-`

| faixa (relativa) | conteúdo | confiança | observações |
|---|---|---|---|
| `0x00` – ~`0x1B7C000` | Tabela `AssemblyRef` com ~28 MB de registros inválidos/padding | [CONFIRMADO] | Inflada de propósito; causa `BadImageFormatException` em ILSpy/dnfile |
| ~`0x1B7C900` – `0x1B7D256` | Tabelas reais (fim do stream) | [CONFIRMADO] | `ManifestResource` em `0x1B7C986` |
| `0x1B7D256` – `0x1B7F6F0` | Padding até o fim declarado | [CONFIRMADO] | — |

## 4. Tabelas de metadados (contagens reais)

| tabela | linhas | confiança | observações |
|---|---|---|---|
| `TypeRef` | 905 | [CONFIRMADO] | Referências externas (BCL, Guna, SkiaSharp, Newtonsoft, WPF) |
| `TypeDef` | 1.405 | [CONFIRMADO] | Nomes ofuscados pelo Eazfuscator |
| `Field` | 7.364 | [CONFIRMADO] | — |
| `MethodDef` | 28.944 | [CONFIRMADO] | Inclui o entry point `0x6E1D` |
| `Param` | 9.733 | [CONFIRMADO] | — |
| `MemberRef` | 4.530 | [CONFIRMADO] | Chamadas a BCL/terceiros |
| `CustomAttribute` | 1.225 | [CONFIRMADO] | Inclui atributos de licenciamento do Eazfuscator |
| `StandAloneSig` | 1.977 | [CONFIRMADO] | — |
| `TypeSpec` | 732 | [CONFIRMADO] | Genéricos |
| `NestedClass` | 771 | [CONFIRMADO] | Muitos tipos aninhados (padrão de ofuscador) |
| `MethodSpec` | 839 | [CONFIRMADO] | Chamadas genéricas |
| `ManifestResource` | **142** | [CONFIRMADO] | Tabela real em `0x1B7C986`, stride 16, fim `0x1B7D256` |

## 5. Recursos manifestos (142)

Nomes preservados porque fazem parte do contrato de recursos (o ofuscador não os renomeia). Inventário completo em `output\resource_inventory.json`; arquivos limpos em `resources_clean\`.

### 5.1 Fontes (13)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `Honest.Native.Fonts.Inter-Wpf-Regular.ttf` | 340.560 | [CONFIRMADO] | Inter para WPF |
| `Honest.Native.Fonts.Inter-Wpf-Bold.ttf` | 342.224 | [CONFIRMADO] | Inter Bold para WPF |
| `Honest.Native.Fonts.Inter.ttf` | 874.712 | [CONFIRMADO] | Inter variable/estático |
| `Honest.Resources.Fonts.Geist-Regular.ttf` | 126.052 | [CONFIRMADO] | Geist |
| `Honest.Resources.Fonts.Geist-Medium.ttf` | 127.664 | [CONFIRMADO] | Geist |
| `Honest.Resources.Fonts.Geist-SemiBold.ttf` | 127.876 | [CONFIRMADO] | Geist |
| `Honest.Resources.Fonts.Geist-Bold.ttf` | 128.828 | [CONFIRMADO] | Geist |
| `Honest.Resources.Fonts.GeistMono-Regular.ttf` | 148.520 | [CONFIRMADO] | Geist Mono |
| `Honest.Resources.Fonts.GeistMono-Medium.ttf` | 149.332 | [CONFIRMADO] | Geist Mono |
| `Honest.Native.Fonts.Geist-*` (4) | idem | [CONFIRMADO] | Duplicatas para o caminho nativo |

`[INFERIDO]` As duplicatas `Honest.Native.Fonts.*` existem para instalação via `AddFontResource`/`AddFontMemResourceEx` (P/Invoke presente no heap), enquanto `Honest.Resources.Fonts.*` é usado pelo WinForms/WPF.

### 5.2 Marca e imagens de tela

| recurso | tamanho (B) | dimensões | confiança | observações |
|---|---|---|---|---|
| `Honest.Resources.honest_dot_logo.png` | 259.560 | 1254×1254 | [CONFIRMADO] | Logo principal |
| `Honest.Resources.sem_fundo.png` | 259.560 | 1254×1254 | [CONFIRMADO] | Mesmo tamanho do logo (provável variante transparente) |
| `Honest.Resources.honest_login_ascii.png` | 504.572 | — | [CONFIRMADO] | Arte ASCII da tela de login |
| `Honest.Resources.assistant_avatar.png` | 1.368 | — | [CONFIRMADO] | Avatar do assistente |
| `Honest.Resources.assistant_send.png` | 2.368 | — | [CONFIRMADO] | Botão enviar do chat |
| `Honest.ApplicationIcon` | 122.972 | — | [CONFIRMADO] | Ícone multi-resolução usado em runtime |
| `Fps.HomePhotos.driver.jpg` | 26.804 | — | [CONFIRMADO] | Card "Driver" da Home |
| `Fps.HomePhotos.ai.jpg` | 73.548 | — | [CONFIRMADO] | Card "IA" da Home |
| `Fps.HomePhotos.games.jpg` | 40.576 | — | [CONFIRMADO] | Card "Jogos" da Home |
| `Fps.Captures.capture-shutter.wav` | 21.656 | — | [CONFIRMADO] | Som do obturador de captura |
| `Eazfuscator.NET.Runtime.Licensing.WindowsForms.Error.png` | 148.972 | — | [CONFIRMADO] | Diálogo de erro de licença do Eazfuscator |

### 5.3 Ícones de UI (52 + manifesto)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `Honest.UI.Icons.manifest.json` | 20.176 | [CONFIRMADO] | 52 ícones, cor `#E2E2E2`, stroke 1,6, tamanhos 128/256, revisões Lucide `70562c1e…` e Tabler `a4ce1404…` |
| `Honest.UI.Icons.<nome>.png` (52) | 284 – 3.688 | [CONFIRMADO] | `settings, camera, usb, user, capture-region, window, monitor, pencil, rectangle, annotation-arrow, text, undo, save, copy, check, close, star, back, send, link, image, plus, gamepad, chat, power, menu, next, expand, collapse, minimize, maximize, restore, steam, windows, home, tweaks, gpu, guides, bios, assistant, cpu, download, fingerprint, hdd, location, mouse, notifications, scheduled-tasks, play, protection, arrow-right, arrow-up` |

O `manifest.json` também define `legacyResources`, mapeando nomes antigos de bitmap para os ícones novos — útil para rastrear a evolução da UI (ver `03_telas.md`).

### 5.4 Scripts e código-fonte embutidos (9)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `Honest.WindowsMedia.BuildMedia.ps1` | 19.172 | [CONFIRMADO] | Orquestrador de ISO/USB |
| `Honest.WindowsMedia.AnalyzeImage.ps1` | 19.396 | [CONFIRMADO] | Inventário de imagem Windows (apps/capabilities) |
| `Honest.WindowsMedia.MediaOperations.ps1` | 21.656 | [CONFIRMADO] | Cópia, verificação, partição, boot |
| `Honest.WindowsMedia.PostInstall.ps1` | 7.644 | [CONFIRMADO] | Tweaks pós-instalação (12 grupos) |
| `Honest.WindowsMedia.MediaRules.ps1` | 3.648 | [CONFIRMADO] | Listas de proteção (apps/capabilities) |
| `Honest.WindowsMedia.MediaTransfer.cs` | 6.852 | [CONFIRMADO] | Cópia 1 MB + SHA-256 + verificação |
| `Honest.WindowsMedia.IsoImage.cs` | 3.440 | [CONFIRMADO] | ISO via IMAPI2FS |
| `Honest.WindowsMedia.OfflineHive.cs` | 3.476 | [CONFIRMADO] | Leitura de hive offline via `offreg.dll` |
| `Honest.Tweaks.EdgeBrowserUninstall.ps1` | 4.640 | [CONFIRMADO] | Remoção do Edge preservando WebView2 |
| `Honest.Tweaks.OneDriveUninstall.ps1` | 3.628 | [CONFIRMADO] | Remoção do OneDrive com validação de assinatura |

### 5.5 Windows Media / UEFI (13)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `Honest.WindowsMedia.UEFI.EFI.Boot.bootx64.efi` | 31.892 | [CONFIRMADO] | Bootloader UEFI x64 |
| `Honest.WindowsMedia.UEFI.EFI.Boot.bootia32.efi` | 30.292 | [CONFIRMADO] | Bootloader UEFI x86 |
| `Honest.WindowsMedia.UEFI.EFI.Boot.bootaa64.efi` | 42.516 | [CONFIRMADO] | Bootloader UEFI ARM64 |
| `Honest.WindowsMedia.UEFI.EFI.Rufus.ntfs_x64.efi` | 173.588 | [CONFIRMADO] | Driver NTFS UEFI (origem Rufus/UEFI:NTFS) |
| `Honest.WindowsMedia.UEFI.EFI.Rufus.ntfs_ia32.efi` | 163.156 | [CONFIRMADO] | Idem x86 |
| `Honest.WindowsMedia.UEFI.EFI.Rufus.ntfs_aa64.efi` | 169.492 | [CONFIRMADO] | Idem ARM64 |
| `Honest.WindowsMedia.UEFI.NOTICE.txt` | 1.772 | [CONFIRMADO] | Aviso de licença/distribuição |
| `Honest.WindowsMedia.UEFI.UPSTREAM.txt` | 1.760 | [CONFIRMADO] | Origem upstream |
| `Honest.WindowsMedia.UEFI.LICENSE-UEFI-NTFS.txt` | 18.436 | [CONFIRMADO] | Licença |
| `Honest.WindowsMedia.UEFI.LICENSE-NTFS-3G.txt` | 18.348 | [CONFIRMADO] | Licença |
| `Honest.WindowsMedia.UEFI.LICENSE-NTFS-LIB.txt` | 25.772 | [CONFIRMADO] | Licença |
| `Honest.WindowsMedia.UEFI.uefi-ntfs-v2.8-source.zip` | 462.568 | [CONFIRMADO] | Código-fonte (GPL) |
| `Honest.WindowsMedia.UEFI.ntfs-3g-v1.9-source.zip` | 1.518.388 | [CONFIRMADO] | Código-fonte (GPL) |

URLs de origem confirmadas nas strings: `github.com/pbatard/rufus/tree/875f221d…/res/uefi`, `github.com/pbatard/uefi-ntfs/tree/42de94dc…`, `github.com/pbatard/ntfs-3g/tree/a2226ecd…`, `github.com/pbatard/efifs`.

### 5.6 Dados e configuração (4)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `Honest.Nvidia.PciIds` | 112.604 | [CONFIRMADO] | Tabela PCI ID NVIDIA offline; comentário `# NVIDIA vendor section bundled for offline GPU identification.` |
| `Honest.social-endpoint.json` | 292 | [CONFIRMADO] | `apiUrl = https://honest.emulationzone2026.workers.dev` + 2 URLs anteriores |
| `Honest.native.clrjit_host.dll` | 6.690.308 | [CONFIRMADO] | DLL nativa (PE x64) |
| `Honest.native.auth_native.dll` | 3.472.388 | [CONFIRMADO] | DLL nativa (PE x64) |

### 5.7 Costura (23)

| recurso | tamanho (B) | confiança | observações |
|---|---|---|---|
| `costura.*.dll.compressed` (22) | 2.280 – 5.237.316 | [CONFIRMADO] | Assemblies comprimidos com Deflate; descomprimidos em runtime |
| `costura.metadata` | 4.760 | [CONFIRMADO] | Formato `nome|versão|fullname|destino|sha1|tamanho` |

`[INFERIDO]` O carregamento ocorre pelo módulo `<Module>.cctor` do Costura, que registra `AssemblyResolve` e descomprime sob demanda — por isso o PE externo não tem imports.

## 6. Recursos `.resources` de formulários

Os 16 blobs `__.resources` têm nomes ofuscados no manifesto (ex.: `\u0002 .resources`), então o vínculo com a tela é feito pelo conteúdo.

| blob | tamanho (B) | conteúdo relevante | confiança | observações |
|---|---|---|---|---|
| `001` | 37.376 | nenhum nome de controle | [CONFIRMADO] | Provável form sem imagens |
| `002` | 180 | vazio | [CONFIRMADO] | Placeholder do compilador |
| `003` | 4.042.147 | `Honest.Properties.Resources` (85 bitmaps + 1 ícone) | [CONFIRMADO] | Blob principal de imagens |
| `004` | 180 | vazio | [CONFIRMADO] | — |
| `005` | 37.376 | sem nomes | [CONFIRMADO] | — |
| `006` | 67.433 | `LogoPic.Image`, `Nav.IconGames`, `Nav.IconInstallers` | [CONFIRMADO] | Form com logo + navegação lateral |
| `007` | 28.586 | `Pic2.Image`, `Pic4.Image` | [CONFIRMADO] | Form com 2 imagens |
| `008` | 21.835 | `IconIntel.Image` | [CONFIRMADO] | Página de CPU/Intel |
| `009` | 333.789 | `cardEafc`, `cardFortnite`, `cardRust`, `cardWarzone` (JPEGs) | [CONFIRMADO] | Tela de Jogos com 4 cards |
| `010` | 180 | vazio | [CONFIRMADO] | — |
| `011` | 66.438 | `animation1.BlindCoeff`, `MosaicShift`, `SlideCoeff` | [CONFIRMADO] | Transição Guna.UI entre telas |
| `012` | 180 | vazio | [CONFIRMADO] | — |
| `013` | 154.359 | `Pic2`, `Pic4`, `Pic6`, `Pic8` | [CONFIRMADO] | Form com 4 imagens |
| `014` | 167.436 | `Pic10`, `Pic3`, `pictureBox1/3/5/7/13` | [CONFIRMADO] | Form com 7 imagens (provável Home) |
| `015` | 62.304 | `PicInstagram.Image` | [CONFIRMADO] | Bloco de rede social |
| `016` | 184 | vazio | [CONFIRMADO] | — |

Observação metodológica: os nomes de controle aparecem como UTF-16 no blob, o que permite lê-los mesmo com metadados ilegíveis. `[CONFIRMADO]`

## 7. Modelo de domínio exposto no heap `#Strings`

Nomes de tipos/propriedades sobreviveram à ofuscação (ela renomeia TypeDefs, mas não strings de membros referenciadas por serialização). Agrupamento por área:

| área | símbolos | confiança | observações |
|---|---|---|---|
| Perfil de PC | `SavedPcProfile`, `RecommendedProfile`, `HasCustomSettings`, `DeviceType`, `Memory`, `Motherboard`, `PowerSupply`, `Case`, `Cooler`, `Storage`, `Display`, `Country`, `NotebookModel`, `HasDetails` | [CONFIRMADO] | Perfil de hardware salvo/recomendado |
| Jogos | `IsCustomGame`, `CustomPath`, `SteamAppId`, `EpicAppName`, `ExecutableName`, `KnownPaths` | [CONFIRMADO] | Jogos Steam/Epic + jogos custom |
| Chat | `Conversations`, `Messages`, `Draft`, `HumanMode`, `LastPollTimestamp`, `IncludeInContext`, `ShareWithAi`, `Role`, `Text`, `CreatedUtc` | [CONFIRMADO] | Chat com IA + modo humano |
| Desempenho | `GpuEnabled`, `CompatEnabled`, `CpuIoEnabled`, `CpuPriority`, `IoPriority`, `AccountKey`, `SelectedId` | [CONFIRMADO] | Preferências por jogo |
| Genéricos | `Title`, `UpdatedUtc`, `Description`, `Name` | [CONFIRMADO] | Entidades de catálogo |
| Licença/integridade | `CheckLicense`, `GenerateLicense`, `VerifyAssemblyIntegrity` | [CONFIRMADO] | — |
| Anti-debug | `IsDebuggerPresent`, `CheckRemoteDebuggerPresent`, `NtQueryInformationProcess`, `EnumWindows`, `GetClassName` | [CONFIRMADO] | — |

## 8. Namespaces e identidade interna

| item | confiança | observações |
|---|---|---|
| `Fps.Assistant` | [CONFIRMADO] | Namespace do assistente (string literal) |
| `Honest.Properties` | [CONFIRMADO] | Recursos globais (`Honest.Properties.Resources.resources`) |
| `Honest.UI.Icons` | [CONFIRMADO] | Ícones Lucide/Tabler |
| `Honest.Native` | [CONFIRMADO] | Fontes e DLLs nativas |
| `Honest.WindowsMedia` | [CONFIRMADO] | Pipeline de mídia |
| `Honest.Tweaks` | [CONFIRMADO] | Scripts de remoção |
| `Honest Tech` | [CONFIRMADO] | Nome do assembly (`SecurityPermissionAttribute` → `Honest Tech, Version=2.5.1.0`) |
| `Version: 2026.10.07` | [CONFIRMADO] | String de versão de build/dados |

## 9. Estrutura de UI (frameworks)

| item | confiança | observações |
|---|---|---|
| WinForms | [CONFIRMADO] | `System.Windows.Forms` + TypeRefs de `Form`, `Control` |
| Guna.UI2 2.0.4.7 | [CONFIRMADO] | `Guna2Button`, `Guna2CheckBox`, `Guna2ComboBox`, `Guna2DragControl`, `Guna2Panel`, `Guna2PictureBox`, `Guna2TextBox`, `Guna2ToggleSwitch`, `Guna2TrackBar` |
| WPF (ilhas) | [CONFIRMADO] | `PresentationFramework`, `WindowsBase`, `WindowsFormsIntegration`, `ElementHost` |
| SkiaSharp 4.153 | [CONFIRMADO] | `SKBitmap`, `SKCodec`, `SKCanvas` para desenho/anotação |
| Animações | [CONFIRMADO] | `Guna2Transition`/`animation1` com `BlindCoeff`, `MosaicShift`, `SlideCoeff` |
| Drag/resize sem borda | [CONFIRMADO] | `Guna2DragControl`, `DwmSetWindowAttribute`, `SetWindowPos`, `CreateRoundRectRgn` |

## 10. Limitações

| item | confiança | observações |
|---|---|---|
| Hierarquia real de Forms | [LACUNA] | TypeDefs ilegíveis; telas deduzidas por recursos e controles |
| Corpo IL dos métodos | [LACUNA] | `#US` cifrado + metadados inviáveis para ILSpy |
| Conteúdo das DLLs nativas | [LACUNA] | `Honest.native.*.dll` não foram desmontadas nesta fase |
| Mapeamento exato dos 85 bitmaps para cada tela | [LACUNA] | `.resources` usa `DeserializingResourceReader`; correlação nome→bytes é aproximada |
| Nomes de tabelas do backend | [LACUNA] | Só o endpoint é conhecido |

**Arquivo gravado:** `C:\re\geandre_v3\docs\02_estrutura.md`
