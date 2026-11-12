# Sevenzin — ferramenta de diagnóstico e otimização (reconstrução segura)

App novo e limpo que substitui o binário analisado em `C:\re\geandre`. Segue as seções 21–24 de
`07_funcionalidades.md`: **medir antes de otimizar**, somente leitura primeiro, tudo reversível e transparente.

**O que este app NÃO faz** (ao contrário do original): sem login (HWID/Discord), sem telemetria invasiva, sem ler
memória de outros processos, sem screenshot enviado a webhook, **sem desligar Defender/UAC/HVCI/mitigações**, sem
ativador do Windows, sem downloads sem verificação.

## Estado atual

Interface única em **WPF** (shell escuro alinhado ao Figma), motor desacoplado e testado.

- `src/Sevenzin.Diagnostics/` — biblioteca de coleta e lógica, **sem UI** (19 testes). Inventário somente leitura
  (cada dado com origem e confiança **Fato/Inferência/Hipótese**, sem identificadores pessoais), **topologia de CPU**
  (P/E + domínios L3 ≈ CCD), **telemetria** (sessão + média/p95/p99/desvio + overhead medido + **A/B** inconclusivo‑vs‑ruído),
  **benchmark de frametime** (FPS/1% low/p95/p99/stutter) e o **motor de tweaks** reversível com a trava de segurança
  (`GuardaSeguranca`) que recusa Defender/UAC/HVCI/mitigações/SmartScreen/Windows Update/write‑cache.
- `src/Sevenzin.Wpf/` — aplicativo **WPF**, tokens em [docs/design-system.md](docs/design-system.md). Telas prontas:
  **Início** (dashboard com monitores ao vivo + sistema), **Ajustes** (tweaks com toggles; segurança bloqueada),
  **Configurações** (abas Geral/Capturas/Preferências; "Iniciar com o Windows" real via chave Run do HKCU, sem login),
  **Inventário** (grade agrupada por categoria, confiança por cor), **Telemetria** (sessão ao vivo + resumo + A/B) e
  **Benchmark** (cena interna reproduzível) e **Jogos** (perfil reversível por jogo: prioridade — nunca Realtime — e
  afinidade por P-core, com "aplicar ao abrir / restaurar ao fechar" e busca) e **GPU** (diagnóstico de GPU/driver +
  link oficial do driver, somente leitura) e **Instaladores** (catálogo oficial com download verificado por assinatura/hash
  antes de executar, nada silencioso), **Capturas** (captura de tela local, salva no PC, nunca enviada) e **Admin**
  (painel local de auditoria dos tweaks, somente leitura). Entrada **sem login** — abre direto, sem HWID/Discord/licença.
  Tipografia **Inter/Geist** embutida e transição suave entre telas.
- `src/Sevenzin.Report/` — runner de linha de comando: `relatorio-pc.md`/`.json`, `--selftest`, `--telemetria <seg>`.
- `tests/Sevenzin.Tests/` — 19 testes do motor (relatório/inventário, topologia, telemetria + A/B, frametime, tweaks + trava).

### Rodar

```
dotnet build Sevenzin.sln -c Release
dotnet run --project src/Sevenzin.Wpf -c Release        # interface gráfica (WPF)
dotnet run --project src/Sevenzin.Report -c Release     # gera o relatório no diretório atual
dotnet test Sevenzin.sln -c Release                     # roda os testes
```

> Requer o .NET SDK 8. Neste ambiente ele está em `C:\re\geandre\dotnet` — exporte `DOTNET_ROOT` e o `PATH` para ele antes de chamar `dotnet`.

## Roadmap e otimizações

- **Plano completo, fase a fase e por tela** (com ações, desafios e soluções): [docs/roadmap-fases-telas.md](docs/roadmap-fases-telas.md).
- **Catálogo de otimizações do original** (338 escritas de registro, 110 chaves, 299 comandos), classificado
  🟢 reversível / 🟡 hardware‑A/B / 🔴 proibido: [docs/catalogo-otimizacoes.md](docs/catalogo-otimizacoes.md)
  (dados brutos em `docs/otimizacoes-originais/`).

Ordem atual: Fase 5 (consolidação) em andamento — **WinForms aposentado**; a seguir fontes/bandeja, depois **Jogos**
(perfis reversíveis por título), ampliação dos tweaks HKLM (elevação sob demanda + A/B) e **GPU** (diagnóstico seguro).

## Princípios (§24)

Medir antes de otimizar · padrões seguros · uma mudança por vez · transparência · compatibilidade detectada, não
presumida · reversibilidade testada · segurança nunca desligada · sem promessas de FPS fixo.
