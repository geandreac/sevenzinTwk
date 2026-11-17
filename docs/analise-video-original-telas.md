# Análise do vídeo do original ("Honest Tech") — telas e adaptações para o Sevenzin

**Fonte:** `Z:\Computador\Vídeos\Video do Geandre.exe.mp4` (68,3 s, 1510×880) — gravação de tela do app
original **"Honest Tech"** (o `geandre.exe` reconstruído com segurança no Sevenzin). Só **análise estática**:
o binário não foi executado; os quadros foram extraídos no navegador (a máquina não tem ffmpeg).

**Princípio (skill `ui-reference-builder`):** *inspirar e adaptar, não copiar.* As ideias abaixo entram no
**tema/Figma do Sevenzin** (cards escuros, eyebrow+título, sidebar de 66px), respeitando as regras do projeto:
tudo reversível/auditado, **nenhuma métrica inventada** (mostrar "—" quando indisponível), avisos de risco
preservados. Marco cada proposta como **[layout]** (só visual, seguro) ou **[feature]** (muda comportamento —
precisa de decisão + teste).

## 8 telas identificadas no vídeo

| # | Tela original | Equivalente no Sevenzin | Tempo no vídeo |
|---|---|---|---|
| 1 | **Início** (dashboard) | `InicioView` | 0–21 s |
| 2 | **GPU** | `GpuView` | 3–36 s |
| 3 | **Ajustes** | `AjustesView` | 24–27 s |
| 4 | **Limpeza** | *(não existe tela dedicada)* | 30–33 s |
| 5 | **Instaladores** | `InstaladoresView` | 39–45 s |
| 6 | **Biblioteca** (jogos) | `JogosView` | 47–51 s |
| 7 | **BIOS** | `BiosView` | 54–59 s |
| 8 | **Configurações** | `ConfiguracoesView` | 62–68 s |

## Linguagem visual do original (comum a todas)
- Fundo quase preto; cards `#0E0E0E` sobre preto; **eyebrow** em caps pequeno (ex.: "FIRMWARE") acima de um
  **título bold grande**; subtítulo de uma linha logo abaixo.
- Sidebar estreita só de ícones (já é o padrão do Sevenzin).
- **Toasts** no canto superior (ex.: "Perfil LATENCY aplicado.").
- Estado aplicado **inline com desfazer** ("Aplicado agora · Desfazer", "Aplicado: Latência · Remover perfil").
- Dados em linha com **valor alinhado à direita** (BIOS: `Clock .......... 3,80 GHz`).

## Tela a tela

### 1. Início (dashboard)
**Original:** header "Início" + "Windows 11 Pro 25H2 · monitorando em tempo real" + selo **"Otimizado · 9 de 9
ajustes ativos"**; fileira de ~4 tiles de métrica; **gráfico de linha** que cresce ao vivo; grande **ilustração
de ventoinha/turbina** no canto inferior; 2 botões no topo-direito.
**Proposta Sevenzin:** [layout] trazer o **selo de estado** ("Otimizado · N de N ajustes ativos") como chip
no header; [layout] padronizar os **stat tiles** (valor grande + rótulo pequeno), usando o `dataviz` só com
**métricas reais** (uso de CPU/RAM/disco que já lemos) e "—" quando indisponível; [layout] a ilustração
decorativa pode virar um gráfico/medidor real (ex.: sparkline de uso), não um enfeite estático.

### 2. GPU
**Original:** duas colunas — à esquerda **toggles de driver** ("AJUSTES DO DRIVER · ENERGIA E FUNCIONAMENTO":
Economia de energia, Power Gating, Driver Perf, Auto NVIDIA Debloat [Abrir]); à direita um **grid de perfis
gráficos** selecionáveis por rádio ("PERFIL GRÁFICO · ESCOLHA UM": Básico/Casual/Competitivo/FPS/FPS 2.0/
Latência/Avançado) com **borda no selecionado**; card de **detecção da GPU** ("RTX 4060 Ti · Detectada") no
topo-direito; rodapé com estado aplicado ("Aplicado: Latência · Ray Tracing desligado") + "Remover perfil".
**Proposta:** [layout] card de **GPU detectada** no topo-direito (o Sevenzin já lê o fabricante — reusar);
[layout] grid de **perfis como cartões-rádio** com borda de seleção (mais claro que lista). [feature] os
perfis NVIDIA em si só entram com explicação de risco, confirmação e reversão (regra 2 do AGENTS).

### 3. Ajustes
**Original:** "9 de 9 ativos · Windows e Debloat não têm reversão"; seções nomeadas (**SISTEMA**, **ENTRADA E
REDE**) com itens que mostram **"Aplicado agora · Desfazer"** inline (ex.: "Raw input — Desliga a precisão
aprimorada do ponteiro").
**Proposta:** [layout] o `CatalogoTweaks`/`AjustesView` já é por seções — adotar o **chip "Aplicado · Desfazer"
por item** e o resumo "N de N ativos" no header. O aviso "não têm reversão" deve ficar visível onde se aplica.

### 4. Limpeza  *(tela que o Sevenzin NÃO tem)*
**Original:** "Remove temporários e caches. Nenhum arquivo pessoal." + seção "O QUE LIMPAR".
**Proposta:** [feature] avaliar uma tela/card de Limpeza **só leitura + confirmação** (listar o que seria
removido, tamanho, e limpar só com OK e com aviso) — fora do escopo atual; decisão do dono antes de implementar.

### 5. Instaladores
**Original:** "Programas essenciais, sem sair do Honest." com **pills de filtro com contagem**:
Todos 56 · Sistema 10 · Drivers 10 · Plataformas 6 · Jogos 3 · Monitor…
**Proposta:** [layout] adicionar **pills de categoria com contagem** no topo da `InstaladoresView` (hoje é uma
lista única); agrupa o catálogo grande e melhora a varredura. Categoria é só filtro visual — seguro.

### 6. Biblioteca (Jogos)
**Original:** grade de **cards com capa (cover art)** dos jogos; abrir um jogo abre um **modal "OTIMIZAÇÕES DO
JOGO / CS2"** com capa + "Trocar capa", **grade de toggles em 2 colunas** (Otimização de GPU, Compatibilidade,
SMT, P-cores, Um CCD, Prioridade alta, Boost, CORE 0 — cada um com subtexto de estado) + **"EXECUTÁVEL DO
JOGO"** (caminho + Selecionar) + **Reverter/Aplicar**.
**Proposta:** [layout] trocar os chips inline da `JogosView` atual por um **modal de perfil por jogo** com os
toggles em **grade 2 colunas** + caminho + Reverter/Aplicar — bem mais legível que a fileira de chips (que já
tivemos de consertar). [layout] cards com capa são bons, mas [feature] buscar/baixar capa automaticamente sai
do escopo; manter capa opcional/manual. Os toggles mapeiam direto nos que a `JogosView` já tem.

### 7. BIOS
**Original:** "FIRMWARE / BIOS — Exporte, revise e gerencie as configurações do firmware."; coluna de ações
**numeradas** ("01 / REVISAR": Exportar, Ver Config., Abrir Pasta; "02 / CONFIGURAR": Auto Config, Importar,
Entrar BIOS); card de **specs** (CPU AMD Ryzen 7 5800X, Cores/Threads, Clock, Placa Mãe, Modelo, Versão) com
valores à direita; rodapé "Alterações no firmware exigem cuidado. Revise antes de importar."
**Proposta:** [layout] adotar as **etapas numeradas** (01 Revisar / 02 Configurar) e o **card de specs** com
valores à direita na `BiosView`. [feature] "Importar/Entrar BIOS" são ações sensíveis — manter informativo +
confirmação + aviso (a `BiosView` do Sevenzin já é informativa/segura).

### 8. Configurações
**Original:** "Deixe o Honest do seu jeito, dentro e fora da tela." + seção "INICIALIZAÇÃO".
**Proposta:** [layout] a `ConfiguracoesView` já tem abas; alinhar os rótulos de seção (INICIALIZAÇÃO etc.) e o
tom do subtítulo. Pouca mudança.

## Prioridade sugerida (do mais barato/seguro ao mais trabalhoso)
1. **[layout] Instaladores — pills de categoria com contagem** (ganho alto, risco baixo).
2. **[layout] Início — selo "Otimizado · N de N" + stat tiles padronizados** (só métricas reais).
3. **[layout] GPU — card de GPU detectada + grid de perfis como cartões-rádio.**
4. **[layout] Jogos — modal de perfil por jogo (grade 2 col)** em vez dos chips inline.
5. **[layout] BIOS — etapas numeradas + card de specs à direita.**
6. **[layout] Ajustes — chip "Aplicado · Desfazer" por item + resumo "N de N".**
7. **[feature] Limpeza** (tela nova) — só com decisão do dono.

## Limitações / notas
- Não copiar textos, capas (CS2) nem a identidade do original; recriar com o tema/Figma do Sevenzin.
- Métricas do dashboard: só reais; nada de FPS/temperatura (não existem no original — ver
  `docs/engenharia-reversa/05_metricas_instaladores_jogos.md`).
- Quadros extraídos com um servidor local temporário (o dono subiu) + `.frames/` (gitignored).
