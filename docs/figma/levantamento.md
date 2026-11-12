# Levantamento do Figma (via Figma MCP) — fonte da verdade visual

Arquivo: **Projeto Sevenzin 2026** · fileKey `SxC2LoJThQfhbyqQtuTf56` · acesso confirmado (`whoami`: geandre, admin).
Extraído via Figma MCP (`get_metadata`, `download_assets`). Atualizado conforme a Etapa 0 avança.

## Páginas (node IDs)
- `0:1` Telas — telas do app (Login, Início, GPU, Instaladores, Jogos, BIOS, Capturas, Windows, Configurações, Menu lateral).
- `3:2` Componentes — Sidebar, Toggle, Checkbox, Logo, Controles da janela, Indicador ativo.
- `10:1118` Paleta & Guia — paleta e guia de estilo.
- `16:713` Admin — fluxo ADM-00..10 (usuários, criar login, validação, acesso negado, variantes tablet/celular).

## Componentes (página 3:2)
| Componente | Node ID | Tamanho |
|---|---|---|
| Toggle (On `3:3` / Off `3:6`) | `3:9` | 112×32 |
| Checkbox (On `3:10` / Off `3:14`) | `3:16` | 76×32 |
| Sidebar/Recolhida | `3:17` | 66×740 |
| Sidebar/Expandida | `3:61` | 212×740 |
| Controles da janela | `3:119` | 120×48 |
| Indicador ativo | `3:126` | 3×22 |
| Logo/Sevenzin | `3:2898` | 24×24 |

## 1. Ícones da Sidebar (de `3:17` Sidebar/Recolhida)
Spec do Figma (todos): **viewBox 20×20, traço `stroke-width=1.6`, caps/joins redondos, cor padrão `#A9B6C8`** (`icon/default/secondary`).
SVGs reais salvos em `src/Sevenzin.Wpf/Icons/figma/`; geometrias no módulo único `src/Sevenzin.Wpf/Icons.xaml`.

| Item do app | Ícone (Figma) | Node ID | Chave WPF |
|---|---|---|---|
| Início | home | `3:25` | `ic_inicio` |
| Ajustes | sliders | `3:28` | `ic_ajustes` |
| GPU | gpu | `3:33` | `ic_gpu` |
| Instaladores | download | `3:38` | `ic_instaladores` |
| Jogos | game | `3:41` | `ic_jogos` |
| BIOS | bios | `3:45` | `ic_bios` |
| Capturas | camera | `3:51` | `ic_capturas` |
| Windows | mouse | `3:54` | `ic_windows` |
| Configurações | gear | `3:57` | `ic_config` |
| (recolher) | Menu | `3:23` | `ic_menu` |
| Status de licença | ellipse verde `#22C55E` | `3:60` | — |

Estados: **padrão** `#A9B6C8`; **ativo** azul de marca (texto+ícone acompanham `Foreground` do item, via binding); **hover** fundo `surface/2`.
Itens do app que **não existem no Figma** (Inventário, Telemetria, Benchmark, Admin): ícones criados no mesmo estilo (0–20, 1.6).

## 2. Tipografia
**Inteiramente Inter** (confirmado pelo usuário e pelos textos do Figma), variando por peso (Thin/Normal/Medium/SemiBold/Bold).
Aplicado: `UiFont` e `HeadingFont` = Inter. Fontes embutidas em `src/Sevenzin.Wpf/Fonts/` (Inter-Regular, Inter-Bold; OFL).
> Pendência: extrair a **escala exata** (tamanho/line-height/letter-spacing por token) — `get_variable_defs` do MCP remoto exige um
> layer **selecionado no Figma desktop** (retornou "nothing selected"); fazer com a seleção, ou confirmar os tamanhos por inspeção dos frames.

## 3. Logo/Ícone Sevenzin
Node `3:2898` (24×24) e instância na sidebar `3:2908` (22×22). É um "S" com raio, cor `#2F8CFF`. SVG salvo em `Icons/figma/logo.svg`.
> A fazer: aplicar o SVG oficial no logo da sidebar/título/login (hoje a sidebar usa um "S" em caixa — trocar pelo vetor do Figma).

## 4. Admin/Login (página 16:713)
Frames do fluxo (nomes): ADM-00 Fluxo/validação/segurança, ADM-01 Usuários, ADM-02 Criar login (vazio), ADM-03 (preenchido),
ADM-04 (erros de validação), ADM-05 Confirmar criação (modal), ADM-06 Login criado (credenciais), ADM-07 Acesso negado,
ADM-08 Tablet, ADM-09/10 Celular.
Tela de **Login de acesso** (entrar): em Telas — `01 · Login` e `01b · Login (estados de campo e botão)`.
> A fazer: extrair node IDs e estados exatos do frame de Login (campos e‑mail/senha, foco, erro, carregando) e implementar o backend
> (controller/service/repository, bcrypt/argon2, sessão/token, força bruta, seed por env) com testes — item 4 do checklist.

## Não existe no Figma (registrar)
- "Esqueci minha senha": **a verificar** nos frames de Login antes de implementar (só implementar se existir).
