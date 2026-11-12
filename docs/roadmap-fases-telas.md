# Sevenzin — Funcionalidades, Otimizações e Roadmap por Fases/Telas

Documento-guia do projeto. Cobre (1) tudo que o sistema já faz, (2) as otimizações realizadas e catalogadas,
(3) o mapeamento fase a fase por tela com ações concretas, e (4) desafios e soluções de cada fase.
Complementa [design-system.md](design-system.md) e [catalogo-otimizacoes.md](catalogo-otimizacoes.md).

Princípios fixos (carta segura §23–24): **medir antes de otimizar · somente leitura por padrão · uma mudança por vez ·
tudo reversível e auditado · nunca desligar segurança (Defender/UAC/HVCI/mitigações) · sem login/telemetria/exfiltração ·
sem promessa de FPS fixo.**

---

## Parte 1 — Funcionalidades do sistema (o que existe hoje)

### 1.1 Motor (`src/Sevenzin.Diagnostics`, independente de UI, 24 testes)

| Área | O que faz | Confiança/garantias |
|---|---|---|
| **Inventário somente leitura** | Coleta Sistema (Windows/BIOS/placa-mãe), CPU, GPU/driver, Memória, Armazenamento, Energia (plano ativo). | Cada dado traz origem, hora e grau **Fato/Inferência/Hipótese**. Nenhum identificador pessoal (HWID/serial/MAC/usuário/nome do PC). |
| **Topologia de CPU** | Núcleos P/E (EfficiencyClass) e domínios de cache L3 (≈ CCD) via `GetLogicalProcessorInformationEx`. | Fato para P/E; Inferência para CCD (heurística L3). |
| **Telemetria** | Sessão iniciada/encerrada por você; série temporal de CPU%, clock efetivo e memória (contadores WMI). | Resumo com média/p95/p99/desvio + **overhead medido** da própria coleta. |
| **Comparação A/B** | Compara linha de base × execução; marca **"inconclusivo" quando \|Δ\| ≤ ruído** medido. | Não declara "melhor/pior" sem direção conhecida. |
| **Benchmark de frametime** | Cena interna reproduzível; FPS médio, **1% low**, p95/p99 de frametime, desvio e **stutter**. | Mede o próprio app (não um jogo externo); sem admin, sem anti-cheat. |
| **Motor de tweaks (Fase 4)** | Aplica/desfaz ajustes reversíveis com **prévia, cópia do estado anterior, desfazer e auditoria** local. | **Trava de segurança no código** recusa Defender/UAC/HVCI/mitigações/SmartScreen/Windows Update/write-cache. |

### 1.2 Interface WPF (`src/Sevenzin.Wpf`, shell escuro do Figma)

Janela com barra de título própria + **navegação lateral**. Telas prontas e reais:

- **Início** — dashboard: monitores ao vivo (CPU%, memória) + cards de sistema (CPU/GPU/RAM/SO) com dados reais.
- **Ajustes** — catálogo de tweaks com toggles por seção, backed pelo motor; ajustes de segurança aparecem **bloqueados**.
- **Configurações** — abas Geral/Capturas/Preferências; "Iniciar com o Windows" real (chave Run HKCU); pasta de dados; sem login.
- **Inventário** — grade escura agrupada, confiança por cor.
- **Telemetria** — sessão ao vivo (iniciar/parar), resumo p95/p99, comparação A/B na tela.
- **Benchmark** — roda a cena e mostra FPS/1% low/percentis/stutter.
- Itens de nav ainda vazios ("em migração/em breve"): GPU, Instaladores, Jogos, BIOS, Capturas, Windows.

### 1.3 App WinForms (`src/Sevenzin.App`, legado)
As 4 telas originais (Inventário/Telemetria/Benchmark/Otimizações). O WPF alcançou paridade — **a aposentar**.

### 1.4 Documentação e dados
- [catalogo-otimizacoes.md](catalogo-otimizacoes.md) + [otimizacoes-originais/](otimizacoes-originais/): tudo que o binário original fazia.
- [design-system.md](design-system.md): paleta e telas do Figma.

---

## Parte 2 — Otimizações realizadas e catalogadas

### 2.1 Aplicadas hoje pelo app (reversíveis, HKCU, sem admin)
1. **Desligar Game DVR** (`GameConfigStore\GameDVR_Enabled=0`) — reduz overhead de gravação em jogos.
2. **Desligar ID de publicidade** (`AdvertisingInfo\Enabled=0`) — privacidade.
3. **Desligar animações da barra de tarefas** (`Explorer\Advanced\TaskbarAnimations=0`) — resposta visual.
4. **Desligar sugestões de conteúdo** (`ContentDeliveryManager\…338388Enabled=0`) — privacidade.

Cada uma: prévia → backup do valor anterior → aplica → auditoria. Desfazer restaura o estado exato.

### 2.2 Catálogo completo do original (classificado para evolução)
Do binário analisado: **338 escritas de registro, 110 chaves, 299 comandos** (powercfg/netsh/etc.), organizados em
[catalogo-otimizacoes.md](catalogo-otimizacoes.md) por categoria e **classe**:

- 🟢 **Reversível** (candidato): efeitos visuais, Game DVR/Game Bar, telemetria/privacidade, perfil MMCSS de jogo,
  preferência de GPU, plano de energia (alto desempenho), fast startup off, DNS multicast/NetBIOS off.
- 🟡 **Hardware/Inferência** (só com A/B): rede/TCP (TcpAckFrequency etc.), MSI-mode/IRQ affinity, DPC, memória
  (LargeSystemCache/DisablePagingExecutive), power gating de GPU, offloads de NIC, afinidade/prioridade por jogo.
- 🔴 **Proibido** (a trava recusa; só **detectar e oferecer restaurar**): desligar Defender, UAC, HVCI/VBS,
  mitigações Spectre/Meltdown, Exploit Protection, SmartScreen, pausar Windows Update, write-cache forçado.

---

## Parte 3 — Mapeamento fase a fase (por tela)

Status: ✅ feito · 🔄 em andamento · ⬜ a fazer · ⛔ excluído pela carta segura.

### Fase 0 — Segurança e fundação da confiança ✅
**Objetivo:** garantir que nada perigoso seja replicado.
**Ações:** Fazer a trava `GuardaSeguranca` (lista proibida no código); Fazer o modelo de confiança (Fato/Inferência/Hipótese);
Fazer a política "somente leitura por padrão".
**Desafio:** o original mistura otimização com malware/segurança-off. **Solução:** classificar cada ação e barrar 🔴 no código, com teste.

### Fase 1 — Inventário e dashboard ✅ · Telas: **Início**, **Inventário**
**Início (dashboard):**
- Fazer os monitores ao vivo (CPU%, memória) lendo os contadores reais.
- Fazer os cards de SISTEMA (CPU/GPU/RAM/SO) a partir do inventário.
- Fazer os cards de RECURSOS como atalhos para GPU e Jogos.
**Inventário:**
- Fazer a grade agrupada com origem e confiança colorida de cada dado.
- Fazer a exportação local (MD/JSON) sem identificadores.
**Desafio:** WMI satura/omite dados (ex.: VRAM > 4 GB, canais de RAM). **Solução:** marcar "não confirmado" em vez de inventar; usar API de topologia onde há (CPU P/E).

### Fase 2 — Shell e identidade visual ✅ · Tela: **shell (sidebar)**
- Fazer a janela escura com barra de título própria (minimizar/maximizar/fechar).
- Fazer a navegação lateral com item ativo destacado e rodapé de status.
- Fazer o tema (paleta do Figma como recursos) reusável por todas as telas.
**Desafio:** WinForms não renderiza o design escuro moderno. **Solução:** migrar a UI para **WPF** (motor intacto).

### Fase 3 — Medição ✅ · Telas: **Telemetria**, **Benchmark**
**Telemetria:**
- Fazer a sessão iniciar/parar com janela de captura visível e cronômetro.
- Fazer o resumo (média/p95/p99/desvio) e exibir o **overhead medido**.
- Fazer o A/B: "definir linha de base" → medir de novo → veredito inconclusivo-vs-ruído.
**Benchmark:**
- Fazer a cena reproduzível e medir frametime (FPS médio, 1% low, p95/p99, stutter).
**Desafio:** 1ª leitura WMI é lenta (warm-up) e distorce o overhead. **Solução:** 1 amostra de aquecimento fora do cronômetro.
**Desafio:** frametime de jogo real exige ETW/PresentMon (admin). **Solução:** benchmark **interno** sem admin; ETW fica fora por ora.

### Fase 4 — Recomendações reversíveis ✅ · Tela: **Ajustes**
- ✅ Catálogo de tweaks por seção com toggle (ligar=aplicar, desligar=desfazer), prévia, **estado anterior salvo**,
  desfazer e **auditoria** local (acessível por Configurações → Abrir pasta).
- ✅ **HKCU** (sem admin): Game DVR, ID de publicidade, animações, sugestões.
- ✅ **HKLM** (máquina): SystemResponsiveness, Win32PrioritySeparation, Fast Startup off, HAGS — com **elevação sob
  demanda** (reabre como admin só ao aplicar um item de máquina) e **aviso A/B**.
- ✅ Linha **bloqueada** mostrando que segurança (Defender/UAC/isolamento) não é desligada; a trava recusa no código.
- **Testes:** trava recusa ajuste de segurança (HKLM), catálogo sem itens proibidos e admin exigido ⇔ HKLM,
  aplicar/desfazer funciona igual em HKLM, backup/persistência/auditoria.
**Solução aplicada:** HKCU sem admin x HKLM com elevação só na hora; A/B antes de recomendar; nunca aplicar em massa.

### Fase 5 — Consolidação ⬜ · (sem tela nova)
- Fazer a aposentadoria do app WinForms (remover `src/Sevenzin.App` e seus testes de UI; manter os testes do motor).
- Fazer o polimento: fontes Inter/Geist embutidas, ícones definitivos, agrupamento visual do inventário, ícone de bandeja
  (habilita os toggles "minimizar/fechar para a bandeja" das Configurações).
**Desafio:** remover o WinForms quebra 2 testes de UI dele. **Solução:** mover a cobertura essencial para o motor (já coberta) e apagar só os testes específicos do WinForms.

### Fase 6 — GPU e drivers (seguro) ✅ · Tela: **GPU**
- ✅ Detecção de GPU/driver (fabricante, versão, data, VRAM, modo) — somente leitura.
- ✅ Heurística de **driver possivelmente desatualizado** (> 18 meses) e recomendações seguras marcadas como sugestão.
- ✅ Link para a página **oficial** do driver (NVIDIA/AMD/Intel), domínio verificado — **sem** espelhos, sem download, sem DDU.
- **Testes (43 no total):** detecção de fabricante (8 casos), URL oficial coerente por fabricante, heurística de data, coleta real.
**Solução aplicada:** marca incerteza da VRAM (WMI satura); nenhuma limpeza de driver nem flags ocultas; só leitura + link oficial.

### Fase 7 — Instaladores (verificados) ✅ · Tela: **Instaladores**
- ✅ Catálogo de utilitários benignos com **origem oficial** (Coreinfo/Microsoft, CPU-Z, CrystalDiskInfo).
- ✅ Download com **progresso** e **verificação de assinatura (publisher) + hash (quando fixado)** antes de permitir executar.
- ✅ Nada roda automaticamente: executar só após verificação **Ok** e **confirmação explícita**; falha na verificação apaga o arquivo.
- **Testes (50 no total):** Sha256 do arquivo, Verificar (Ok / hash não confere / sem assinatura / publisher diferente), assinatura
  nula para arquivo não assinado, catálogo só com https.
**Solução aplicada:** só fontes oficiais, verificação obrigatória, nada silencioso — o oposto do original.

### Fase 8 — Jogos / perfis por título (reversível) ✅ · Tela: **Jogos**
- ✅ Lista de jogos (adicionar pelo .exe) + catálogo persistido + **busca**.
- ✅ **Perfil por jogo**: prioridade (Normal/Acima/Alta, **nunca Realtime**) e **afinidade** (todos os núcleos ou
  **só P-cores**, via máscara de topologia confirmada), aplicados ao processo com **estado anterior salvo** e **reverter**.
- ✅ **"Aplicar ao abrir"** (modo automático): o `GestorJogos.Sincronizar` aplica os jogos que abrem e **restaura os que
  fecham**; um timer de 2 s dirige o monitor.
- ✅ Aviso A/B na tela (medir com Benchmark/Telemetria antes de confiar no ganho).
- **Testes (28/28):** máscaras P/E, aplicar/reverter no próprio processo, máscara 0 = todos, nunca Realtime, catálogo
  persistente, gestor (aplica ao abrir / não reaplica / reverte ao fechar / manual / falha), e integração do controle real.
- **Limitação conhecida:** com várias instâncias do mesmo executável, aplica na primeira encontrada (jogos normalmente
  têm uma instância).
**Desafio:** afinidade/prioridade fixa é tratada como "otimização universal" e pode piorar tudo; não mexer em CPU híbrida sem topologia. **Solução:** só oferecer seleção de núcleo com topologia confirmada (já temos P/E), manter o agendador do Windows como padrão, nunca "Realtime", sempre reversível no encerramento.

### Fase 9 — Capturas (local) ✅ · Tela: **Capturas**
- ✅ Captura de tela da tela principal, salva como **PNG em pasta local** (`%LOCALAPPDATA%\Sevenzin\capturas`), **nunca enviada**.
- ✅ A janela do Sevenzin é minimizada por um instante para não aparecer na imagem; miniatura e "Abrir pasta" na tela.
- **Testes (53 no total):** nome de arquivo (prefixo/data/.png), pasta local correta, e **Salvar grava um PNG válido** (relido).
**Solução aplicada:** sem rede, sem webhook — texto explícito "local, não enviado"; o oposto do original que exfiltrava.

### Fase 10 — Entrada local e Admin ✅ · Tela: **Admin**
- ✅ **Entrada sem autenticação**: o app abre direto na tela Início — **sem** login, HWID, Discord ou licença (removidos).
  O rodapé da sidebar diz "Local · sem login".
- ✅ Visão **Admin**: painel **local de auditoria** dos tweaks (hora, ação, otimização, mudança), somente leitura, com
  "Atualizar" e "Abrir pasta". Sem servidor, sem controle remoto.
- **Testes (55 no total):** `LeitorAuditoria` interpreta as linhas do log (ignora vazias/inválidas) e retorna vazio sem arquivo.
**Solução aplicada:** entrada local substitui o login por Discord/licença; Admin é leitura de auditoria, não controle remoto.

### Excluídas pela carta segura ⛔ · Telas: **BIOS**, **Windows (ISO)**
- **BIOS**: flash/edição de firmware — risco de inutilizar a máquina. Fora do escopo.
- **Windows (ISO)**: construtor de mídia que edita hives offline e desliga segurança. Fora do escopo.
Se um dia entrarem, só como **diagnóstico/leitura**, nunca gravando firmware nem desabilitando proteções.

---

## Parte 4 — Próximas fatias (minha escolha, nesta ordem)

A partir de agora eu escolho e sigo sem perguntar a cada passo:

1. **Fase 5 — aposentar o WinForms** (rápido; remove duplicação de `RegistroWindows`/`CenaBenchmark` e reduz manutenção).
2. **Fase 5 — polimento**: fontes Inter/Geist, agrupamento do inventário, ícone de bandeja (destrava toggles de bandeja).
3. **Fase 8 — Jogos** (perfis reversíveis por título): a funcionalidade-assinatura de otimização, e totalmente dentro da carta segura.
4. **Fase 4 (ampliar) — tweaks 🟡/HKLM** com elevação sob demanda e A/B obrigatório.
5. **Fase 6 — GPU** (diagnóstico + recomendações seguras + link oficial de driver).

Decisões firmes: nada que desligue segurança; BIOS e ISO ficam fora; cada otimização só é recomendada após A/B no PC real.
