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

## §7. Fase 3 — Instaladores completos (12/11/2026)

- `WingetRunner.cs` (novo): allowlist winget **literal** do original (5 IDs: Steam/Discord/7-Zip/Chrome/VLC),
  código "já instalado" `-1978335189` e construção do comando como função pura; `InstalarAsync` recusa qualquer
  ID fora da lista sem iniciar processo.
- `Instaladores.cs`: catálogo ampliado para **36 utilitários** — os 7 existentes + os nomes confirmados na RE
  (05_metricas §6): 7-Zip, Brave, Chrome, Edge, DirectX, Epic, Rockstar, Ubisoft, VALORANT, FACEIT, Gamers Club,
  TeamSpeak, LatencyMon, MSI Afterburner, G HUB, NVCleanstall, OCAT, Open-Shell, StartAllBack, VS Code, Visual
  Studio, DDU, Radeon Software Slimmer, ZenTimings, Prime95, Geekbench, OCCT, DriverStore Explorer, driver de rede
  Intel. **Sempre fonte oficial**; download direto continua só para URLs estáveis (Sysinternals). URLs/hashes do
  original são lacuna no `#US` — não inventados.
- `InstaladoresView`: cartão "Instalação rápida (winget)" com checkboxes dos 5 IDs, confirmação explícita antes
  de executar e andamento registrado na fila persistida.
- **Exclusão registrada:** KMS Activator (logo confirmado no original) NÃO entra no catálogo — ativador de licença
  não-oficial, risco de malware e violação de licença (`CatalogoUtilitarios.Excluidos` + teste).

**Arquivos alterados:** `WingetRunner.cs` (novo), `Instaladores.cs`, `InstaladoresView.xaml.cs`,
`WingetTests.cs` (novo).

**Testes:** `dotnet build` 0/0 · `dotnet test` **123/123** (allowlist literal, comando exato, rejeição fora da
lista, cobertura dos nomes da RE, exclusão do KMS).

**Pendências / validação manual:** instalação real via winget em máquina controlada (confirmação na UI);
validação visual do cartão (F8).

## §8. Fase 4 — Guias, Notificações e tweaks de mouse/localização (12/11/2026)

- **Motor com suporte a REG_SZ** (`Tweaks.cs`): `TipoValor { Dword, String }` no `Tweak`, `IRegistro.LerString/
  EscreverString`, backup variante (`ValorAnterior`) que desfaz restaurando o tipo exato. Motivo: o original
  gravava strings em `Control Panel\Mouse` (confirmado em `otimizacoes-originais/01_registro_setvalues.txt`).
- **Tweaks novos:** 3 de mouse (aceleração + 2 limiares, HKCU/string) e 2 de localização (`HideSCALocation=1`
  e `lfsvc Start=4` com aviso de que jogos podem depender do serviço) — ambos confirmados no original.
- **Guias** (`Guias.cs` + `GuiasView`): 6 guias reconstruídos do que o app realmente faz (textos exatos do
  original são lacuna no `#US` — registrada). Só instrui sobre recursos implementados.
- **Notificações** (`Notificacoes.cs` + `NotificacoesView`): histórico local (JSON atômico, máx. 200) + fila de
  instaladores na mesma tela. Eventos reais registrados em Ajustes (aplicar/desfazer), Proteção, Instaladores
  (verificação e winget) e Capturas (salvar). Nada sai da máquina.

**Arquivos alterados:** `Tweaks.cs` (motor string), `RegistroWindows.cs`, `Guias.cs` (novo), `Notificacoes.cs`
(novo), `GuiasView.xaml/.cs` (novos), `NotificacoesView.xaml/.cs` (novos), `MainWindow.xaml.cs`, `Icons.xaml`,
`AjustesView.xaml.cs`, `ProtecaoView.xaml.cs`, `InstaladoresView.xaml.cs`, `CapturasView.xaml.cs`,
`TweaksTests.cs`, `CacheEscritaTests.cs` (fake ampliado), `GuiasENotificacoesTests.cs` (novo).

**Testes:** `dotnet build` 0/0 · `dotnet test` **127/127** (string-tweak aplica/desfaz restaurando REG_SZ,
catálogo de mouse/localização conforme o original, guias únicos com passos não vazios, notificações
persistem/ordenam/limpam).

**Pendências / validação manual:** render das telas novas (F8); efeito real do `lfsvc Start=4` exige
reinício; conferir no Painel de Controle que o mouse ficou 1:1.

## §9. Fase 5 — Perfil, licença local e 2FA (12/11/2026)

- **TOTP local** (`Totp.cs`, novo): RFC 6238 (HMAC-SHA1, 30 s, 6 dígitos) com Base32 RFC 4648 próprio,
  tolerância de ±1 janela e comparação em tempo constante. **Validado contra os vetores oficiais do RFC 6238.**
  Substitui o `KeyAuth_Enable2FA/GetTwoFactorSecret` do original por verificação 100% offline.
- **Contas** (`Usuarios.cs`): `Usuario.TotpSegredo/TotpAtivo` (padrão desligado — `usuarios.json` antigo segue
  abrindo); `IRepositorioUsuarios.Atualizar`; `ServicoContas.GerarSegredo2FA/Confirmar2FA/Desativar2FA`
  (o fator só ativa depois de confirmado com um código válido).
- **Autenticação** (`Autenticacao.cs`): novo status `Exige2FA`; `Autenticar(email, senha, codigo2fa?)` — senha
  correta + 2FA ativo sem código devolve `Exige2FA`; código errado conta como falha (força bruta).
- **Login** (`LoginWindow`): etapa do código do autenticador (bloco TOTP, botão vira "Verificar código").
- **Licença local** (`Licenca.cs`, novo): chave `A1B2-C3D4-E5F6-G7H8` guardada em JSON local; validação de
  formato pura. **Registro formal, não bloqueio** — sem servidor/HWID, e nada é escondido com ou sem chave.
- **Tela Perfil** (`PerfilView`, nova + item na navegação): conta (e-mail/papel/criada), ativação de 2FA com
  segredo + confirmação, licença (salvar/remover) e resumo local (jogos, capturas, notificações).

**Arquivos alterados:** `Totp.cs`, `Licenca.cs` (novos); `Usuarios.cs`, `Autenticacao.cs`, `LoginWindow.xaml/.cs`,
`PerfilView.xaml/.cs` (novos), `MainWindow.xaml.cs`, `Icons.xaml`, `TotpELicencaTests.cs` (novo).

**Testes:** `dotnet build` 0/0 · `dotnet test` **133/133** (vetores RFC 6238, conferência ±1 janela, formato e
persistência da licença, fluxo Exige2FA → código errado rejeitado → código certo autentica, 2FA nunca ativa sem
confirmação).

**Pendências / validação manual:** testar o login com 2FA num autenticador real (Google Authenticator/Authy);
render da tela Perfil (F8).

## §10. Fase 6, F7.1 e detecção de jogos (13/11/2026)

**F6 — Assistente** (commit `0e789c0`): endpoint HTTPS **configurado pelo usuário** em Configurações (padrão:
desligado — o servidor do original NÃO é reaproveitado). Envio explícito por mensagem (`Assistente.EnviarMensagemAsync`,
JSON {mensagem, contexto, utc}), contexto do PC (`Assistente.ContextoPc`, só inventário, "—" sem valor) incluído
só com o checkbox marcado. **HumanMode = lacuna registrada** (comportamento de servidor). Histórico em memória
(nada persistido/automático). Testes: URL, config (corrompido→desligado), contexto sem valores forjados, POST com
handler fake (2xx/5xx/vazio).

**Detecção de jogos Steam/Epic** (commit `f05cfff`): `DescobridorJogos` só leitura — `libraryfolders.vdf`,
`appmanifest_*.acf` (nome/appid/installdir) e manifestos `.item` da Epic (DisplayName/InstallLocation/
LaunchExecutable combinados). `ExecutavelPrincipal` = heurística pura (nome da pasta → maior .exe → ignora
unins/redist). Launcher ausente = lista vazia, sem erro. UI: seção "Jogos detectados" na `JogosView` com
"Adicionar ao catálogo" (abre o seletor de .exe quando ambíguo; **nada é aplicado automaticamente**).

**F7.1 — Análise de ISO (só leitura)** (commit `8799d89`): `ExecutorDism.AnalisarIsoAsync` monta a ISO
(read-only via PowerShell), localiza `install.wim/esd`, roda `dism /Get-WimInfo`, `/Get-AppxProvisionedPackages`
e `/Get-WindowsOptionalFeature`, desmonta sempre (finally). Parsers **puros e testados** (`AnalisadorImagem`) com
amostras no formato real do DISM. UI: cartão na `WindowsView` com elevação sob demanda e resultado resumido.

**F8 (parcial) — docs:** `paridade_funcional.md` **reescrito** com o estado real (28 tweaks, segurança, assistente,
ISO, detecção de jogos, energia, Supabase/MFA/Mongo, dois apps, lacunas F7.2 e HumanMode); plano e AGENTS/README
atualizados. **Validação visual/manual = pendente** (render das telas novas e fluxos reais).

**Testes:** `dotnet build` 0/0 · `dotnet test` **158/158**.

**Lacunas registradas:** F7.2 (construção ISO/USB/debloat — ADK/IMAPI2FS/GPL; só com decisão explícita);
HumanMode do assistente (servidor); categorias **d** antigas (rede/DPC/serviços/MMCSS Games) por risco/retorno.

## §11. Fase 7.2 — Construção de mídia (13/11/2026)

- **ISO personalizada** (`CriadorIso.cs` + `ConstrutorIso.BuildAsync`): monta a origem (read-only), copia os
  arquivos com **SHA-256** (`CopiadorVerificado`, blocos de 1 MB como o MediaTransfer do original), exporta
  **só a edição escolhida** via `dism /Export-Image` (compressão máxima), cria a ISO **UEFI-bootável** via
  **IMAPI2FS** (COM embutido no Windows — sem ADK, sem binários GPL) usando o `efisys.bin` da própria mídia
  de origem, desmonta e limpa o staging sempre (finally).
- **Pendrive** (`GravadorUsb.cs` + `UsbMedia.cs`): réplica do **SafeUsb/CheckUsb** do original (USB, ≥ 8 GB,
  nunca boot/sistema/offline/readonly, UniqueId presente, reidentificação por UniqueId+Serial+Tamanho antes
  de gravar); prepara FAT32; copia com SHA-256; **divide o install.wim em partes de 3.800 MB** (limite FAT32)
  via `dism /Split-Image` — o caminho FAT32 do original. Confirmação explícita na UI com modelo/tamanho do
  pendrive e aviso de apagamento total.
- **UI** (`MediaView`, nova + item "Mídia" na navegação): escolher ISO → edições detectadas → destino →
  construir (etapas ao vivo); pendrives seguros → gravar.
- **Limitação registrada:** boot **UEFI** apenas (sem BIOS/Legacy — coerente com o alerta do original); ficam
  de fora por ora a remoção de apps/features, injeção de drivers e OfflineHive (etapas 5–9 do fluxo F6).

**Arquivos alterados:** `ConstrutorMedia.cs`, `CriadorIso.cs`, `GravadorUsb.cs` (novos), `MediaView.xaml/.cs`
(novos), `MainWindow.xaml.cs`, `Icons.xaml`, `ConstrutorMediaTests.cs` (novo).

**Testes:** `dotnet build` 0/0 · `dotnet test` **162/162** (parser Get-Disk, SafeUsb completo, cópia com
SHA-256 atravessando blocos, validação do pedido de construção).

**Pendências / validação manual:** construir uma ISO real e bootar numa VM; gravar num pendrive de teste;
confirmar o hash de arquivos grandes na cópia.

## §12. F9–F12 — Catálogo completo, GPU/boost por jogo, desinstalação e serviço de imagem (13/11/2026)

- **F9 — Catálogo completo do original** (commits `22e68bc`): +20 itens com os VALORES LITERAIS recuperados
  (`01_registro_setvalues.txt`): MMCSS Games (GPU Priority=8, Priority=2, SFIO=Normal, Scheduling=Medium),
  TdrDelay=8, DisableDynamicPstate=1 (AMD), ChillEnabled=0, RMPriorityThrottleDelay=0, NetworkThrottlingIndex=10,
  DisablePagingExecutive/LargeSystemCache=1, transparência/AeroPeek/DragFullWindows/MenuShowDelay/Toast, WER e
  Copilot off. Teste `CatalogoOriginalTests` valida a fidelidade valor por valor.
- **F10 — GPU alta + boost por jogo**: `PerfilJogo.PrefereGpuAlta` (grava/remove `HKCU DirectX\UserGpuPreferences
  <exe> = "GpuPreference=2;"`) e `PriorityBoost` (temporário, revertido); `RegistroUsuario` (IRegistro HKCU no
  motor); `EstadoProcesso` com Boost; UI com 2ª linha de opt-ins por jogo.
- **F11 — Desinstalação assistida**: `DesinstaladorApps` usa os setups OFICIAIS (Edge `--uninstall
  --force-uninstall --system-level`; OneDrive `/uninstall`) com confirmação explícita; localização dos setups
  testada (SysWOW64 → System32, Installer por versão).
- **F12 — Serviço de imagem na construção**: `ConstrutorIso.BuildAsync` monta o WIM exportado em RW e aplica
  remoção de apps provisionados, remoção de features e injeção de drivers INF assinados (`dism /Commit`;
  `/Discard` em falha). UI da Mídia: lista real de apps da edição escolhida (checkbox) + pasta de drivers.

**Testes:** `dotnet build` 0/0 · `dotnet test` **171/171**.

**Lacunas registradas:** OfflineHive; boot BIOS/Legacy; CPU Sets por processo; rede por interface; lista de
serviços/tarefas do original (#US cifrado); lista fixa de apps do debloat (o Sevenzin usa a lista real da imagem).

### Validação visual (smoke, 13/11/2026)
- Renders headless gerados para as 9 telas novas/alteradas: `assistente`, `midia`, `perfil`, `protecao`,
  `guias`, `notificacoes`, `windows`, `instaladores`, `jogos` — em `%TEMP%\opencode\renders\*.png`
  (1280×760). Todas as telas **construíram e renderizaram sem exceção** (o modo `--render` fecha o app
  com código 0; um view que lançasse não produziria o PNG).
- **Limitação:** o modelo da sessão não processa imagens — a inspeção visual fina (fidelidade ao Figma,
  espaçamentos, cores) fica como validação **manual do dono** sobre esses PNGs.


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
