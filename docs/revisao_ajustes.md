# Revisão e ajustes do Sevenzin — itens 1 a 7

Tecnologia: WPF .NET 8 (`net8.0-windows`). Figma como fonte visual (parte das consultas ficou bloqueada pelo **limite do Figma MCP — plano Starter**; registrado onde afeta). Logo oficial "S em raio" reutilizada do asset do projeto (`Icons.xaml` → `ic_logo`), nunca redesenhada.

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
Matriz completa em [`paridade_funcional.md`](paridade_funcional.md). Conclusão: o app implementa um **subconjunto seguro e reversível** (8 tweaks com motor de prévia/backup/desfazer/auditoria + `GuardaSeguranca`) e **exclui** o conjunto perigoso do original. Não há paridade 1:1 — por projeto. Pendência: validar os tweaks aplicando em máquina real com admin.

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
