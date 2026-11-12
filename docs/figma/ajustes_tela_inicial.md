# Ajustes da tela inicial — fidelidade ao Figma

Arquivo Figma: **Projeto Sevenzin 2026** · fileKey `SxC2LoJThQfhbyqQtuTf56`.
Frame de referência: **`3:949` — "02 · Início"** (1280×740). Valores extraídos via Figma MCP (`get_metadata`, `get_screenshot`, `download_assets`) e amostragem de pixel dos PNGs exportados.

## Nodes consultados
| Node | Nome | Medida (px) |
|---|---|---|
| `3:949` | 02 · Início (frame) | 1280×740 |
| `3:950` | Sidebar/Recolhida (instância) | **x0 · 66×740** |
| `3:994` | Indicador ativo | x0 y105 · **3×22** |
| `3:996` | "Início" (título da barra superior) | x90 y17 · 35×17 |
| `3:997` | Controles da janela | x1150 · 120×48 |
| `3:1004` | "OTIMIZAÇÃO" (eyebrow) | x113 y80 · 63×12 |
| `3:1005` | "Início" (título da página) | x116 y104 · 64×**29** |
| `3:1006` | "Windows · Monitorando em tempo real" | x113 y136 · 237×16 |
| `3:1007` | "MONITORES AO VIVO" | x113 y170 · 104×12 |
| `3:1008/15/22/29` | Monitor/CPU·GPU·Memória·Latência | **272×118**, x=110/396/682/968 (gap 14) |
| `3:1012` (etc.) | Sparkline | x15 y80 · **242×26** |
| `3:1036` | Tag/Estável (badge) | x212 y12 · 48×16 (texto "Estável") |
| `3:1039` | Sistema (faixa) | x110 y342 · **1130×62** |
| `3:1044/47/50` | Divisores verticais | **1×38**, x=282/564/846 |
| `3:1052/62` | Recurso/Ajustes de driver · Perfil por jogo | **558×174**, x=110/682 (gap 14) |
| `3:1054/64` | Scrim do cartão de recurso | x0 y60 · 558×114 |

### Layout dentro do monitor (coords locais do card 272×118)
label x17 y15 (12px) · valor x17 y36 (**31px alt → ~26px**) · unidade x53 y48 (16px) · sparkline x15 y80 (242×26) · legenda x17 y102 (12px).

### Faixa Sistema (1130×62)
4 colunas a cada ~282px (CPU x21, GPU x303, RAM x585, SO x867); label y11 (12px) sobre valor y29 (16px); divisores 1×38 em y12.

## Cores (amostradas dos PNGs do Figma — batem com os tokens já existentes)
- Fundo da tela: `#05080E` (`BgBase`); sidebar `#070B13` (`BgSidebar`).
- Cartão (monitor/sistema): fill `#0B111C` (`Surface1`), borda `#1A2536` (`BorderDefault`), raio ~12.
- Sparkline: azul de marca `#2F8CFF` (`Brand500`), ~1.6px.
- **Degradê do cartão de recurso** (diagonal topo-esquerda→baixo): amostras —
  TL `#1A2B44` · centro `#0A111B` · BR/rodapé `#010203`.
  Reproduzido com `LinearGradientBrush` (0,0)→(1,1), stops `#1A2B44` / `#0A111B` (0.5) / `#010203`.
- Logo: glifo "S-raio" `#2F8CFF`, asset oficial node `3:2898` (SVG em `Icons/figma/logo.svg`, geometria `ic_logo`).

## Divergências registradas (justificadas)
1. **Itens extras na sidebar** (Inventário, Telemetria, Benchmark, Admin): não constam no Figma, mas são telas funcionais do produto (Admin é exigido pelo fluxo de login). Mantidos como ícones; os 9 primeiros (Início…Configurações) seguem o design exatamente.
2. **Cartão de recurso**: no Figma a camada base é uma imagem colocada ("Imagem: …"); sem raster/SVG exportável (`download_assets` retornou vazio). Reproduzido com o degradê amostrado (cores exatas do Figma), não com cor sólida.
3. **"Estável"**: o badge do Figma é estático; aqui reflete a medição real (mostra "Estável" quando o ping responde sem perda; "—/indisponível" quando falha).

## Arquivos alterados
| Arquivo | Mudança |
|---|---|
| `src/Sevenzin.Wpf/MainWindow.xaml` | Coluna da sidebar 260→**66px**; topo = logo `ic_logo` centralizado + botão hambúrguer; só-ícones; ponto verde no rodapé (sem texto "Local · sem login"); barra superior com título à esquerda + controles à direita. |
| `src/Sevenzin.Wpf/MainWindow.xaml.cs` | Nav só-ícone com `ToolTip` + `AutomationProperties.Name`; rótulos recolhíveis; `Menu_Click` expande (66↔210); `TituloBarra` acompanha a tela. |
| `src/Sevenzin.Wpf/Theme.xaml` | Tokens `ResGradTop/Mid/Bot`; estilo `Eyebrow`; `NavItem` reescrito (indicador lateral 3×22 na borda, destaque sutil, ícone de marca no ativo, estado desabilitado). |
| `src/Sevenzin.Wpf/Icons.xaml` | `ic_logo` (asset oficial `3:2898`). |
| `src/Sevenzin.Wpf/Views/InicioView.xaml(.cs)` | Padding do conteúdo 44/40; eyebrow "OTIMIZAÇÃO"; título 24px; valor dos cards 26px; badge "Estável" que some sem rede; faixa Sistema com divisores verticais e tooltip do nome completo; cartões de recurso com degradê de 3 stops do Figma, altura 160. |

## Ajustes por área (checklist do pedido)
1. **Sidebar/navegação** ✔ 66px, só-ícones, tooltips no hover, foco por teclado (ListBox) + nome por leitor de tela; ícones oficiais do Figma; estados padrão/hover/ativo/desabilitado; indicador lateral + destaque do ícone (sem pílula larga).
2. **Logo/topo** ✔ glifo oficial `ic_logo` (#2F8CFF) no lugar do quadrado "S" + texto; hambúrguer incluído; "Local · sem login" removido (mantido só o ponto verde de status).
3. **Barra superior/cabeçalho** ✔ barra 48px com título "Início" à esquerda e controles; eyebrow "OTIMIZAÇÃO"; título reduzido para 24px (altura Figma 29).
4. **Layout/espaçamentos** ✔ conteúdo reposicionado após a sidebar estreita; margens 44/40 (frame x110/1240).
5. **Cartões de monitoramento** ✔ hierarquia valor/unidade/legenda/sparkline/selo; sparkline azul na base; GPU indisponível mostra "—" sem gráfico; nomes longos com reticências + tooltip.
6. **Seção Sistema** ✔ faixa compacta (padding 11) com 3 divisores verticais; nomes completos no tooltip; valores reais do inventário.
7. **Cartões de recurso** ✔ retângulo raio 12; degradê diagonal recriado com as cores amostradas do Figma (não cor sólida, não aproximação inventada).
8. **Cores/tipografia/tokens** ✔ cores batem com os tokens existentes; gradiente e eyebrow centralizados em `Theme.xaml`.

## Testes e comandos
- `dotnet build src\Sevenzin.Wpf` → **0 erros, 0 avisos**.
- `dotnet run -- --render app_inicio.png inicio` → PNG gerado e comparado com `fig_inicio.png` (frame `3:949`).
- `dotnet test` → **67/67 aprovados** (regressão ok).

## Comparação visual e divergências restantes
- Render `app_inicio.png` corresponde ao frame Figma em sidebar, logo, barra superior, cabeçalho, cartões, faixa Sistema e cartões de recurso.
- **Divergências documentadas**: (1) 4 ícones extras na sidebar (Inventário/Telemetria/Benchmark/Admin) — telas funcionais fora do Figma; (2) cartão de recurso reproduz o degradê amostrado (a camada base do Figma é imagem colocada, sem asset exportável); (3) badge "Estável" é dinâmico (some quando o ping falha).
- **Limitação técnica**: `get_variable_defs` do MCP remoto exige seleção no Figma desktop; medidas/cores foram extraídas por `get_metadata` + amostragem de pixel dos PNGs exportados, que são os valores do próprio arquivo Figma.

