# Revisão e ajustes do Sevenzin — itens 1 a 7

Tecnologia: WPF .NET 8 (`net8.0-windows`). Figma como fonte visual (parte das consultas ficou bloqueada pelo **limite do Figma MCP — plano Starter**; registrado onde afeta). Logo oficial "S em raio" reutilizada do asset do projeto (`Icons.xaml` → `ic_logo`), nunca redesenhada.

## §4. Remoção da trava `GuardaSeguranca` (12/11/2026)

**Decisão do dono do repositório** (confirmada em conversa, após alerta sobre o conflito com a regra antiga do
AGENTS.md): remover a trava `GuardaSeguranca` e passar o conjunto antes "fora do escopo" (UAC/Defender, ISO/USB,
assistente, etc.) para **em escopo**, implementado por fases com explicação, confirmação explícita e reversão.

### O que foi removido
| item | local | ação |
|---|---|---|
| Classe `GuardaSeguranca` (lista `Proibidos` + método `Proibido(Tweak)`) | `src/Sevenzin.Diagnostics/Tweaks.cs` | removida |
| Chamada `GuardaSeguranca.Proibido(t)` em `MotorTweaks.Aplicar` (lançava `InvalidOperationException`) | `src/Sevenzin.Diagnostics/Tweaks.cs` | removida |
| Teste `Trava_recusa_tweak_de_seguranca` | `tests/Sevenzin.Tests/TweaksTests.cs` | removido |
| Seção estática "AVANÇADO (BLOQUEADO)" + método `LinhaBloqueada` (não era mais verdadeira) | `src/Sevenzin.Wpf/Views/AjustesView.xaml.cs` | removidos |

### O que foi desacoplado / preservado
- `MotorTweaks` **não foi alterado** além da chamada removida: prévia, backup (`tweaks-backup.json`), desfazer e
  auditoria (`tweaks-auditoria.log`) continuam intactos — a reversibilidade agora é a única barreira, e permanece
  obrigatória em todo caminho de escrita.
- Teste `Catalogo_nao_tem_nenhum_tweak_de_seguranca` foi **adaptado** (lista proibida movida para o próprio teste,
  como guarda transitória até os grupos avançados entrarem no catálogo com testes próprios). Demais testes de
  tweaks inalterados.

### Como restaurar a trava no futuro
- Branch dedicada: **`arquivo/guardaseguranca`** (aponta para `7091d90`, último commit com a trava íntegra).
- `git diff master arquivo/guardaseguranca -- src/Sevenzin.Diagnostics/Tweaks.cs` mostra exatamente o trecho removido.
- Ou `git revert` do commit de remoção (ver `git log --oneline -- src/Sevenzin.Diagnostics/Tweaks.cs`).

### Verificação após a remoção
- `dotnet build` → **0 erros, 0 avisos**.
- `dotnet test` → **100/100 aprovados** (o teste da trava foi o único removido).
- `grep GuardaSeguranca` no código: nenhuma referência viva (só comentários históricos datados).

### Consequência registrada
Sem a trava, **qualquer** `Tweak` do catálogo é aplicável, inclusive os de segurança, quando forem adicionados.
Por isso as fases seguintes tratam esses itens como ação de risco: aviso na descrição, confirmação explícita na UI
e desfazer pelo motor — conforme a regra de segurança nº 2 do `AGENTS.md` (nunca executar na máquina de
desenvolvimento sem confirmação do dono).

## §5. Fase 1 — Segurança avançada + tela Proteção (12/11/2026)

**F1.1 — tweaks de segurança com confirmação + tela Proteção** (commit `a08654b`)
- `Tweak.ExigeConfirmacao` (novo campo opcional): itens de risco pedem confirmação explícita na UI.
- 7 tweaks novos no `CatalogoTweaks` (categoria "Segurança", todos HKLM/admin/confirmação, risco ALTO/CRÍTICO):
  `uac-off` (EnableLUA=0), `defender-rt-off`, `defender-antispyware-off`, `smartscreen-off` (política
  EnableSmartScreen=0), `hvci-off` (CRÍTICO, requer reinício), `mitigacoes-off` (FeatureSettingsOverride),
  `wu-pausar` (NoAutoUpdate).
- Leitores só-leitura em `SegurancaSistema.cs`: UAC, Defender tempo real, SmartScreen, HVCI, mitigações,
  atualizações automáticas (+ Secure Boot/TPM já existentes) + `Interpretar` puro.
- Nova tela `ProtecaoView` + item "Proteção" na navegação (ícone `ic_protecao`): estado real por mecanismo +
  toggle do tweak correspondente com confirmação; avisos de reinício e de Tamper Protection.
- `AjustesView`: diálogo de confirmação para `ExigeConfirmacao` + risco na descrição.

**F1.2 — write-cache por disco** (commit `d30a1d8`)
- `CacheEscrita.cs`: descoberta dos discos via WMI (`Win32_DiskDrive`) → resolução do nó
  `HKLM\SYSTEM\CurrentControlSet\Enum\<PnP>\<instância>\Device Parameters` → tweak dinâmico por disco
  (`CacheIsPowerProtected=1`, Id = SHA-256 do PnP) aplicado pelo `MotorTweaks` (backup/desfazer/auditoria).
- Seção "WRITE-CACHE (por disco)" na `ProtecaoView` com estado atual e aviso de perda de dados.

**Arquivos alterados:** `Tweaks.cs`, `SegurancaSistema.cs`, `CacheEscrita.cs` (novo), `AjustesView.xaml.cs`,
`ProtecaoView.xaml/.cs` (novos), `MainWindow.xaml.cs`, `Icons.xaml`, `TweaksTests.cs`,
`SegurancaSistemaTests.cs` (novo), `CacheEscritaTests.cs` (novo), `plano-implementacao-completa.md` (novo),
`paridade_funcional.md`, `roadmap_implementacao.md`, `roadmap-fases-telas.md` (marcado histórico), `AGENTS.md`, `README.md`.

**Testes:** `dotnet build` 0/0 · `dotnet test` **109/109** (conformidade dos itens de segurança, motor
aplica/desfaz UAC e write-cache no registro fake, interpretação pura, leitores determinísticos, vínculo
tela↔catálogo).

**Pendências / validação manual:**
- Render visual da `ProtecaoView` e dos novos itens de Ajustes (F8).
- Efeito real de cada item exige administrador e, para UAC/HVCI/mitigações, **reinício** — validar em máquina
  controlada, um por vez, com desfazer.
- Defender com Tamper Protection ativa ignora os valores — a UI avisa, mas o efeito real depende do ambiente.
- Plano por fases: `docs/plano-implementacao-completa.md` (F0 ✅ · F1.1 ✅ · F1.2 ✅ · F2 a F8 a seguir).

## §6. Fase 2 — Capturas completas (12/11/2026)

**F2.1 — modos de captura · F2.2 — editor de anotação · F2.3 — envio explícito** (um único ciclo; commit abaixo)
- Motor (`Capturas.cs` ampliado): `CapturarArea` (região/monitor), `CapturarJanela` (GetWindowRect), `JanelasVisiveis`
  (EnumWindows com título e tamanho mínimo), `Monitores` (EnumDisplayMonitors) e `AreaTelaInteira` (virtual screen)
  — removido o fallback antigo que inventava 1920×1080.
- `EditorCaptura.cs` (novo): `Anotacao` (lápis/retângulo/seta/texto), `Renderizar` puro (GDI+, nunca altera a base)
  e `PilhaAnotacoes` com desfazer/limpar.
- `EnvioCaptura.cs` (novo): `UrlWebhookValida` (só HTTPS absoluta), `ConfigCapturasStore` (JSON local atômico,
  padrão **sem webhook**) e `EnviarAsync` (POST multipart, handler injetável).
- UI: `CapturasView` reescrita (seletor de modo + ComboBox de monitores + toolbar de ferramentas + desenho com
  mapeamento de escala + salvar/copiar/enviar); `RegiaoOverlay` (arrastar região, Esc cancela); `JanelaPicker`
  (lista de janelas); `PromptTexto` (texto da anotação, tema escuro); aba Capturas das Configurações com campo de
  webhook (salvar/limpar, validação HTTPS) — botão Enviar só existe com URL configurada e confirma cada envio.

**Arquivos alterados:** `Capturas.cs`, `EditorCaptura.cs` (novo), `EnvioCaptura.cs` (novo), `CapturasView.xaml/.cs`
(reescritos), `RegiaoOverlay.xaml/.cs` (novos), `JanelaPicker.xaml/.cs` (novos), `PromptTexto.cs` (novo),
`ConfiguracoesView.xaml/.cs`, `Theme.xaml` (estilo `DarkCombo`), testes novos: `CapturasModosTests`,
`EnvioCapturaTests`.

**Testes:** `dotnet build` 0/0 · `dotnet test` **118/118** (enumeração consistente de janelas/monitores/área,
render de anotações por pixels, pilha de desfazer, validação de URL, persistência/config corrompida, POST com
handler fake 2xx/5xx/rede).

**Pendências / validação manual:** render visual e captura real nos 4 modos (F8); envio real contra um webhook de
teste do usuário; suavidade do desenho a lápis em imagens grandes (re-render por ponto).


## Resumo por item
| # | Item | Status |
|---|---|---|
| 1 | Menu lateral recolhido | ✅ feito e validado (render) |
| 2 | Monitor de GPU | ✅ corrigido (leitura real) · validação sob carga = manual |
| 3 | Paridade funcional | ✅ matriz em `paridade_funcional.md` |
| 4 | Telas Windows e BIOS | ✅ implementadas (informativas/seguras), validadas |
| 5 | Ícone do aplicativo | ✅ gerado da logo oficial e aplicado |
| 6 | Animações e transições | ✅ implementadas (menu + telas) com "reduzir movimento" |
| 7 | Rolagem e scrollbar | ✅ scrollbar escura global; conteúdo acessível |

## Item 1 — Menu lateral recolhido
**Causa:** o `NavItem` pintava `Background = BrandTint` no estado selecionado (a "pílula" azul).
**Correção:** removido o fundo; a seleção agora é só o **marcador lateral (3×22)** + ícone/texto de marca; ícone centralizado na coluna de 66px; hover (`Surface2`) e foco por teclado preservados. Arquivo: `Theme.xaml` (estilo `NavItem`). Evidência: render `v6_inicio` (ativo sem pílula).

## Item 2 — Monitor de GPU
**Diagnóstico:** o `%` da GPU mostrava "—" porque **não havia sensor de uso** — só detecção (WMI `Win32_VideoController`); o card nunca era atualizado ("fase posterior").
**Correção:** novo `LeitorGpuUso` (`src/Sevenzin.Diagnostics/LeitorGpuUso.cs`) soma a utilização das engines **3D** dos contadores de performance **"GPU Engine"** (Win10+). Devolve `null` → "—" com tooltip de motivo quando o contador não existe (Windows antigo/sem suporte); **nunca força 0%**. Ligado em `InicioView.TickAsync`.
**Testes:** `GpuUsoTests` (agregação 3D, limite 100, sem‑3D=0). Render mostrou leitura real (0% ocioso, distinto de "—").
**Não verificado:** acompanhamento sob carga real (jogo/benchmark) — teste manual pendente.

## Item 3 — Paridade funcional
Matriz completa em [`paridade_funcional.md`](paridade_funcional.md). Conclusão histórica (antes de 12/11/2026): o app implementava um **subconjunto seguro e reversível** (8→16 tweaks com motor de prévia/backup/desfazer/auditoria + `GuardaSeguranca`) e **excluía** o conjunto perigoso do original. Pendência: validar os tweaks aplicando em máquina real com admin.

## Item 4 — Telas Windows e BIOS
Antes eram placeholders. Agora:
- **BIOS/UEFI** (`Views/BiosView`): estado **só leitura** real — BIOS/placa (WMI), **Secure Boot** (registro `SecureBoot\State`, só leitura) e **TPM** (WMI `Win32_Tpm`) — + instruções manuais classificadas por risco (entrar no setup = informativo; XMP/EXPO, boot/Secure Boot e flash de BIOS = ⚠ atenção) + salvaguardas.
- **Windows** (`Views/WindowsView`): estado só leitura (edição/build/arquitetura) + recomendações **reversíveis** que apontam para Ajustes (prévia, uma por vez, desfazer, auditoria) + nota de que o **construtor de ISO/debloat** do original foi removido.
- Leitores só‑leitura: `src/Sevenzin.Diagnostics/SegurancaSistema.cs`.
**Classificação de ações:** informativa (entrar no setup, ler estado), reversível (recomendações via Ajustes), potencialmente disruptiva (XMP/EXPO), alto risco (flash de BIOS, mudar Secure Boot). **Nenhuma ação altera BIOS/Windows automaticamente.** Sem elevação oculta, bypass de UAC, comandos ofuscados ou desativação de Defender.
**Reversão:** mudanças reais só existem em Ajustes, com desfazer + backup. BIOS é 100% manual pelo usuário.
Evidência: render `v6_bios` (Secure Boot ativo, TPM habilitado), `v6_windows`.

## Item 5 — Ícone do aplicativo
Gerado da logo oficial `ic_logo` (#2F8CFF), multi‑tamanho (16/24/32/48/64/128/256) em `src/Sevenzin.Wpf/Assets/sevenzin.ico` via `IconeGerador` (`--makeicon`). Aplicado em:
- **Executável**: `<ApplicationIcon>` no `.csproj`.
- **Janela/barra de tarefas**: `Icon="pack://…/Assets/sevenzin.ico"` em `MainWindow` e `LoginWindow`.
- **Bandeja/instalador**: não aplicável ainda (não há ícone de bandeja nem instalador no projeto) — registrar quando existirem.
Proporção e cor preservadas (Viewbox uniforme). Build final com o ícone: 0 erros.

## Item 6 — Animações e transições
- **Menu expandir/recolher**: largura da sidebar animada (66↔210, 180ms, `CubicEase`) em `MainWindow.Menu_Click` (coluna `Auto` + `Border` com `Width` animável).
- **Troca de telas**: fade + leve slide (`AnimarEntrada`), já existente, mantido.
- **Reduzir movimento**: ambos checam `SystemParameters.ClientAreaAnimation` (reflete "Mostrar animações no Windows"); quando desligado, aplicam o estado final sem animação. Sem animações decorativas em massa.
**Não verificado por render** (é movimento): confirmar visualmente a suavidade e o modo reduzido — teste manual.

## Item 7 — Rolagem e scrollbar
**Causa:** as telas usavam a **scrollbar nativa clara** do Windows, destoando do tema (barra branca no Admin).
**Correção:** estilo global de `ScrollBar` escuro em `Theme.xaml` (trilho transparente; indicador arredondado `BorderStrong` → `TextMuted` no hover → `TextSecondary` ao arrastar; vertical 10px e variante horizontal; clique no trilho pagina). Aplicado implicitamente a todos os `ScrollViewer`. Barra superior e sidebar permanecem fixas; só o conteúdo rola. Não especificado no Figma → seguiu o tema (registrado).
**Rolagem aninhada:** a grade de auditoria do Admin tem rolagem interna limitada (tabela), ao final da página; o restante do formulário fica no scroll externo e acessível. Teclado/roda/trackpad preservados (comportamento padrão do `ScrollViewer`).
Evidência: render `v6_admin` (scrollbar escura, sem barra branca).

## Testes e build
- `dotnet build` (WPF + Diagnostics) → **0 erros, 0 avisos**.
- `dotnet test` → **84/84 aprovados** (inclui `GpuUsoTests` novos e os de autenticação/tweaks).
- Pacotes adicionados: `System.Diagnostics.PerformanceCounter` 8.0.1 (GPU), `BCrypt.Net-Next` 4.2.0 (já antes).
- Renders desktop (1280×760): `v6_inicio`, `v6_admin`, `v6_bios`, `v6_windows`, `v3/v4/v5` (login/admin). Mobile não se aplica (desktop WPF).

## Limitações / pendências (validação manual)
- GPU sob carga real; suavidade das animações e modo "reduzir movimento"; aplicação/desfazer real dos 8 tweaks (admin).
- Verificação visual fina do Admin contra os frames ADM e a scrollbar contra o Figma: **bloqueada pelo limite do Figma MCP** (Starter) — refazer quando resetar.
- Ícone de bandeja e instalador: não existem no projeto ainda.
