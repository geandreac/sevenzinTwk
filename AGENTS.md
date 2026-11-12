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

## 🔴 Regras de segurança — operação

1. **NUNCA execute** o binário original `geandre.exe` (em `C:\re\geandre_v3\` ou `Z:\Computador\Downloads\honest*`),
   nem qualquer binário dele extraído. SHA-256 `70DC5163…AFCB72`. Só análise **estática**; use os docs já prontos.
2. **Toda ação que altera o Windows** (registro, serviços, ISO/USB, desinstalação, modo de vídeo) exige: explicação
   clara do efeito, **confirmação explícita do usuário** e **opção de desfazer** quando possível (prévia/backup/
   auditoria no `MotorTweaks`). **Nunca execute otimizações na máquina de desenvolvimento sem confirmação do dono.**
3. Senhas **nunca** em texto puro — hash **bcrypt** (`Usuarios.cs`/`Autenticacao.cs`).
4. **Nada de exfiltração oculta**: qualquer envio pela rede (assistente, captura, licença) precisa ser uma ação
   explícita e configurada pelo usuário, com consentimento por envio — nunca em segundo plano sem aviso.

> **Nota (12/11/2026):** a trava `GuardaSeguranca` (que recusava tweaks de segurança) foi **removida por decisão do
> dono** (ver `docs/revisao_ajustes.md` §4). Para restaurá-la: branch `arquivo/guardaseguranca` (commit `7091d90`).
> Os grupos avançados (UAC, Defender, etc.) estão **em escopo** e entram no catálogo com aviso, confirmação e
> reversão, conforme a regra 2 acima. O catálogo NÃO ganha item sem explicação de risco e teste.

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
tests/Sevenzin.Tests/      xUnit (101 testes)
docs/                      documentação; docs/engenharia-reversa/ = inventário do original
```

Arquitetura: a lógica fica em `Sevenzin.Diagnostics` (sem dependência de UI, 100% testável); a UI (`Sevenzin.Wpf`)
só orquestra. Tweaks são DWORDs de registro reversíveis via `MotorTweaks` (prévia → backup → aplicar → desfazer →
auditoria). O catálogo `CatalogoTweaks` é renderizado automaticamente pela `AjustesView` — adicionar um tweak
é só adicionar uma entrada ao array (com explicação de risco, aviso na UI e um teste).

## Build, testar e rodar (Windows)

Requer **.NET SDK 8**. Esta máquina tem um SDK local em `C:\re\geandre\dotnet` — se `dotnet` não estiver no PATH,
use no PowerShell: `$env:DOTNET_ROOT='C:\re\geandre\dotnet'; $env:PATH="C:\re\geandre\dotnet;$env:PATH"`.

```powershell
dotnet build src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug   # compilar o app
dotnet test                                                   # rodar os 101 testes
dotnet run --project src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug   # abrir o app
```

No 1º uso o app pede para criar o administrador (e-mail + senha forte ≥12, maiúsc/minúsc, número e símbolo).
Para semear sem UI: variáveis `SEVENZIN_ADMIN_EMAIL` e `SEVENZIN_ADMIN_SENHA`. Dados em runtime ficam em
`%LOCALAPPDATA%\Sevenzin\` (conta, backup/auditoria de tweaks, perfis de jogo, log de acessos) — fora do repositório.

## Banco de contas (JSON local ou MongoDB)

Por padrão as contas ficam num JSON local (`%LOCALAPPDATA%\Sevenzin\usuarios.json`), compartilhado pelos dois apps.
Para usar **MongoDB** (servidor), defina a string de conexão em variável de ambiente — **nunca no código**:

```powershell
setx SEVENZIN_DB "mongodb+srv://usuario:senha@cluster/...?tls=true"
setx SEVENZIN_DB_NOME "sevenzin"   # opcional; senão usa o banco da URI ou "sevenzin"
```

Se `SEVENZIN_DB` estiver definida e o servidor responder, o app usa Mongo (coleção `usuarios`) e **migra o JSON local
na 1ª vez**; se não estiver, ou o banco não responder, cai no JSON automaticamente (o app nunca trava). Senhas
continuam em **bcrypt** (só o hash vai ao banco). Use conexão **TLS** e credenciais só na env var. Código:
`src/Sevenzin.Diagnostics/RepositorioUsuariosMongo.cs` e a escolha em `src/Sevenzin.Wpf/Servicos.cs`.

## Como trabalhar aqui

- **Solução mais simples que funciona**: sem abstração/opção/config que ninguém pediu; reutilize o que já existe.
- Toda lógica nova não-trivial (branch/loop/parser/segurança) ganha **pelo menos um teste** em `tests/`.
- Antes de dizer "pronto": `dotnet build` (0 erros/avisos) e `dotnet test` (tudo verde) — e **mostre a saída**.
- Tweaks de **máquina (HKLM)** exigem admin; tweaks de **usuário (HKCU)** não. Mantenha `ExigeAdmin == (Colmeia==MaquinaLocal)`.
- Métricas são **reais**: mostre "—" quando a métrica é indisponível; **nunca** invente número (FPS/temperatura não
  existem no original — seriam feature nova, ver `docs/engenharia-reversa/05_metricas_instaladores_jogos.md`).

## Próximos passos (implementação completa, por fases)

A trava `GuardaSeguranca` foi removida por decisão do dono (12/11/2026) e o conjunto antes "fora do escopo"
(UAC/Defender, ISO/USB, assistente, etc.) passou a ser **em escopo**. O plano de fases completo e o status por
tela estão em `docs/plano-implementacao-completa.md`; cada fase só avança com build 0/0 e testes verdes, e toda
ação no Windows mantém explicação + confirmação + desfazer (regra de segurança nº 2 acima).
