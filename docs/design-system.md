# Design system do Sevenzin (extraído do Figma)

Fonte: Figma "Projeto Sevenzin 2026" (página **Paleta & Guia**). Tema **escuro**, preto azulado de base, azul de ação.
Tipografia do projeto: **Inter** (UI) e **Geist / Geist Mono** (as fontes já vinham embutidas no binário original).

## Paleta (variáveis "Sevenzin · Cores")

| Token | Hex | Uso |
|---|---|---|
| bg/base | `#05080E` | fundo da janela |
| bg/sidebar | `#070B13` | fundo da navegação lateral |
| surface/1 | `#0B111C` | cartões/painéis |
| surface/2 | `#101826` | superfície elevada |
| surface/3 | `#172238` | superfície mais alta / hover |
| border/default | `#1A2536` | bordas |
| border/strong | `#26364F` | bordas de destaque |
| text/primary | `#E8ECF7` | texto principal |
| text/secondary | `#A8B0C0` | texto secundário |
| text/muted | `#7B8AA0` | texto apagado |
| text/disabled | `#4A5670` | desabilitado |
| brand/400 | `#5AA9FF` | azul claro |
| brand/500 | `#2F8CFF` | **azul de ação principal** |
| brand/600 | `#1E6FE0` | azul hover/pressionado |
| brand/700 | `#1656B8` | azul escuro |
| accent/cyan | `#38BDF8` | destaque ciano |
| brand/tint | `#0E2340` | tinta de marca (fundo sutil) |
| status/success | `#22C55E` | sucesso (= "Fato") |
| status/warning | `#F5B841` | atenção (= "Inferência") |
| status/danger | `#F05252` | erro/risco |
| status/success-bg | `#0A2A1A` | fundo de sucesso |

Mapeamento para a confiança atual do app: Fato → status/success, Inferência → status/warning, Hipótese → text/muted.

## Telas no Figma (página "Telas")

Shell com **navegação lateral** (menu lateral expandido/recolhido), não abas no topo.

- 01 · Login (+ 01b estados de campo e botão)
- 02 · Início (dashboard)
- 03 · Ajustes (página inteira)
- 04 · GPU
- 05 · Instaladores (+ 05b rolado: Monitoramento, + 05c filtro Drivers)
- 06 · Jogos (+ 06b hover no card + busca, + 06c Otimizados/vazio)
- 07 · BIOS
- 09 · Capturas
- 10 · Windows (ISO)
- 11 · Configurações
- 12 · Menu lateral expandido
- Páginas à parte: **Componentes**, **Admin**, **Paleta & Guia**

## Observação de escopo (carta segura §23)

Algumas telas do Figma mapeiam para recursos de risco do original que **não** entram na reconstrução segura:
BIOS (flash), Windows (ISO/debloat de instalação), e qualquer desligar‑segurança. O shell visual e as telas
seguras (Início, Jogos/otimização, Configurações, e as nossas Inventário/Telemetria/Benchmark/Otimizações) seguem.
