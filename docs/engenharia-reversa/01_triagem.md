# 01 — Triagem do binário

**Alvo:** `Z:\Computador\Downloads\honest\geandre.exe` (cópia de trabalho: `C:\re\geandre_v3\geandre.exe`)
**Data da análise:** 2026-11-12
**Método:** engenharia reversa **estática** (leitura de bytes, parsers PE/CLR próprios, extração de recursos). O binário **não foi executado**, nem qualquer um dos PEs/DLLs internos. Nenhuma alteração foi feita no arquivo original, no registro, em serviços ou em configurações do Windows.

Legenda de confiança: `[CONFIRMADO]` = lido diretamente do arquivo; `[INFERIDO]` = deduzido de evidência, com a evidência citada; `[LACUNA]` = não foi possível determinar.

---

## 1. Identificação

| item | origem | confiança | observações |
|---|---|---|---|
| Nome do arquivo | `geandre.exe` | [CONFIRMADO] | Cópia byte a byte do original em `Z:` |
| Tamanho | 68.518.400 bytes | [CONFIRMADO] | `output\triage.txt` |
| SHA-256 | `70DC5163CF1343D22484D0B2702AFDA125D37A38904D74EE9F90FAA489AFCB72` | [CONFIRMADO] | Calculado sobre a cópia local |
| MD5 | `bd5834f260b34e5a11220d5a83b5e6d4` | [CONFIRMADO] | `output\triage.txt` |
| Timestamp do cabeçalho PE | `2026-10-23T18:16:19Z` | [CONFIRMADO] | Campo `TimeDateStamp` |
| Assinatura Authenticode | ausente | [CONFIRMADO] | Sem `IMAGE_DIRECTORY_ENTRY_SECURITY`; não há certificado embutido |
| Overlay | ausente (`offset=None size=0`) | [CONFIRMADO] | Dados terminam no fim da seção `.rsrc` |

## 2. Cabeçalhos PE

| item | origem | confiança | observações |
|---|---|---|---|
| Formato | PE32+ (`0x20B`) | [CONFIRMADO] | `output\triage.json` |
| Arquitetura | x64 (`Machine 0x8664`) | [CONFIRMADO] | — |
| Linker | 6.0 | [CONFIRMADO] | Compatível com toolchain MSVC moderno |
| Subsystem | 2 (Windows GUI) | [CONFIRMADO] | App sem console |
| ImageBase | `0x400000` | [CONFIRMADO] | — |
| EntryPoint (RVA) | `0x0` no cabeçalho PE | [CONFIRMADO] | `AddressOfEntryPoint = 0`; execução real é via CLR |
| EntryPoint (token gerenciado) | `0x06006E1D` | [CONFIRMADO] | MethodDef de entrada no CLR header |
| Seções | 2 (`.text`, `.rsrc`) | [CONFIRMADO] | Sem `.reloc`, `.data` ou `.tls` |
| Imports | 0 DLLs / 0 funções | [CONFIRMADO] | Assembly 100% gerenciado; P/Invoke é resolvido em runtime |
| Exports | 0 | [CONFIRMADO] | — |
| TLS | ausente | [CONFIRMADO] | — |
| Debug directory | ausente | [CONFIRMADO] | Sem PDB |
| Rich header | ausente | [CONFIRMADO] | Removido/ausente |

### Seções

| seção | VA | VirtualSize | RawSize | Entropia | confiança | observações |
|---|---|---|---|---|---|---|
| `.text` | `0x2000` | 68.391.620 | 68.391.936 | 6,3404 | [CONFIRMADO] | Contém o PE gerenciado inteiro (metadados + recursos) |
| `.rsrc` | `0x413C000` | 125.782 | 125.952 | 2,5039 | [CONFIRMADO] | 12 recursos Win32 (ícones, versão, manifesto) |

A entropia de 6,34 em `.text` é alta para código gerenciado comum e é compatível com recursos comprimidos (Costura) e strings cifradas.

## 3. Cabeçalho CLR e metadados

| item | origem | confiança | observações |
|---|---|---|---|
| CLR header | RVA `0x2000` | [CONFIRMADO] | Imediatamente no início de `.text` |
| Runtime declarado | `v4.0.30319` | [CONFIRMADO] | CLR 4 |
| Framework alvo | `.NETFramework,Version=v4.8` | [CONFIRMADO] | String de `TargetFrameworkAttribute` em `output\strings_ascii.txt` |
| Flags | ILONLY | [CONFIRMADO] | Sem código nativo no assembly externo |
| Metadados (`BSJB`) | RVA `0x25542D8`, offset `0x25524D8` | [CONFIRMADO] | 29.257.708 bytes |
| Recursos gerenciados | RVA `0x8D6024`, offset `0x8D4224` | [CONFIRMADO] | 29.876.916 bytes; terminam na raiz dos metadados |
| Streams | `#-` 28.833.412 B; `#Strings` 332.900 B; `#US` 3.304 B; `#GUID` 16 B; `#Blob` 87.968 B | [CONFIRMADO] | Offsets relativos à raiz dos metadados |
| `#US` (user strings) | 3.304 bytes, cifrados | [CONFIRMADO] | Não há texto UTF-16 legível; bloqueia recuperação de literais de UI |

Detalhamento completo de tabelas, heaps e recursos está em `02_estrutura.md`.

## 4. Informações de versão (RT_VERSION `16/1`)

| item | origem | confiança | observações |
|---|---|---|---|
| `FileDescription` | `Fps` | [CONFIRMADO] | `output\triage.txt` |
| `ProductName` | `Fps` | [CONFIRMADO] | — |
| `ProductVersion` | `2.5.1` | [CONFIRMADO] | — |
| `FileVersion` | `2.5.1.0` | [CONFIRMADO] | — |
| `Assembly Version` | `2.5.1.0` | [CONFIRMADO] | — |
| `InternalName` | `Honest Tech.exe` | [CONFIRMADO] | Nome interno diverge do nome de distribuição |
| `OriginalFilename` | `Honest Tech.exe` | [CONFIRMADO] | — |
| `CompanyName` | vazio | [CONFIRMADO] | — |
| `LegalCopyright` | `Copyright © 2026` | [CONFIRMADO] | Ano 2026 |
| `Comments` / `LegalTrademarks` | vazio | [CONFIRMADO] | — |

Não há `AssemblyCompany`/`AssemblyProduct` preenchidos de forma útil para atribuição de autor.

## 5. Manifesto de aplicação (RT_MANIFEST `24/1`)

| item | origem | confiança | observações |
|---|---|---|---|
| `requestedExecutionLevel` | `requireAdministrator` | [CONFIRMADO] | `output\manifest.xml` — o app **exige elevação** para abrir |
| `uiAccess` | `false` | [CONFIRMADO] | — |
| `PermissionSet` | `Unrestricted="true"` | [CONFIRMADO] | Full trust |
| `dpiAware` | `false` | [CONFIRMADO] | — |
| `dpiAwareness` | `unaware` | [CONFIRMADO] | UI WinForms sem escala por monitor |
| `longPathAware` | `true` | [CONFIRMADO] | Suporte a caminhos > 260 caracteres |
| `supportedOS` | Win10, Win8.1, Win10/11 | [CONFIRMADO] | GUIDs `{35138b9a…}`, `{1f676c76…}`, `{8e0f7a12…}` |
| `assemblyIdentity` | `MyApplication.app`, versão `1.0.0.0` | [CONFIRMADO] | Identidade genérica (template padrão do Visual Studio) |

O nível `requireAdministrator` é coerente com as funções observadas no código: tweaks de `HKLM`, DISM, formatação de USB, instalação de drivers e captura global de teclado.

## 6. Recursos Win32

| ID | tipo | tamanho (B) | confiança | observações |
|---|---|---|---|---|
| `3/1` … `3/9` | RT_ICON | 1.128 / 1.720 / 2.440 / 4.264 / 6.760 / 9.640 / 16.936 / 67.624 / 12.303 | [CONFIRMADO] | 9 tamanhos do ícone do app (16→256 px, incluindo PNG comprimido em 256) |
| `14/32512` | RT_GROUP_ICON | 132 | [CONFIRMADO] | Agrupa os 9 ícones |
| `16/1` | RT_VERSION | 776 | [CONFIRMADO] | Tabela da seção 4 |
| `24/1` | RT_MANIFEST | 1.370 | [CONFIRMADO] | XML da seção 5 |

Total: 12 recursos; nenhum `RT_DIALOG`, `RT_STRING`, `RT_ACCELERATOR` ou `RT_BITMAP` — toda a UI é construída em código gerenciado (WinForms + Guna.UI2), não em recursos Win32.

## 7. Conteúdo embutido

| item | origem | confiança | observações |
|---|---|---|---|
| PEs embutidos por assinatura `MZ` | 9 ocorrências | [CONFIRMADO] | x64, x86 e ARM64 misturados; inclui binários de terceiros |
| Assemblies .NET embutidos (Costura.Fody) | 22 | [CONFIRMADO] | Lista, versões e SHA-1 em `resources_clean\141_costura.metadata` |
| `Costura.dll` | 6.0.0.0 | [CONFIRMADO] | Carregador de assemblies comprimidos |
| `Guna.UI2.dll` | 2.0.4.7 | [CONFIRMADO] | Toolkit visual da UI |
| `SkiaSharp.dll` + `libSkiaSharp.dll` | 4.153.0.0 | [CONFIRMADO] | Renderização de imagem/gráficos |
| `Newtonsoft.Json.dll` | 13.0.0.0 | [CONFIRMADO] | Serialização |
| `System.Text.Json.dll` | 10.0.0.3 | [CONFIRMADO] | Serialização adicional |
| `System.Resources.Extensions.dll` | 4.0.0.0 | [CONFIRMADO] | Leitor de `.resources` usado por `Honest.Properties.Resources` |
| `System.Formats.Nrbf.dll` | 9.0.0.0 | [CONFIRMADO] | Leitura de formato NRBF |
| `System.Reflection.Metadata.dll` | 9.0.0.0 | [CONFIRMADO] | Inspeção de metadados em runtime (usada na verificação de integridade) |
| `System.Diagnostics.EventLog`, `System.Diagnostics.PerformanceCounter` (via ServiceController/EventLog) | 10.0.0.3 | [CONFIRMADO] | Diagnóstico de sistema |
| `System.ServiceProcess.ServiceController.dll` | 10.0.0.3 | [CONFIRMADO] | Controle de serviços |
| `System.IO.Pipelines.dll` | 10.0.0.3 | [CONFIRMADO] | I/O assíncrono (chat/HTTP) |
| `Microsoft.Bcl.AsyncInterfaces` / `Microsoft.Bcl.HashCode` | 10.0.0.3 / 1.0.0.0 | [CONFIRMADO] | Dependências de compatibilidade |
| `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`, `System.Threading.Tasks.Extensions` | várias | [CONFIRMADO] | Dependências de `System.Text.Json`/SkiaSharp |
| `System.Collections.Immutable` | 9.0.0.0 | [CONFIRMADO] | Estruturas imutáveis |
| `System.Security.Principal.Windows` | 10.0.0.3 | [CONFIRMADO] | Identidade Windows |

Há ainda duas DLLs nativas carregadas fora do Costura:

| item | origem | confiança | observações |
|---|---|---|---|
| `Honest.native.clrjit_host.dll` | recurso gerenciado (6.690.308 B) | [CONFIRMADO] | Nome sugere hospedagem de JIT/CLR própria; função exata `[LACUNA]` |
| `Honest.native.auth_native.dll` | recurso gerenciado (3.472.388 B) | [CONFIRMADO] | Autenticação/credenciais nativas; função exata `[LACUNA]` |

## 8. Proteções e ofuscação

| item | origem | confiança | observações |
|---|---|---|---|
| Eazfuscator.NET | presente | [CONFIRMADO] | Runtime de licenciamento embutido (`Eazfuscator.NET.Runtime.Licensing.WindowsForms.Error.png`, recurso `142`) |
| TypeDefs renomeados | 1.405 TypeDefs, maioria com nomes ilegíveis | [CONFIRMADO] | `output\md_full.txt`; descompilação por nome inviável |
| `#US` cifrado | 3.304 B sem texto legível | [CONFIRMADO] | Literais de UI não recuperáveis estaticamente |
| `AssemblyRef` inflados | ~28 MB de padding no stream `#-` | [CONFIRMADO] | Impede leitores ILSpy/dnfile (`BadImageFormatException: Read out of bounds`) |
| Strings de API sensíveis | preservadas em claro no heap (`KeyAuth_*`, P/Invoke) | [CONFIRMADO] | Ofuscação não cobre nomes de métodos de interoperabilidade |
| Anti-debug | `IsDebuggerPresent`, `CheckRemoteDebuggerPresent`, `NtQueryInformationProcess` | [CONFIRMADO] (nomes no heap) | Uso efetivo `[INFERIDO]` |
| Verificação de integridade | `VerifyAssemblyIntegrity`, `GenerateLicense`, `CheckLicense` | [CONFIRMADO] (nomes no heap) | Algoritmo `[LACUNA]` |

## 9. Limitações desta fase

| item | origem | confiança | observações |
|---|---|---|---|
| Descompilação IL | inviável | [CONFIRMADO] | Metadados corrompidos de propósito + ausência de .NET SDK (apenas runtimes 8.0.34) |
| Textos literais da interface | não recuperados | [LACUNA] | `#US` cifrado; rótulos visíveis só existem no `manifest.json` de ícones (PT) e nos scripts |
| Nome real das telas | não recuperado | [LACUNA] | `TypeDef` de formulários ilegíveis; telas são inferidas por recursos e nomes de controles |
| Autor/empresa | não determinado | [LACUNA] | Campos de versão vazios e binário não assinado |

**Arquivo gravado:** `C:\re\geandre_v3\docs\01_triagem.md`
