# Plano de Implementação Completa — Sevenzin

**Decisão do dono (12/11/2026):** a trava `GuardaSeguranca` foi removida (`docs/revisao_ajustes.md` §4; branch de
restauração `arquivo/guardaseguranca`) e o conjunto antes "fora do escopo" passou a **em escopo**. Este plano cobre
todas as telas e funcionalidades — área do usuário e área do ADM — usando como fonte da verdade funcional os
relatórios de engenharia reversa em `C:\re\geandre_v3\output\` e `docs/engenharia-reversa/*`, e o **Figma como fonte
da verdade visual**.

**Regras operacionais (fixas):**
- Fase só avança quando 100% implementada, com `dotnet build` 0/0 e `dotnet test` todo verde.
- Sem stubs, TODOs, mocks permanentes ou telas só visuais: cada ação executa o processo e mostra resultado verificável.
- Nada é inventado fora da engenharia reversa ou dos requisitos aprovados; falta de informação = lacuna registrada.
- Toda ação que altera o Windows tem **explicação, confirmação explícita e desfazer** quando possível.
- **Nunca** executar otimizações nesta máquina de desenvolvimento sem confirmação do dono; **nunca** executar `geandre.exe`.

**Legenda de status:** ✅ Implementada e validada (teste/evidência) · 🟡 Implementada sem validação manual · ◐ Parcial · ⬜ Ausente.

---

## 1. Inventário de telas e funcionalidades (status atual)

Base: 22 telas do original (`docs/engenharia-reversa/03_telas.md`) + área ADM própria do Sevenzin.

| # | Tela / funcionalidade (original) | No Sevenzin hoje | Status | Fase |
|---|---|---|---|---|
| 1 | Login (KeyAuth: login/licença/2FA/web) | `LoginWindow` e-mail+senha bcrypt, sessão/papéis/anti-brute-force; sem licença nem 2FA | ◐ | F5 |
| 2 | Shell de navegação | `MainWindow` (sidebar recolhível, animações) | ✅ | — |
| 3 | Home | `InicioView` (CPU via GetSystemTimes, GPU, mem, latência, sistema, recursos) | ✅ | — |
| 4 | Jogos | `JogosView` (afinidade P-cores, prioridade, working set, timer 1 ms opt-in) + **detecção Steam/Epic só leitura** | 🟡 | F8 ✅ |
| 5 | Placa de vídeo (GPU) | `GpuView` (driver, VRAM, modos de vídeo DEVMODE, link oficial) | 🟡 | F8 |
| 6 | Processador (CPU) | `InventarioView` (WMI + topologia P/E/CCD, confiança) | ✅ | — |
| 7 | Armazenamento / Localização / Mouse | Inventário cobre armazenamento; tweaks de mouse (REG_SZ) e localização (HideSCALocation + lfsvc) no catálogo | 🟡 | F4 ✅ |
| 8 | Ajustes (12 grupos) | `AjustesView` com 28 tweaks reversíveis (inclui Segurança, Mouse, Localização) | 🟡 | F1 ✅ |
| 9 | Instaladores (~85 itens + winget) | `InstaladoresView` com 36 utilitários oficiais + fila persistida + winget allowlist (5 IDs) | 🟡 | F3 ✅ |
| 10 | Guias | `GuiasView` com 6 guias baseados no que o app realmente faz | 🟡 | F4 ✅ |
| 11 | BIOS | `BiosView` (Secure Boot/TPM só leitura + instruções por risco) | 🟡 | F8 |
| 12 | Assistente (IA/humano) | `AssistenteView`: endpoint HTTPS configurado pelo usuário, envio explícito por mensagem, contexto com consentimento | 🟡 | F6 ✅ |
| 13 | Capturas (região/janela/monitor + anotação) | `CapturasView` completa: 4 modos + editor (lápis/retângulo/seta/texto/desfazer) + envio explícito opcional | 🟡 | F2 ✅ |
| 14 | Pendrive / Windows Media (ISO/USB) | `MediaView` (nova): construir ISO personalizada (edição única, UEFI) + gravar pendrive (SafeUsb + FAT32 + split de WIM); análise de ISO no WindowsView | 🟡 | F7 ✅ |
| 15 | Perfil (conta + hardware) | `PerfilView`: conta local, 2FA (TOTP offline), licença local, resumo | 🟡 | F5 ✅ |
| 16 | Configurações | `ConfiguracoesView` (iniciar com Windows, pasta de dados, preferências) | 🟡 | F8 |
| 17 | Notificações / Tarefas agendadas | `NotificacoesView` (histórico local real + fila persistida) | 🟡 | F4 ✅ |
| 18 | Proteção | — | ⬜ | F1 |
| 19 | Sair | Logout na sidebar (volta ao login) | ✅ | — |
| 20 | Área ADM | `AdminView` (auditoria local de tweaks, atualizar/abrir pasta) | ✅ | F5 |

**Nota (lacuna registrada):** o original não tinha área ADM nem gestão de usuários; o Sevenzin tem papéis
(administrador × usuário) mas a UI de gestão de usuários **não estava na engenharia reversa nem nos requisitos
aprovados** — não será inventada; se o produto exigir, será decisão explícita do dono.

## 2. Fases

| Fase | Conteúdo | Dependência |
|---|---|---|
| **F0** ✅ | Remoção da trava + registro + branch de restauração | — |
| **F1.1** ✅ | Tweaks avançados (UAC, Defender, SmartScreen, HVCI, mitigações, Windows Update) + confirmação explícita + **tela Proteção** | F0 |
| **F1.2** ✅ | Write-cache forçado por disco (CacheIsPowerProtected via Enum) + seção na Proteção | F1.1 |
| **F2** ✅ | Capturas completas: região/janela/monitor, editor de anotação, envio explícito (webhook configurado pelo usuário) | F1 |
| **F3** ✅ | Instaladores completos: catálogo ampliado da RE (35 logos confirmados + lista provável, fontes oficiais + hash) + winget allowlist | F1 |
| **F4** ✅ | Guias + Notificações/Tarefas agendadas + tweaks extras (mouse, localização) | F3 |
| **F5** ✅ | Perfil + licença local + 2FA (substitui KeyAuth; sem HWID/Discord) + completar ADM | F4 |
| **F6** ✅ | Assistente: envio explícito por mensagem, consentimento por conversa, IncludeInContext (HumanMode = lacuna de servidor) | F5 |
| **F7.1** ✅ | Windows Media: análise da ISO **somente leitura** (edições/apps/features via DISM, parsers testados) | F6 |
| **F7.2** ✅ | Windows Media: **construção** — ISO personalizada (edição única, UEFI via IMAPI2FS + efisys da origem) e **pendrive** (SafeUsb, FAT32 + split de WIM 3.800 MB). Sem ADK/GPL. Boot UEFI; BIOS/Legacy = limitação registrada | F7.1 |
| **F9** ✅ | Catálogo completo do original (MMCSS Games, TdrDelay, AMD, rede, memória, visual, WER, Copilot — valores literais) | F7 |
| **F10** ✅ | GPU alto desempenho + priority boost por jogo (UserGpuPreferences + boost temporário) | F9 |
| **F11** ✅ | Desinstalação assistida de Edge/OneDrive (setups oficiais + confirmação) | F9 |
| **F12** ✅ | Serviço de imagem na construção (remover apps/features, injetar drivers INF) | F7.2 |
| **F8** 🟡 | Consolidação: docs atualizados (paridade/plano/registro); validação visual e manual = pendente | todas |

---

## 3. Fase 1.1 — ✅ concluída (detalhe)

**Objetivo:** os grupos de segurança do original entram no catálogo como tweaks de **risco ALTO**, com confirmação
explícita e desfazer; a nova tela **Proteção** mostra o estado real de cada mecanismo e os toggles correspondentes.

| Item | Chave (HKLM) | Valor | Risco / aviso |
|---|---|---|---|
| `uac-off` | `SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\EnableLUA` | 0 | ALTO — remove consentimento de elevação |
| `defender-rt-off` | `SOFTWARE\Microsoft\Windows Defender\Real-Time Protection\DisableRealtimeMonitoring` | 1 | ALTO — Tamper Protection pode ignorar |
| `defender-antispyware-off` | `SOFTWARE\Microsoft\Windows Defender\DisableAntiSpyware` | 1 | ALTO — só efeito em edições antigas/TP off |
| `smartscreen-off` | `SOFTWARE\Policies\Microsoft\Windows\System\EnableSmartScreen` | 0 (DWORD) | ALTO — política que desliga o SmartScreen |
| `hvci-off` | `SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled` | 0 | CRÍTICO — isolamento de núcleo |
| `mitigacoes-off` | `SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\FeatureSettingsOverride` | 1 | ALTO — mitigações Spectre/Meltdown |
| `wu-pausar` | `SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoUpdate` | 1 | ALTO — pausa atualizações automáticas |

**Observação técnica (registrada):** `hvci-off` só tem efeito com reinicialização; Defender com Tamper Protection
ativo ignora os valores — a UI avisa. O motor continua garantindo prévia/backup/desfazer/auditoria para todos.

### Entregas da F1.1
1. `Tweak.ExigeConfirmacao` (novo campo opcional) — UI pede confirmação antes de aplicar.
2. 7 tweaks novos no `CatalogoTweaks` (todos HKLM, `ExigeAdmin=true`, `ExigeConfirmacao=true`, risco ALTO/CRÍTICO).
3. Leitores de estado em `SegurancaSistema.cs`: UAC, Defender (tempo real), SmartScreen, HVCI, mitigações, Windows Update.
4. `ProtecaoView` (WPF) + item na navegação: estado real por mecanismo + toggle com confirmação + aviso de reinício onde aplicável.
5. `AjustesView`: diálogo de confirmação para `ExigeConfirmacao`.
6. Testes: IDs únicos com os novos itens; todo item de segurança exige admin+confirmação; leitores retornam tipo correto; motor aplica/desfaz os novos itens no registro fake.

**Critério de conclusão:** build 0/0, testes verdes, telas renderizadas com dados reais (sem valor forjado).

**Resultado (12/11/2026):** build 0/0 · **104/104 testes** · `Tweak.ExigeConfirmacao` + 7 tweaks "Segurança" no
catálogo + leitores de estado em `SegurancaSistema` + `ProtecaoView` com confirmação explícita + avisos na
`AjustesView`. Validação visual (render) = manual, junto com a F8.
