# AGENTS.md — Sevenzin

Contexto permanente para qualquer agente de IA (OpenCode, etc.) que trabalhe neste repositório.
**Leia este arquivo inteiro antes de agir.** Responda sempre em **português do Brasil**.

## O que é

Sevenzin é a **reconstrução segura, em WPF (.NET 8)**, de um otimizador de PC Windows (`geandre.exe` /
"Honest" / "Fps.Assistant") que foi analisado por engenharia reversa estática. Princípio do produto:
**medir antes de otimizar** — só leitura primeiro, tudo **reversível, auditado e confirmado** pelo usuário.

A análise completa do original está em [`docs/engenharia-reversa/`](docs/engenharia-reversa/) (7 fases + `metricas.json`),
o roadmap em [`docs/engenharia-reversa/roadmap_implementacao.md`](docs/engenharia-reversa/roadmap_implementacao.md) e o
mapa de paridade em [`docs/paridade_funcional.md`](docs/paridade_funcional.md). Comece por esses.

## 🔴 Regras de segurança — INEGOCIÁVEIS

1. **NUNCA execute** o binário original `geandre.exe` (em `C:\re\geandre_v3\` ou `Z:\Computador\Downloads\honest*`),
   nem qualquer binário dele extraído. SHA-256 `70DC5163…AFCB72`. Só análise **estática**; use os docs já prontos.
2. O app **nunca replica** o conjunto perigoso do original: desligar Defender/UAC/HVCI/mitigações/SmartScreen/Windows
   Update/write-cache; login por HWID/Discord; captura→webhook; `SetSystemTime`; flash de BIOS; construtor de ISO/debloat.
   A trava `GuardaSeguranca` (`src/Sevenzin.Diagnostics/Tweaks.cs`) recusa qualquer tweak que toque em segurança —
   **não a enfraqueça**. Todo tweak novo passa por ela e tem teste.
3. Senhas **nunca** em texto puro — hash **bcrypt** (`Usuarios.cs`/`Autenticacao.cs`).
4. Nada de telemetria/exfiltração: o app não envia dados do usuário para fora da máquina.

## 🔴 Autoria no Git — INEGOCIÁVEL

O único nome em Contributors é **`geandreac`**. **NUNCA** adicione `Co-Authored-By`, "Generated with …" nem
qualquer atribuição a IA (Claude, Deepseek, OpenCode) em commits, PRs, issues ou comentários. Use a identidade git
global (`geandreac` / `geandreac.dev@gmail.com`); não use `--author` com outro nome. Remoto: `github.com/geandreac/sevenzinTwk`.

## Estrutura

```
Sevenzin.sln
src/Sevenzin.Wpf/          app WPF (.NET 8, Windows): Views/, MainWindow, LoginWindow, Theme.xaml
src/Sevenzin.Diagnostics/  motor SEM UI (testável): Tweaks, Autenticacao, Usuarios, Coletores, Jogos, Gpu, Capturas…
src/Sevenzin.Report/       runner de linha de comando (relatorio-pc.md/.json)
tests/Sevenzin.Tests/      xUnit (85 testes)
docs/                      documentação; docs/engenharia-reversa/ = inventário do original
```

Arquitetura: a lógica fica em `Sevenzin.Diagnostics` (sem dependência de UI, 100% testável); a UI (`Sevenzin.Wpf`)
só orquestra. Tweaks são DWORDs de registro reversíveis via `MotorTweaks` (prévia → backup → aplicar → desfazer →
auditoria). O catálogo `CatalogoTweaks` é renderizado automaticamente pela `AjustesView` — adicionar um tweak seguro
é só adicionar uma entrada ao array (e um teste).

## Build, testar e rodar (Windows)

Requer **.NET SDK 8**. Esta máquina tem um SDK local em `C:\re\geandre\dotnet` — se `dotnet` não estiver no PATH,
use no PowerShell: `$env:DOTNET_ROOT='C:\re\geandre\dotnet'; $env:PATH="C:\re\geandre\dotnet;$env:PATH"`.

```powershell
dotnet build src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug   # compilar o app
dotnet test                                                   # rodar os 85 testes
dotnet run --project src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug   # abrir o app
```

No 1º uso o app pede para criar o administrador (e-mail + senha forte ≥12, maiúsc/minúsc, número e símbolo).
Para semear sem UI: variáveis `SEVENZIN_ADMIN_EMAIL` e `SEVENZIN_ADMIN_SENHA`. Dados em runtime ficam em
`%LOCALAPPDATA%\Sevenzin\` (conta, backup/auditoria de tweaks, perfis de jogo) — fora do repositório.

## Como trabalhar aqui

- **Solução mais simples que funciona**: sem abstração/opção/config que ninguém pediu; reutilize o que já existe.
- Toda lógica nova não-trivial (branch/loop/parser/segurança) ganha **pelo menos um teste** em `tests/`.
- Antes de dizer "pronto": `dotnet build` (0 erros/avisos) e `dotnet test` (tudo verde) — e **mostre a saída**.
- Tweaks de **máquina (HKLM)** exigem admin; tweaks de **usuário (HKCU)** não. Mantenha `ExigeAdmin == (Colmeia==MaquinaLocal)`.
- Métricas são **reais**: mostre "—" quando a métrica é indisponível; **nunca** invente número (FPS/temperatura não
  existem no original — seriam feature nova, ver `docs/engenharia-reversa/05_metricas_instaladores_jogos.md`).

## Próximos passos (roadmap §5, todos seguros)

1. Enumeração de modos de vídeo (DEVMODE/Hz) — só leitura.
2. Uso de CPU do sistema (%) na Home — `GetSystemTimes`.
3. Working set / `timeBeginPeriod` opt-in nos perfis de jogo, com aviso e reversão ao fechar.
4. Fila de instaladores persistida (JSON local).
