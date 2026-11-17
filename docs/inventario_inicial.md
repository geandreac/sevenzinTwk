# Inventário inicial — Sevenzin (F0)

Gerado em 17/11/2026, antes das alterações do ciclo "paridade com geandre.exe". Protocolo de evidência:
`E-### | [RÓTULO] | fonte | achado | confiança`. Estados: `Implementada e validada` · `Implementada, não validada` · `Parcial` · `Não funcional` · `Não implementada` · `Não verificável`.

## 1. Mapa do repositório

| item | valor | evidência |
|---|---|---|
| Stack | WPF .NET 8 (`net8.0-windows`), C# | E-001 [CONFIRMADO] `src/Sevenzin.Wpf/Sevenzin.Wpf.csproj` · alta |
| Entrypoints | `Sevenzin.Wpf` (app usuário, `App.xaml.cs`), `Sevenzin.Admin` (app admin **local, gitignored**), `Sevenzin.Report` (CLI) | E-002 [CONFIRMADO] `Sevenzin.sln`, `.gitignore:8` · alta |
| Motor | `Sevenzin.Diagnostics` (sem UI, testável) | E-003 [CONFIRMADO] `src/Sevenzin.Diagnostics/*.cs` · alta |
| Testes | xUnit 2.9.2, 1 projeto `tests/Sevenzin.Tests` | E-004 [CONFIRMADO] `tests/Sevenzin.Tests/Sevenzin.Tests.csproj:12` · alta |
| SDK | .NET SDK 8 local em `C:\re\geandre\dotnet` (não no PATH global) | E-005 [CONFIRMADO] `AGENTS.md:57` · alta |
| Build | `dotnet build -v q` → **0 erros, 0 avisos** | E-006 [CONFIRMADO] saída de build 17/11/2026 · alta |
| Testes (baseline) | `dotnet test` → **198/198 aprovados, 0 falhas** | E-007 [CONFIRMADO] saída de teste 17/11/2026 · alta |
| Runtime/SDK externo | Python 3.14 em `C:\Python314` (scripts de auditoria) | E-008 [CONFIRMADO] `scripts/audit_*.py` · alta |

## 2. Mapa de navegação (Sidebar — `MainWindow.Itens`)

`src/Sevenzin.Wpf/MainWindow.xaml.cs` — E-009 [CONFIRMADO] linhas 14–35 · alta.

| tag | rótulo | tela (View) | estado |
|---|---|---|---|
| inicio | Início | `InicioView` | Implementada e validada |
| ajustes | Ajustes | `AjustesView` | Implementada e validada |
| gpu | GPU | `GpuView` | Implementada, não validada |
| instaladores | Instaladores | `InstaladoresView` | Implementada, não validada |
| jogos | Jogos | `JogosView` | Implementada, não validada |
| guias | Guias | `GuiasView` | Implementada, não validada |
| notificacoes | Notificações | `NotificacoesView` | Implementada, não validada |
| bios | BIOS | `BiosView` | Implementada, não validada |
| capturas | Capturas | `CapturasView` | Implementada, não validada |
| windows | Windows | `WindowsView` | Implementada, não validada |
| midia | Mídia | `MediaView` | Implementada, não validada |
| protecao | Proteção | `ProtecaoView` | Implementada, não validada |
| config | Configurações | `ConfiguracoesView` | Implementada, não validada |
| perfil | Perfil | `PerfilView` | Implementada, não validada |
| inventario / telemetria / benchmark | — | `InventarioView`/`TelemetriaView`/`BenchmarkView` | Implementadas e validadas |
| — (removido) | Assistente | `AssistenteView` (arquivo mantido, rota removida) | Não roteada |

Rotas mortas: nenhuma (`switch` de `SelecionarAba` cobre todos os itens do array; `AssistenteView` não é mais instanciada — registro: E-010 [CONFIRMADO] `MainWindow.xaml.cs:169` removido em 17/11/2026 · alta).

## 3. Mapa de serviços (motor `Sevenzin.Diagnostics`)

| serviço | método público | dependência externa | tratamento de erro | estado |
|---|---|---|---|---|
| `MotorTweaks` | Aplicar/Desfazer/Previa/Aplicado | registro (IRegistro) | exceção → UI captura; backup JSON; auditoria | Implementada e validada |
| `CatalogoTweaks` | `Todos` (76 itens) | — | — | Implementada e validada |
| `CacheEscrita` | Listar/TweakParaDisco | WMI + registro Enum | catch → lista vazia | Implementada e validada |
| `RedePorInterface` | Listar/TweaksPara | WMI | catch → lista vazia | Implementada e validada |
| `AplicadorPerfil`/`GestorJogos`/`ControleProcessoWindows` | Aplicar/Reverter/Sincronizar | processos reais | try/catch → false + mensagem | Implementada e validada |
| `DescobridorJogos` | DetectarSteam/Epic/Tudo | arquivos/registro | catch → lista vazia | Implementada e validada |
| `CpuSets` | Listar/IdsDePerformance/Aplicar | kernel32 | catch → null/vazio | Implementada e validada |
| `CapturaTela`/`EditorCaptura` | Capturar/Editor/Renderizar | GDI+ | try/catch | Implementada e validada |
| `EnvioCaptura` | EnviarAsync | HTTP (webhook do usuário) | catch → false | Implementada e validada |
| `Assistente` | EnviarMensagemAsync | HTTP (endpoint do usuário) | catch → null | Implementada, não roteada |
| `DownloaderInstalador` | BaixarAsync | HTTP | exceção → UI | Parcial (sem staging/.part — ver F5) |
| `VerificadorDownload` | Verificar/Sha256/AssinaturaPublisher | arquivos | exceção → null | Implementada e validada |
| `WingetRunner` | InstalarAsync/ArgumentosPara | winget.exe | código de saída | Implementada e validada |
| `ExecutorDism` | AnalisarIso/MontarWim/Servico | dism.exe + PowerShell | código de saída | Implementada, não validada (sem ISO real) |
| `ConstrutorIso`/`GravadorUsb`/`UsbMedia` | Build/Gravar/Seguro | IMAPI2FS + PowerShell | resultado com erro | Implementada, não validada |
| `GestorEnergia` | AplicarSevenzin/Restaurar/NomeAtivo | powercfg | resultado com erro | Implementada e validada |
| `SegurancaSistema`/`Leitor*` | leituras de estado | registro/WMI/API | catch → null | Implementada e validada |
| `Autenticacao`/`Usuarios`/`Totp` | Autenticar/Criar/2FA | bcrypt, JSON/Mongo/Supabase | status genérico | Implementada e validada |
| `FilaToasts` | Adicionar/RemoverExpirados | — | — | Implementada e validada |

## 4. Mapa de persistência

| dado | local | formato | versionamento |
|---|---|---|---|
| contas | `%LOCALAPPDATA%\Sevenzin\usuarios.json` (ou MongoDB se `SEVENZIN_DB`) | JSON | sem versão |
| backup de tweaks | `%LOCALAPPDATA%\Sevenzin\tweaks-backup.json` | JSON (DWORD+string) | sem versão |
| auditoria | `%LOCALAPPDATA%\Sevenzin\tweaks-auditoria.log` | TSV | — |
| jogos | `%LOCALAPPDATA%\Sevenzin\jogos.json` | JSON | tolerante a campos novos |
| fila instaladores | `%LOCALAPPDATA%\Sevenzin\fila-instaladores.json` | JSON | — |
| notificações | `%LOCALAPPDATA%\Sevenzin\notificacoes.json` | JSON (máx. 200) | — |
| capturas | `%LOCALAPPDATA%\Sevenzin\capturas\*.png` | PNG | — |
| config captura/assistente/licença | `capturas-config.json`, `assistente-config.json`, `licenca.json` | JSON | — |
| downloads | `%LOCALAPPDATA%\Sevenzin\instaladores\*` | binário | **sem staging (.part)** — E-011 [CONFIRMADO] `DownloaderInstalador.cs` grava direto no destino · alta |

## 5. Baseline de qualidade (17/11/2026)

- `dotnet build -v q` → **0 erros, 0 avisos** (E-006).
- `dotnet test` → **198/198** (E-007). Nenhuma falha pré-existente.

## 6. Lacunas que impedem auditoria

| lacuna | como obter |
|---|---|
| Sem renders visuais inspecionáveis por este modelo (não processa imagens) | inspeção humana sobre `%TEMP%\opencode\renders\*.png` |
| Sem ISO real / pendrive de teste / autenticador físico para validar F7 e 2FA | hardware de teste do dono |
| Lista de serviços/tarefas e URLs/hashes de instaladores do original ficam no `#US` cifrado | indisponível — registrado como `[LACUNA]` permanente |
| Figma MCP indisponível nesta sessão | tokens já extraídos em `docs/figma/*` e `Theme.xaml` |

## 7. Conflitos registrados entre instruções do dono

| conflito | resolução adotada |
|---|---|
| Charter antigo proibia UAC/Defender off × decisão de 12/11/2026 autorizou | grupos implementados **com confirmação explícita e desfazer**; o app nunca aplica sozinho (§14.4 mantido) |
| Instrução nova "o app não desativa Defender" × autorização anterior | mantidos os itens opt-in com consentimento; **nenhuma execução automática**; divergência registrada em `validacao_paridade.md` |
