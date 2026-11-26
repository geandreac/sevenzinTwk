# Estudo — materiais de otimização (pasta "Otimização") e BoosterX.exe

Data: 26/11/2026 · Análise somente leitura e estática; nada foi executado.

## Material

`Z:\Computador\Downloads\Otimizacao\Otimização`: 108 arquivos (107 baixados do Google Drive), 10 grupos de
duplicatas. 104 scripts e anotações (.reg, .cmd, .bat, .txt) e 4 executáveis:

| Executável | SHA-256 | Assinatura | Defender (1.463.471.0) |
|---|---|---|---|
| `BoosterX.exe` 2.2.2.1 | `457650023111ECAA72ED5AC809D6B1204518F4591E3D81E62A732334026261B1` | Válida (GlobalSign, "IP Ksenofontov Iosif Yurievich", RU) | Nada encontrado |
| `Apps\Msi Mode Tool\._cache_MSI Mode.exe` | `CA4356CF2A4019B773D6342CFD291CD5C2E5E8F7E4C1CB3B6771CEF67006BEC1` | Sem assinatura | Nada encontrado |
| `Apps\Otimizadores\1 - WinSlap.1.4.exe` | `DA936AA54E8C3C2F9C163C644B61EEDB7D57BE69D3836A43BAD95414902E6165` | Sem assinatura | Nada encontrado |
| `Apps\Otimizadores\1 - WinSlap.1.6.exe` | `E45341A979148485D75D90E5CFE7353D48FF79A8F754DB4AD6C794F8BB51144A` | Sem assinatura | Nada encontrado |

## BoosterX.exe (estático)

.NET Framework 4, 32 bits, ofuscado: 2.154 de 2.305 tipos com nomes `#=z…`, os 1.275 textos literais cifrados
e 33 MB de recursos cifrados. Pelos metadados ainda legíveis:
- **Mudanças no sistema:** BCD, registro, serviços, tarefas agendadas, DISM/AppX (debloat), modo MSI, arquivo de
  paginação, timer, perfis do driver NVIDIA (DRS), PowerShell dentro do processo.
- **Licença presa ao hardware:** HWID, expiração da chave.
- **Rede:** envio de dados por HTTP, Discord RPC, IGDB.
- **Hardware:** LibreHardwareMonitor, que costuma depender do driver WinRing0 (na lista de drivers vulneráveis da
  Microsoft).

Sem desofuscar não dá para listar os ajustes exatos nem os servidores contatados. Análise dinâmica não foi feita
nem é necessária: nada do Sevenzin depende do comportamento interno do BoosterX.

## O que entrou no Sevenzin (e por quê)

| Item | Motivo |
|---|---|
| Ponto de restauração antes de otimizar (`PontoRestauracao`, `PontoAntes`) | Segurança: verifica, cria via WMI e só declara sucesso se o ponto novo aparecer na lista do Windows |
| Prefetch desmarcado na Limpeza | Apagá-lo deixa a abertura de programas mais lenta até o Windows refazê-lo |
| Inicialização (`Inicializacao`, `TempoBoot`) | Maior ganho mensurável de boot; mesmo mecanismo do Gerenciador de Tarefas; tempo de boot vindo do evento 100 |
| Timers de boot (`TimersBoot`) | Detecta `useplatformclock`/`useplatformtick`/`disabledynamictick`/`tscsyncpolicy` alterados por scripts e volta ao padrão, com desfazer e aviso de BitLocker |
| `game-mode-on`, `power-throttling-off`, `apps-segundo-plano-off`; aviso de `large-cache-on` | Benefício plausível, reversível, com risco explicado |

## O que foi rejeitado

- **Segurança:** DEP desligado (`nx optout/AlwaysOff`), `integrityservices disable`, todas as mitigações via
  `Set-ProcessMitigation`, TPM entropy, Hyper-V/VBS desligados no boot.
- **Apagar dados e diagnóstico:** apagar todos os logs de eventos, `del *.log /s` na raiz do C:.
- **Desligar coisas úteis:** serviços em massa (Bluetooth, Área de Trabalho Remota, diagnóstico, notificações,
  impressão, Delivery Optimization), manutenção automática, Prefetcher/Superfetch.
- **Instabilidade e efeitos colaterais:** `csrss` com prioridade alta, VRR desligado, firewall bloqueando IPs do
  Google, DPI forçado em 96, `AutoEndTasks`.
- **Sem efeito:** chaves obsoletas (PagedPoolSize, SystemPages, PAE, `increaseuserva`, `nolowmem`) e
  "Monitor 1–7ms", que é um ajuste interno do driver AMD.

## Medição

Mesmas condições (tomada, resolução, nada aberto), 3 medições antes e 3 depois, reiniciando entre elas:
- **Boot:** tela Windows (evento 100; exige o serviço DPS ativo).
- **Jogos:** Benchmark, PresentMon ou CapFrameX (FPS médio, 1% low, frametime p99).
- **Latência:** tela de DPC.
- **CPU e memória ociosas:** Telemetria.
