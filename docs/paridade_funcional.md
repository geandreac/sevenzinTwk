# Paridade funcional — Sevenzin vs programa original (`geandre.exe`)

Fonte: análise estática já produzida — [`catalogo-otimizacoes.md`](catalogo-otimizacoes.md) e [`otimizacoes-originais/`](otimizacoes-originais/) (338 escritas de registro, 110 chaves, 299 comandos). **O executável não foi executado.** O `honest (2)/geandre.exe` é byte‑idêntico ao já analisado (SHA256 `70dc5163…`).

## Como ler a classificação
**a)** Implementada e validada (com teste automatizado ou evidência) · **b)** Implementada, não validada · **c)** Parcial · **d)** Ausente · **e)** Incompatível/não aplicável (proibida pelo charter ou fora do escopo seguro).

> **Resumo honesto:** o Sevenzin **não busca paridade 1:1** com o original. Ele reconstrói um **subconjunto seguro e reversível** de otimizações e **exclui** deliberadamente o que é perigoso. A maioria das categorias de tweak do original é, portanto, **parcial, ausente ou incompatível por projeto** — não por esquecimento.

## 1. Otimizações (tweaks de registro/sistema)

| Funcionalidade no original | App novo | Classe | Evidência / nota |
|---|---|---|---|
| Win32PrioritySeparation (foreground) | `priority-separation` (HKLM, A/B) em Ajustes | **b** | `CatalogoTweaks`; aplica/desfaz/auditado; efeito real exige admin (não validado automaticamente) |
| SystemResponsiveness (MMCSS) | `mmcss-responsiveness` em Ajustes | **b** | idem |
| HAGS (HwSchMode) | `hags` em Ajustes | **b** | idem |
| Fast Startup off (HiberbootEnabled) | `fast-startup-off` em Ajustes | **b** | idem |
| GameDVR/Game Bar off | `gamedvr-off` em Ajustes | **b** | HKCU, sem admin |
| ID de publicidade off | `advertising-id-off` | **b** | HKCU |
| Sugestões de conteúdo off | `sugestoes-off` | **b** | HKCU |
| Animações da barra off | `taskbar-animations-off` | **b** | HKCU |
| Motor reversível (prévia, backup, desfazer, auditoria) + trava de segurança | `MotorTweaks` + `GuardaSeguranca` | **a** | testes automatizados do motor e da guarda |
| Perfil MMCSS "Games" (GPU Priority etc.) | — | **d** | não implementado |
| Afinidade/prioridade por jogo (per‑título) | Jogos: perfil por `.exe`, **temporário** | **c** | escopo reduzido e reversível (não aplica afinidade permanente) |
| IFEO PerfOptions por processo | — | **d** | risco em processos do sistema |
| TdrDelay / Preferência de GPU / Power gating AMD / AMD DVR‑Chill | — | **d** | não implementados (risco/baixo retorno) |
| Overclock de clock/memória do driver | — | **e** | não é "otimização segura" (charter) |
| Limpeza de driver (DDU) | GPU é **somente leitura** | **e** | ação destrutiva; app não usa DDU |
| Plano de energia (~100 `powercfg /setacvalueindex`) | Windows: recomenda "Alto desempenho" | **c** | sem os 100 índices sem proveniência |
| Rede/latência (TcpAckFrequency, throttling, offloads…) | — | **d** | A/B dependente de NIC; não implementado |
| Memória/armazenamento (DisablePagingExecutive, MMAgent) | — | **d** | debatível; não implementado |
| Write cache forçado (`CacheIsPowerProtected`) | — | **e** | risco de perda de dados (charter) |
| DPC/MSI/IRQ affinity | — | **d** | mascara instabilidade; não implementado |
| Serviços/tarefas não essenciais off | — | **d** | não implementado como toggles |
| Telemetria/privacidade (AllowTelemetry, Copilot, WER…) | parte (ID publicidade, sugestões) | **c** | subconjunto |
| Visual/UI (efeitos, transparência, tema, ponteiro) | parte (animações, GameDVR) | **c** | subconjunto |
| Debloat/Appx (remover apps, OneDrive, Edge) | — | **e** | alto impacto; charter cauteloso |
| Construtor de ISO/debloat do Windows | Windows: **excluído**, com aviso | **e** | instalação de SO; proibido pelo charter |

### 🔴 Conjunto proibido do original (Defender/UAC/HVCI/Spectre/Exploit Protection/SmartScreen/Windows Update/write‑cache)
Classe **e** para todos. O Sevenzin **nunca aplica**: `GuardaSeguranca` recusa qualquer tweak que toque nesses mecanismos (verificado antes de qualquer escrita); as telas Windows/BIOS deixam isso explícito.

## 2. Telas / fluxos

| Tela/fluxo | App | Classe | Evidência |
|---|---|---|---|
| Login + Admin (contas, papéis, auditoria) | Implementado | **a** | 11 testes de autenticação; render `v3/v4` |
| Início — monitores CPU/Memória/Latência | Implementado | **a** | leituras reais; render |
| Início — monitor **GPU (%)** | Implementado | **b** | `LeitorGpuUso` (GPU Engine); lê valor real; sob carga = validação manual |
| Ajustes (8 tweaks, motor reversível) | Implementado | **a**/**b** | motor testado; efeito por tweak exige admin |
| GPU (diagnóstico só leitura) | Implementado | **a** | WMI; render |
| Instaladores (fontes oficiais, assinatura) | Implementado | **c** | fluxo seguro; catálogo reduzido |
| Jogos (perfil por `.exe` temporário) | Implementado | **c** | escopo seguro |
| Capturas (local, sem webhook) | Implementado | **b** | captura local; modos ricos ausentes |
| BIOS / Windows (informativas) | Implementado | **a** | estado só‑leitura real + instruções; render `v6` |
| Configurações / Inventário / Telemetria / Benchmark | Implementado | **b** | funcionais; render |

## Pendências / lacunas registradas
- Validar os 8 tweaks aplicando/desfazendo em máquina real com admin (efeito no registro) — hoje só o **motor** é testado automaticamente.
- Monitor de GPU: confirmar que o `%` acompanha carga real (jogo/benchmark) — teste manual.
- Categorias **d** (rede, memória, DPC/MSI, serviços) não foram implementadas por decisão de risco/retorno; adicionar só com A/B e reversão testada, se o produto exigir.
- Nenhuma funcionalidade foi declarada "paridade completa" apenas por existir tela/botão: a coluna Classe reflete o que executa e é verificável.
