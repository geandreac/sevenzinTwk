# Estudo — Prontidão para jogos (0–100%) na tela Início

Data: 18/11/2026 · Status: **proposta, nada implementado**

## 1. A ideia

Um medidor na tela Início, de 0 a 100%, mostrando quanto do que é **recomendado para jogos neste PC** já está
aplicado, por componente (Windows, CPU, GPU, Rede, Mouse, Energia). Um botão **"Otimizar para jogos"** aplica de uma
vez tudo o que falta, com prévia, confirmação única, backup e "Desfazer tudo".

## 2. O que o número significa (e o que NÃO significa)

Regra do projeto: métricas são reais, nunca inventadas. Portanto:

- O número é **"ajustes recomendados aplicados"**, não "desempenho" nem "FPS". Rótulo proposto:
  **"Prontidão para jogos · 72%"** com o subtítulo "18 de 25 ajustes recomendados ativos".
- Ganho real de FPS só se afirma com medição: a tela Benchmark (`BenchmarkFrametime`) já existe e pode ser oferecida
  como "medir antes" / "medir depois" do botão.

## 3. O que já existe e é reaproveitado

| Peça | Onde | Uso no medidor |
|---|---|---|
| Selo "N de N ajustes ativos" | `InicioView.ContarAjustes` | É o embrião: hoje conta **todos** os itens sem confirmação, incluindo visuais e de privacidade, que não afetam jogos. Seria substituído. |
| 81 ajustes com leitura de estado | `CatalogoTweaks` + `MotorTweaks.Aplicado` | Fonte principal: estado real lido do registro. |
| Aplicar / desfazer com backup e auditoria | `MotorTweaks.Aplicar/Desfazer` | Reaproveitado sem mudar. |
| Plano de energia | `GestorEnergia` (`GuidAtivo`, plano "SevenzinTwks") | Item "Plano de energia de desempenho ativo". |
| TCP global | `RedeGlobal.EstadoAtual/Aplicar/Restaurar` | Item de rede. |
| Grupos de driver da GPU | `GpuDriverTweaks.GrupoAplicado/DesativarGrupo` | Itens de GPU por fabricante. |
| Fabricante e idade do driver | `DriverGpu.Detectar`, `DriverGpu.PareceAntigo` | Aplicabilidade e recomendação de atualizar (sem aplicar sozinho). |

Ou seja: **quase tudo já sabe ler o próprio estado**. O trabalho novo é escolher os itens e os pesos, somar e mostrar.

## 4. Quais ajustes entram

Três níveis. Só os dois primeiros contam no medidor.

### Nível 1 — Necessário (aplicado pelo botão)
Risco baixo, benefício claro para jogos, reversível.

| Componente | Itens (id no catálogo) |
|---|---|
| Windows | `gamedvr-off`, `gamedvr-captura-off`, `fast-startup-off` |
| CPU / agendamento | `mmcss-games-gpu`, `mmcss-games-prio`, `mmcss-games-sfio`, `mmcss-games-sched` |
| Mouse | `mouse-aceleracao-off`, `mouse-limiar1-off`, `mouse-limiar2-off` |
| Energia | plano "SevenzinTwks" (alto desempenho) ativo |
| GPU AMD | `amd-chill-off` (só se a GPU for AMD) |

### Nível 2 — Recomendado (aplicado só no modo "Completo", que precisa ser marcado)
Risco médio: o próprio catálogo diz "meça A/B".

| Componente | Itens |
|---|---|
| CPU | `mmcss-responsiveness`, `priority-separation` (valor 38; **nunca** junto com `priority-separation-26`) |
| GPU | `hags` (exige reinício e GPU compatível), grupos de driver NVIDIA/AMD de `GpuDriverTweaks` |
| Rede | TCP global (`RedeGlobal`), `rede-throttle-off` |

### Fora do medidor (nunca aplicado pelo botão)
- **Segurança** (UAC, Defender, SmartScreen, HVCI, Spectre/Meltdown, Windows Update) — risco ALTO/CRÍTICO, continua só
  na tela Ajustes com confirmação individual.
- `timer-global-1ms` (ALTO, consumo de energia), `tdr-delay` (mascara travas), memória
  (`paginacao-off`, `large-cache-on` — podem piorar com pouca RAM), power gating AMD (calor/consumo).
- Visual e privacidade: não mudam desempenho em jogos; continuam disponíveis em Ajustes.
- Atualizar driver: vira **pendência informativa** ("Driver de vídeo com mais de 18 meses → Auto NVIDIA Debloat"),
  nunca é executado pelo botão (baixa ~1 GB e reinstala o driver).

## 5. Cálculo

```
aplicáveis = itens dos níveis 1 e 2 que valem para este hardware
             (AMD só com GPU AMD; NVIDIA só com NVIDIA; HAGS só com GPU compatível)
prontidão  = Σ peso(aplicados) / Σ peso(aplicáveis) × 100
pesos      = Necessário 3 · Recomendado 1
```

- Item que não deu para ler (sem permissão, chave ausente) **não conta** em nenhum dos lados e aparece como "—".
- Arredondar para baixo: 99,6% mostra 99%, só mostra 100% quando está tudo aplicado.
- Por componente, a mesma conta, para as barras pequenas.

## 6. Interface proposta (tela Início)

Substitui o selo atual "N de N ajustes ativos", no topo:

```
┌──────────────────────────────────────────────────────────────┐
│  ◔ 72%   Prontidão para jogos                               │
│          18 de 25 ajustes recomendados ativos                │
│                                                              │
│  Windows ██████████ 100%     GPU     ████░░░░░░  40%         │
│  CPU     ███████░░░  75%     Rede    ░░░░░░░░░░   0%         │
│  Mouse   ██████████ 100%     Energia ░░░░░░░░░░   0%         │
│                                                              │
│  ⚠ Driver de vídeo com 20 meses — Auto NVIDIA Debloat →     │
│                                                              │
│  [ Otimizar para jogos ]   ☐ Incluir recomendados (Completo) │
│                            Ver detalhes · Desfazer tudo      │
└──────────────────────────────────────────────────────────────┘
```

- Anel em WPF puro (`Path` com `ArcSegment`), cores do tema: vermelho < 40, amarelo < 80, verde ≥ 80.
- Animação do anel até o valor ao abrir a tela (via `Movimento`, respeitando "reduzir movimento").
- "Ver detalhes" abre a lista item a item: estado, risco e "por que importa".
- Acessibilidade: o anel tem nome acessível ("Prontidão para jogos, 72 por cento"); cada barra também.

Pesquisa de referências (passo da skill `ui-reference-builder`): não feita, porque Magic UI, React Bits e 21st.dev são
componentes React/Tailwind e não se aplicam ao WPF. O desenho segue os componentes da casa: cartões, `SectionLabel` e
`PrimaryButton` de `Theme.xaml`.

## 7. "Ativar automaticamente": como fica dentro das regras

A regra 2 do projeto exige explicação, confirmação e desfazer. "Automático" então significa **um clique, nunca
sozinho em segundo plano**:

1. **Prévia**: o botão abre a lista do que vai mudar, por componente, com risco e "requer reinício" quando for o caso
   (`MotorTweaks.Previa`).
2. **Uma confirmação** para o lote todo (a de Segurança não entra nesse lote).
3. **Aplicação** com backup individual pelo `MotorTweaks`; o plano de energia e o TCP pelos seus próprios módulos.
4. **Resultado**: "21 aplicados, 1 falhou (motivo)", o medidor relê o estado real e um registro vai para Notificações.
5. **"Desfazer tudo"** restaura o lote inteiro a partir do backup.
6. **Desvio**: ao abrir o app, se o medidor caiu (por exemplo, o Windows Update reverteu ajustes), aparece um aviso
   em Notificações: "3 ajustes voltaram ao padrão — reaplicar?". Sem reaplicar sozinho.

## 8. Implementação (quando aprovado)

| Fase | Entrega | Arquivos |
|---|---|---|
| 1 | Motor puro: lista de itens (id, componente, nível, peso, aplicável/aplicado/aplicar/desfazer), cálculo, testes | `Sevenzin.Diagnostics/ProntidaoJogos.cs`, `tests/.../ProntidaoJogosTests.cs` |
| 2 | Cartão na Início com anel, barras e "Ver detalhes" (só leitura) | `InicioView.xaml.cs` |
| 3 | Botão com prévia, lote, "Desfazer tudo" e registro em Notificações | `InicioView`, `ProntidaoJogos` |
| 4 | Aviso de desvio ao abrir o app e atalho "medir antes/depois" no Benchmark | `App.xaml.cs`, `BenchmarkView` |

Fases 1 e 2 sozinhas já entregam o medidor (sem mexer no Windows). Cada fase fecha com build 0/0 e testes verdes.

## 9. Decisões em aberto (do dono)

1. Nome: "Prontidão para jogos", "Pontuação gamer" ou outro?
2. Os pesos 3/1 e a divisão entre Necessário e Recomendado acima estão bons?
3. O modo "Completo" (nível 2) deve vir desmarcado por padrão? (proposta: sim)
4. Notebook na bateria: o plano de alto desempenho conta como pendência ou é ignorado? (proposta: ignorado na bateria)
