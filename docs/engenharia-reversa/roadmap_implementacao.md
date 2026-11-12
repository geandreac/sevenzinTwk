# Roadmap de implementação — do inventário ao sistema de otimização

Derivado da análise das 7 fases ([`00_resumo`](00_resumo.md) … [`06_fluxos`](06_fluxos.md) + [`metricas.json`](metricas.json)).
Guia: **medir antes de otimizar**, só o subconjunto **seguro, reversível e auditado**; o conjunto perigoso do original
(ver §C) é **excluído por design**, não "a implementar". Fonte da trava: `GuardaSeguranca` em `Tweaks.cs`.

Legenda de estado: ✅ pronto · 🟡 pronto (validação manual pendente) · 🔜 próximo · ⛔ não replicar.

---

## 1. Requisitos extraídos, por fase

| fase (doc) | requisito confirmado `[CONFIRMADO]` | onde no projeto | estado |
|---|---|---|---|
| 01 triagem | App desktop Windows elevado, .NET, tema escuro Inter/Geist | `Sevenzin.Wpf` (WPF .NET 8, `Theme.xaml`, ícone oficial) | ✅ |
| 02 estrutura | Camadas UI ↔ engine sem UI; backend opcional | `Sevenzin.Wpf` + `Sevenzin.Diagnostics` (testável isolado) | ✅ |
| 03 telas | 22 telas; shell com nav lateral; login | 13 Views + `MainWindow` + `LoginWindow` (ver §2) | ✅ maioria |
| 04 func. §1 | Login/conta/sessão (sem KeyAuth/HWID/Discord) | `Autenticacao.cs`, `Usuarios.cs`, `LoginWindow` (bcrypt, sessão, papéis) | ✅ |
| 04 func. §3 | 12 grupos de tweaks (privacidade/telemetria/UX/desempenho) | `CatalogoTweaks` + `MotorTweaks` (prévia/backup/desfazer/auditoria) | ✅ subconjunto seguro |
| 04 func. §8 | Otimização por processo: afinidade, prioridade, working set | `Jogos.cs` (afinidade + prioridade, revertido ao fechar) | 🟡 parcial seguro |
| 04 func. §9 | Captura de tela + anotação, **local** | `Capturas.cs` + `CapturasView` | ✅ |
| 04 func. §2 | Perfil/inventário de hardware (WMI + topologia) | `Coletores.cs`, `Inventario.cs`, `TopologiaCpu.cs` | ✅ |
| 05 métricas §2 | Enumeração de modos de vídeo (DEVMODE, Hz) | — (só modo atual via WMI) | 🔜 |
| 05 métricas §3 | Memória/disco/lixeira reais; CPU/GPU uso | `Coletores`, `LeitorGpuUso` (GPU Engine); CPU% | 🟡 |
| 05 §6-8 | Catálogo de instaladores (fontes oficiais) + winget allowlist | `Instaladores.cs` (`CatalogoUtilitarios`, verificação de assinatura) | ✅ subconjunto benigno |
| 06 fluxos F5-F8 | Windows Media / ISO / USB / pós-instalação | — | ⛔ fora do escopo (instalação de SO, GPL, alto risco) |
| 06 fluxos F12 | Assistente IA/humano via webhook | — | ⛔ não replicado (exfiltração/telemetria remota) |

## 2. Telas: original × Sevenzin

| original (03_telas) | Sevenzin | nota |
|---|---|---|
| Login | `LoginWindow` | e-mail+senha, bcrypt; sem KeyAuth/HWID |
| Shell/nav | `MainWindow` (sidebar recolhível) | ✅ |
| Home | `InicioView` | monitores reais (CPU/GPU/mem/ping), "—" quando indisponível |
| Jogos | `JogosView` | perfis por `.exe`, afinidade/prioridade temporárias |
| GPU | `GpuView` | diagnóstico só leitura + ferramentas oficiais |
| CPU/Armazenamento | `InventarioView` | inventário com grau de confiança |
| Ajustes/Tweaks | `AjustesView` | ← **ampliado agora** (ver §4) |
| Instaladores | `InstaladoresView` | fontes oficiais + assinatura |
| BIOS / Windows | `BiosView`, `WindowsView` | informativas, risco classificado |
| Capturas | `CapturasView` | local, sem webhook |
| Telemetria/Benchmark/Inventário | `TelemetriaView`, `BenchmarkView`, `InventarioView` | A/B, frametime, inventário |
| Configurações | `ConfiguracoesView` | — |
| Assistente / Pendrive / Proteção | — | ⛔ não replicados (ver §C) |

## 3. Pré-requisitos

- **.NET SDK 8** (Windows). Build: `dotnet build src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug`.
- Tweaks de **máquina (HKLM)** exigem **administrador** — elevação sob demanda em `AjustesView` (`Elevacao.ReabrirComoAdmin`).
- Primeiro uso: criar o administrador (e-mail + senha forte) ou semear via `SEVENZIN_ADMIN_EMAIL`/`SEVENZIN_ADMIN_SENHA`.
- Testes: `dotnet test` (85 testes).

## 4. Implementado neste ciclo

**Catálogo de tweaks ampliado (8 → 16)** — `src/Sevenzin.Diagnostics/Tweaks.cs`, cobrindo o subconjunto **seguro** dos 12
grupos do original (04_funcionalidades §3; catalogo-otimizacoes §8–9), cada um um DWORD reversível pelo `MotorTweaks`:

- **Jogos:** `gamedvr-off`, `gamedvr-captura-off` (AppCaptureEnabled).
- **Privacidade:** `advertising-id-off`, `sugestoes-off`, `sugestoes-config-off`, `apps-promovidos-off`, `dicas-windows-off`, `experiencias-sob-medida-off` (TailoredExperiences), `destaques-busca-off`.
- **Visual:** `taskbar-animations-off`, `widgets-barra-off` (TaskbarDa).
- **Telemetria (HKLM/admin):** `telemetria-minima` (AllowTelemetry=1, mínimo permitido — não zera), `historico-atividades-off` (EnableActivityFeed).
- **Desempenho (HKLM/admin):** `mmcss-responsiveness`, `priority-separation`, `fast-startup-off`, `hags`.

A UI (`AjustesView`) e o motor propagam o catálogo automaticamente; **não** entrou nada que a `GuardaSeguranca` recuse
(teste `Catalogo_nao_tem_nenhum_tweak_de_seguranca` + novo `Catalogo_tem_ids_unicos_e_cobre_os_grupos_seguros`).

**Catálogo de utilitários ampliado** — `Instaladores.cs`: + HWiNFO, GPU-Z, Process Explorer e Autoruns (Sysinternals,
publisher Microsoft, com verificação de assinatura). Só diagnóstico/leitura; **sem** lançadores de jogos, anticheat ou ativador.

Build 0/0, 85/85 testes.

## 5. 🔜 Próximos (seguros, com paridade comprovada)

Passo a passo sugerido, cada um reversível/só-leitura e com teste:

1. **Enumeração de modos de vídeo (05 §2)** — `EnumDisplaySettingsW`/DEVMODE para listar resolução×Hz suportados e
   permitir escolher a taxa de atualização. *Pré-req:* P/Invoke em `Sevenzin.Diagnostics` (novo `LeitorModosVideo`), seção
   na `GpuView`/`InventarioView`. *Risco:* só leitura (a troca de modo é opcional e reversível). *Teste:* parse do DEVMODE.
2. **Uso de CPU do sistema (05 §3)** — `GetSystemTimes`/`PerformanceCounter` para % de CPU na Home, ao lado do uso de GPU
   já existente. *Risco:* leitura. *Teste:* cálculo de delta de tempos.
3. **Working set / timer sob demanda nos perfis de jogo (04 §8)** — oferecer `SetProcessWorkingSetSize` e `timeBeginPeriod`
   **com aviso explícito** (energia/stutter) e reversão ao fechar o jogo. *Risco:* médio → opt-in, nunca global permanente.
4. **Fila de instaladores persistida (05 §7, lacuna)** — estado da fila de downloads em JSON local (o original não persistia).

## 6. 🟡 Validação manual pendente (não bloqueante)

Aplicar/desfazer os 16 tweaks em máquina real com admin; uso de GPU/CPU sob carga; suavidade das animações e "reduzir
movimento". Registrado em [`revisao_ajustes.md`](../revisao_ajustes.md) e [`paridade_funcional.md`](../paridade_funcional.md).

## 7. ⛔ Fora do escopo (não replicar — §C do charter)

| do original | por quê |
|---|---|
| `EnableLUA=0` (desliga UAC), grupo `uac` | remove consentimento de elevação — `GuardaSeguranca` recusa |
| Remoção forçada de Edge/OneDrive | alto impacto sem opt-in claro |
| Construtor de ISO/USB/debloat do Windows (F5–F8) | instalação de SO; reaproveita binários GPL; alto risco |
| Assistente IA via webhook, HWID/Discord, captura→webhook, KeyAuth | exfiltração / telemetria remota de dados do usuário |
| Desligar Defender/HVCI/mitigações/SmartScreen/Update, write-cache forçado | segurança do SO / risco de perda de dados |

O Sevenzin, no máximo, **detecta e oferece restaurar** o estado protegido desses mecanismos — nunca os desliga.
