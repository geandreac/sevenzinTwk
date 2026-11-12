# Comparação detalhada — Tela Admin vs frames ADM do Figma

Arquivo Figma: `SxC2LoJThQfhbyqQtuTf56` · página **Admin `16:713`**.
Fonte: metadados reais do Figma (node IDs, tamanhos e textos) já baixados via `get_metadata` nesta sessão.

> **Limitação desta rodada:** o Figma MCP atingiu o **limite de chamadas do plano Starter** ("You've reached the Figma MCP tool call limit"), então **não foi possível capturar screenshots novos** para conferência de cor/pixel/tipografia. A comparação abaixo é **estrutural** (layout, campos, medidas e textos — dados reais do arquivo). A verificação visual fina fica pendente até o limite resetar ou o plano ser atualizado. Nada de cor/medida foi improvisado.

## Frames ADM (node IDs)
| Node | Frame | Tamanho |
|---|---|---|
| `16:2233` | ADM-00 · Fluxo, validação e segurança | 2680×1040 |
| `16:714` | ADM-01 · Usuários | 1280×800 |
| `16:891` | ADM-02 · Criar login (vazio) | 1280×968 |
| `16:1055` | ADM-03 · Criar login (preenchido) | 1280×942 |
| `16:1222` | ADM-04 · Criar login (erros de validação) | 1280×1104 |
| `16:1404` | ADM-05 · Confirmar criação (modal) | 1280×942 |
| `16:1593` | ADM-06 · Login criado (credenciais) | 1280×800 |
| `16:1812` | ADM-07 · Acesso negado (não administrador) | 1280×800 |
| `16:1858` / `16:1993` / `16:2100` | ADM-08/09/10 · Tablet / Celular | 768 / 390 |

## ADM-01 "Usuários" vs app
| Elemento (Figma, real) | No app hoje | Classificação |
|---|---|---|
| **Sidebar ADM própria** (66px: users/key/log/gear + indicador) | Reusa a sidebar principal com 1 item "Admin" | Divergência arquitetural — manter abordagem do app (uma sidebar só); registrar |
| Barra superior "Administração / Usuários" + **Selo/Administrador** (avatar) | Barra com e-mail do usuário + "Sair" | Parcial — app mostra o usuário logado, sem avatar/selo |
| Título **"Usuários"** + subtítulo + botão **"Novo login"** (topo-direita) | Eyebrow "ADMINISTRAÇÃO" + título "Admin" + "Criar login" inline | Alinhar texto do título ("Usuários"); botão primário no topo |
| **4 cartões de métrica**: Total (128) · Ativos (117) · Expiram em 7 dias (6) · Bloqueados (5) | Nenhum | "Total/Ativos" = seguro (contagem real); "Expiram em 7 dias" = **licença (charter — não temos)** |
| **Busca** (360×40) + **Filtros** (Todos/Ativos/Expirando/Bloqueados) | Nenhum | Dispensável para poucas contas locais; registrar |
| **Tabela** (1118×420): USUÁRIO (avatar+nome+e-mail) · PERFIL · STATUS · LICENÇA ATÉ · ÚLTIMO ACESSO · ações | Lista simples: e-mail + selo de perfil + "criado em" + Remover | Alinhar: tabela com avatar/nome; "LICENÇA ATÉ"/"ÚLTIMO ACESSO" = **charter/escopo (não rastreamos)** |
| Dados de exemplo (Mariana, Rafael, 128 usuários) | Contas locais reais | Figma é mock; app mostra dados reais (correto) |

## ADM-02 "Criar login" vs app
| Campo (Figma, real) | No app hoje | Classificação |
|---|---|---|
| **Nome completo** (obrigatório) | Ausente (só e-mail) | Alinhar (seguro) — exige mudar `Usuario` + repo + testes |
| **E-mail** ("será o login, único") | ✅ E-mail | OK |
| **Senha** · **mínimo 12** · botão **"Gerar senha forte"** · **medidor de força** (Força 1–4) · requisitos (≥12, maiúsc/minúsc, número, símbolo, não conter nome/e-mail) · mostrar/ocultar | Senha min **8** (≥1 letra, ≥1 número); mostrar/ocultar só no Login | Alinhar política (≥12 + símbolo + caixa) e medidor — **ripple**: muda `ValidacaoCredenciais` + senhas de teste |
| **Confirmar senha** | ✅ Confirmar | OK |
| **Perfil** segmentado (Usuário/Administrador) | Checkbox "Conta de administrador" | Cosmético — alinhar para segmentado |
| **Validade da licença** (select) | Ausente | **Charter — licença removida; não implementar** |
| **Exigir troca de senha no 1º acesso** (checkbox) | Ausente | Alinhar (seguro) — exige flag no `Usuario` + fluxo no login |
| **Card/Resumo** (preview do acesso) | Ausente | Alinhar (cosmético, seguro) |
| Confirmar criação em **modal** (ADM-05) | Criação inline com mensagem | Divergência de fluxo (menor) |

## ADM-07 "Acesso negado" vs app
Figma mostra um estado dedicado de não-administrador. O app já tem a guarda `AdminView` → **"Sem permissão. Esta área é exclusiva de administradores."** (além de esconder a aba). **Intenção atendida.**

## Classificação final
**Já alinhado:** eyebrow "ADMINISTRAÇÃO", estado "Acesso negado"/sem permissão, acesso por papel (bcrypt), somente-leitura da auditoria (o ícone "log" do Figma).

**Divergências de charter/escopo (manter, não implementar):**
- Validade da licença, "Expiram em 7 dias" e métricas de licença (o original usava licença HWID/Discord).
- "Último acesso" e a escala de 128 usuários remotos (app é local, poucas contas).

**Alinhamentos seguros recomendados (próximos passos — exigem verificação visual quando o limite do Figma resetar):**
1. Política de senha forte do Figma (≥12, maiúsc/minúsc, número, símbolo, não conter nome/e-mail) + medidor + "Gerar senha forte".
2. Campo "Nome completo" (muda `Usuario`/repositório/testes).
3. "Exigir troca de senha no 1º acesso".
4. Cartões de métrica com contagem **real** (Total de contas, Administradores, Usuários) — sem licença.
5. Lista de usuários em formato de **tabela** com avatar (iniciais) + nome + e-mail + perfil.
6. Perfil como controle segmentado; card de "Resumo"; botão "Novo login" como ação primária no topo.

## Implementado (alinhamentos seguros estruturais)
Aplicados sem depender de screenshot (só estrutura real + tokens do app):
- **Política de senha forte do Figma**: ≥12, maiúsc/minúsc, número, símbolo, sem nome/e-mail (`ValidacaoCredenciais.AvaliarSenha/ValidarSenha`) + **medidor de força** (4 barras) + **checklist de requisitos** + botão **"Gerar senha forte"** (`GerarSenhaForte`).
- **Campo "Nome completo"** no modelo (`Usuario.Nome`), repositório, seed, login e formulário.
- **Cartões de métrica com contagem real**: Total de contas · Administradores · Usuários (sem licença).
- **Lista de contas** com **avatar (iniciais)** + nome + e-mail + perfil.
- Título da tela alinhado para **"Usuários"** (eyebrow "ADMINISTRAÇÃO").
- Testes atualizados (política ≥12, gerador, não-conter-dados-pessoais) → **81/81**.

## Pendente
- **Verificação visual fina** (cor/pixel/tipografia) contra os frames ADM — **bloqueada pelo limite do Figma MCP** (plano Starter). Refazer quando resetar/atualizar o plano.
- Deferidos (opcionais): perfil como controle segmentado, modal de confirmação (ADM-05), "exigir troca de senha no 1º acesso", busca/filtros. Não implementados para não virar stub.
- Charter (não implementar): validade de licença, "expiram em 7 dias", "último acesso", escala de 128 usuários remotos.
