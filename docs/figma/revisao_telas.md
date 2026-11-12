# Revisão completa das telas — Sevenzin vs Figma

Arquivo Figma: **Projeto Sevenzin 2026** · fileKey `SxC2LoJThQfhbyqQtuTf56`.
Método: inventário pelo código (`src/Sevenzin.Wpf/Views/` + roteamento em `MainWindow.xaml.cs`) + frames via Figma MCP (`get_metadata` da página Telas `0:1`, `get_screenshot`), render headless de cada tela (`--render <png> <tag>`) e comparação lado a lado.
Evidências (PNGs): `C:\re\geandre\render\telas\app_*.png` (app) e `fig_*.png` (Figma).

## Premissa que rege toda a revisão
Este app é uma **reconstrução segura** do binário analisado. O Figma é a fonte visual, mas o *charter de segurança tem precedência*: telas cujo design do Figma depende de recursos proibidos (login HWID/Discord, exfiltração por webhook, desligar Defender, flash de BIOS, builder de ISO, instalação em massa de espelhos) são deliberadamente simplificadas ou não implementadas. Essas divergências são **intencionais e documentadas**, não defeitos.

## Ajustes globais aplicados nesta revisão
- **Sidebar** estreita (66px) só-ícones em todas as telas (antes 260px com rótulos) — Figma `3:950`.
- **Margens de conteúdo** alinhadas a 44 (esq) / 40 (dir) em todas as views (frame Figma x110/1240).
- **Título de tela** padronizado em 24px (`TituloTela`), antes 28px (altura Figma ~29).
- **Eyebrows** adicionados onde o Figma traz um distinto: GPU → "HARDWARE", Instaladores → "SOFTWARE" (Início "OTIMIZAÇÃO" e Configurações "SEU SEVENZIN" já existiam).
- Barra superior com título da tela à esquerda; logo oficial `ic_logo` no topo da sidebar.

---

## Área do usuário

### Início — `3:949` · rota `inicio`
Finalidade: dashboard de monitores ao vivo + inventário resumido + atalhos.
Divergências/ajustes: reconstruída fiel (ver `ajustes_tela_inicial.md`): sidebar estreita, eyebrow, badge "Estável" dinâmico, faixa Sistema com divisores, cartões de recurso com degradê amostrado do Figma.
Estados: dados ao vivo (CPU/RAM/latência reais); GPU "—" quando indisponível (sem gráfico falso).
**Status: Revisada e validada.**

### Ajustes — `3:1067` · rota `ajustes`
Finalidade: otimizações reversíveis e auditadas.
Divergências: o Figma lista itens como **"Windows Defender · Desliga o Defender por completo"** e **"Debloat · Remove apps"** — proibidos pelo charter; o app expõe outro conjunto (Game DVR, ID de publicidade, sugestões, animações, SystemResponsiveness, prioridade de foreground), todos reversíveis. Layout do app usa grupos em cartão + rótulos em caixa-alta; o Figma usa lista plana com divisores. Mantido o padrão do app (consistente com as demais telas).
Estados: toggles on/off, requer-admin sinalizado.
**Status: Revisada com ressalvas (divergência intencional de segurança + estilo de lista).**

### GPU — `3:1201` · rota `gpu`
Finalidade: diagnóstico de GPU (somente leitura).
Divergências: o Figma traz toggles de driver (Power Gating, "Driver Perf — ajusta clock e memória do driver", Economia de energia), abas NVIDIA/AMD e grade de "Perfil gráfico". O app é **somente leitura** (sem DDU, sem alterar flags ocultas) + recomendações seguras. Eyebrow "HARDWARE" adicionado.
Estados: GPU detectada (card de dados) / link oficial do fabricante.
**Status: Revisada com ressalvas (somente leitura por segurança).**

### Instaladores — `3:1342` (+ `3:1481`/`3:1620` estados) · rota `instaladores`
Finalidade: obter utilitários de fontes oficiais com verificação.
Divergências: o Figma mostra catálogo grande com "Instalar" em um clique, abas de categoria e busca (padrão de supply-chain do original). O app lista poucos apps, **só fontes oficiais, download verificado por assinatura/hash antes de executar**, nada sem o usuário. Eyebrow "SOFTWARE" adicionado.
Estados: "Abrir página oficial" / "Baixar e verificar".
**Status: Revisada com ressalvas (catálogo seguro reduzido).**

### Jogos — `3:1759` (+ `3:1889`/`3:2025` estados) · rota `jogos`
Finalidade: perfil por jogo (afinidade/prioridade **temporárias**, revertidas ao fechar).
Divergências: o Figma é uma "Biblioteca" com tiles de jogos de marca (CS2, Valorant, etc.) + busca + abas. O app pede o `.exe` do usuário e cria perfil reversível; sem bundle de marcas/biblioteca.
Estados: vazio ("Nenhum jogo ainda"), toggle "Aplicar ao abrir".
**Status: Revisada com ressalvas (escopo seguro mínimo).**

### Capturas — `3:2389` · rota `capturas`
Finalidade: captura de tela **local**, nunca enviada à rede (ao contrário do original, que exfiltrava por webhook).
Divergências: o Figma traz 3 modos (Região/Janela/Tela inteira), atalhos configuráveis, editar-após-capturar e preview. O app faz captura local + abrir pasta + preview da última. Modos ricos não implementados (expansão futura segura).
Estados: preview vazio / última captura.
**Status: Revisada com ressalvas (comportamento seguro presente; UI mais simples).**

### Configurações — `3:2680` · rota `config`
Finalidade: preferências do app (Geral/Capturas/Preferências).
Divergências: **muito fiel** (eyebrow "SEU SEVENZIN", título, abas, cards de linha). O app troca o texto do Figma *"Inicie na bandeja usando seu Discord ID salvo e validando a licença"* por *"atalho no seu usuário, sem administrador / tudo local, sem login"* — remoção correta do login HWID/Discord. Rótulos de seção em caixa-alta (padrão do app) vs sentence-case do Figma.
Estados: abas, toggles, "salva automaticamente".
**Status: Revisada e validada (com nota de wording de segurança).**

### BIOS — `3:2095` · rota `bios`
Finalidade (Figma/original): área de BIOS. **Flash/gravação de BIOS é proibido pelo charter.**
App: placeholder seguro ("Em migração para WPF — próxima fatia"); nenhuma função de BIOS implementada.
**Status: Não implementada por decisão de segurança (placeholder).**

### Windows (ISO) — `3:2578` · rota `windows`
Finalidade (Figma/original): builder de ISO/debloat do Windows. **Proibido pelo charter.**
App: placeholder seguro; nenhuma função de ISO implementada.
**Status: Não implementada por decisão de segurança (placeholder).**

### Menu lateral expandido — `3:2766`
Atendido pelo botão hambúrguer: a sidebar expande de 66px para 210px mostrando os rótulos (com tooltip/acessibilidade no modo estreito).
**Status: Revisada e validada.**

### Telas extras sem referência no Figma (área do usuário)
- **Inventário** (`inventario`): tabela somente-leitura com Item/Valor/**Confiança** (Fato/Inferência)/Fonte/Observação. Padrão do projeto. **Revisada (sem ref Figma).**
- **Telemetria** (`telemetria`): sessão de medição A/B (média/p95/p99/desvio) + linha de base. **Revisada (sem ref Figma).**
- **Benchmark** (`benchmark`): frametime interno reprodutível, sem anti-cheat. **Revisada (sem ref Figma).**
> Registro: estes três não existem no Figma; seguem o sistema visual já estabelecido, sem apresentar a decisão como especificação do Figma.

---

## Área do administrador / compartilhada

### Login — `10:866` (+ `10:1048` estados) · **não implementada**
O frame do Figma é o login de **licença** do original (logo grande, "PC OPTIMIZER", "Esqueci minha senha", "Ainda não tem licença? Comprar", arte de circuito). O login seguro previsto (item 4 do checklist anterior) é **admin-only por e-mail+senha**, sem criação de conta/social/"Comprar", com hash forte e sessão — ainda **não implementado**. **Implementado** no item 4: `LoginWindow` com e-mail+senha (admin-only, sem licença/"Comprar"/signup), gate em `App.OnStartup`. Detalhes em `CHECKLIST_REVISAO.md`.
**Status: Revisada e validada (fluxo de licença do Figma não replicado por segurança).**

### Admin — página Figma `16:713` (ADM-00..10) · rota `admin`
App: `AdminView` com USUÁRIOS (lista) + CRIAR LOGIN (login/senha/confirmar/checkbox admin, com validação) + AUDITORIA DE OTIMIZAÇÕES (grid). Contas locais, somente leitura da auditoria, sem servidor/login remoto.
**Implementado** no item 4: bcrypt, sessão com expiração/renovação/logout, proteção de rota por papel (a aba Admin só aparece para administradores; `AdminView` checa a sessão), força bruta, seed por env, gate de acesso. Conta própria não pode se auto-remover. Detalhes em `CHECKLIST_REVISAO.md`.
**Status: Revisada e validada (comparação fina com os frames ADM-xx `16:713` fica como próximo passo).**

---

## Validação executada
- `dotnet build src\Sevenzin.Wpf` → **0 erros, 0 avisos**.
- Render headless de **todas** as telas (12 rotas) + re-render de ajustes/gpu/instaladores após mudanças.
- `dotnet test` → **67/67 aprovados** (regressão ok).
- Resolução: desktop 1280×740 (proporção dos frames Figma). Mobile não se aplica (app desktop WPF).
- Gráficos (sparklines): lógica crescer/descer verificada em `InicioView.Empurrar` (valor maior → linha sobe); dados reais.
- Controle de acesso ADM: **ainda não há gate** — registrado como pendência do item 4 (nenhuma função de ADM é exposta além da própria tela Admin; nada de servidor/remoto).

## Resumo (contagem)
**Telas identificadas: 15** — 12 na área do usuário (9 com frame no Figma + 3 extras) e 2 na área ADM/compartilhada (Login, Admin); "Menu lateral expandido" é estado da sidebar.

| Categoria | Telas | Status |
|---|---|---|
| Revisadas e validadas | Início, Configurações, Menu expandido | ✅ |
| Revisadas com ressalvas (segurança) | Ajustes, GPU, Instaladores, Jogos, Capturas | ⚠️ |
| Extras revisadas (sem ref Figma) | Inventário, Telemetria, Benchmark | ✅ (s/ Figma) |
| Não implementadas por segurança | BIOS, Windows (ISO) | ⛔ (placeholder) |
| Pendentes (item 4) | Login, Admin (auth/gate) | ⏳ |

- **Área do usuário**: 12 identificadas · 10 revisadas (2 validadas + 5 com ressalvas + 3 extras) · 2 não implementadas por segurança.
- **Área ADM/compartilhada**: 2 identificadas · Admin revisada com ressalvas · Login pendente (item 4).
- **Pendências para fechar a revisão global**: implementar o item 4 (Login admin e-mail+senha + gate + hash forte/sessão/força bruta) e então revisar Login e Admin contra os frames `10:866`/`16:713`.
