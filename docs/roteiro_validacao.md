# Roteiro de validação em máquina controlada (P0)

Objetivo: transformar a paridade "em código+testes" em **paridade medida** — aplicar, reler o estado real e
registrar o diff antes/depois. Protocolo §2.4/§11.3 do plano.

> ⚠ Rode em **máquina/VM controlada**, como administrador, com ponto de restauração. **Nunca** nesta máquina de
> desenvolvimento sem confirmação explícita. Cada grupo: **ler → aplicar → reler → desfazer → reler**.

## Formato de evidência

```
E-### | [CONFIRMADO/DIVERGENTE/LACUNA] | grupo | comando | antes: <valor> | depois: <valor> | desfeito: <valor>
```

## Grupos

### 1. Plano de energia + hibernação
- `powercfg /getactivescheme` antes; "Criar e ativar Sevenzin"; depois confirmar que o plano "Sevenzin" está ativo
  e que `powercfg /q <guid>` reflete os 134 índices (amostrar 3).
- Hibernação: `reg query HKLM\SYSTEM\CurrentControlSet\Control\Power /v HibernateEnabled` antes/depois de
  "Desativar hibernação" (esperado 0) e "Reativar" (esperado 1).

### 2. Rede global (TCP)
- `netsh int tcp show global` antes; "Aplicar (normal + ctcp)"; reler (esperado autotuning=normal, congestion=ctcp);
  "Restaurar padrão"; reler (default).

### 3. Rede por adaptador
- `Get-NetAdapter` para o GUID; `reg query HKLM\...\Tcpip\Parameters\Interfaces\{GUID} /v TcpAckFrequency` antes/depois
  (esperado 1, 1, 0; NetbiosOptions 2; EnableMulticast 0); "Desfazer" e confirmar a remoção.

### 4. UAC completo (9 itens) + Defender + SmartScreen + HVCI + mitigações
- `reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System` (EnableLUA + 8) antes/depois;
  HVCI em `...\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled`; mitigações `FeatureSettingsOverride`
  (esperado 33554435). **Requer reinício** para UAC/HVCI/mitigações valerem — marcar "aplicado (reinício pendente)".
- Defender: validar se o valor é respeitado com Tamper Protection (documentar se for ignorado).

### 5. GPU (TdrDelay, AMD power gating, HAGS, modo de vídeo)
- `reg query ...\GraphicsDrivers /v TdrDelay` (esperado 8) e `HwSchMode` (2); AMD só em máquina AMD.
- `LeitorModosVideo` já lê o modo real — validar resolução×Hz exibidos.

### 6. CPU / memória / MMCSS
- `SystemResponsiveness` (10), `Win32PrioritySeparation` (38), MMCSS Games (`GPU Priority`=8, `Priority`=2,
  `SFIO Priority`="Normal", `Scheduling Category`="Medium"), `DisablePagingExecutive`/`LargeSystemCache` (1).

### 7. Mouse / visual / privacidade
- `reg query HKCU\Control Panel\Mouse /v MouseSpeed` ("0"), `...\Themes\Personalize /v EnableTransparency` (0),
  `...\Windows Error Reporting /v Disabled` (1), `...\WindowsCopilot /v TurnOffWindowsCopilot` (1).

### 8. Jogos (perfil aplicado ao processo correto)
- Abrir um jogo (ex.: CS2) e "Aplicar": confirmar afinidade/prioridade no Gerenciador de Tarefas; fechar → reverte.
- CPU Sets (P-cores) em CPU híbrida: validar com `Get-Process -Id <pid>` / utilitário de afinidade.
- Detecção Steam/Epic: conferir a lista detectada contra a biblioteca real.

### 9. Instaladores
- 1 download (Sysinternals): assinatura Microsoft válida; falha de hash descarta o arquivo; winget de 1 ID da allowlist.

### 10. Capturas
- 4 modos (tela/monitor/região/janela) + editor + salvar/copiar; medir tempo de desenho (cenário: imagem 4K) antes/depois.

### 11. Admin único (Geandre)
- Sem `SEVENZIN_ADMIN_EMAIL` → bootstrap nega com instrução; com env = Geandre → cria admin; outro e-mail → nega.
- Convite: gerar → cadastrar com o código → conta Usuario; reutilizar o código → recusa; verificar `convites.json` só com hash.
- `GarantirAdminUnico`: injetar admin legado → rebaixado.

### 12. 2FA / Supabase
- Ativar 2FA no Perfil → login pede o código (Google Authenticator real); código errado → falha.
- Supabase (se configurado): login + papel via RLS.

## Registro final

Preencher `docs/validacao_paridade.md` §9 com as linhas E-### e reclassificar itens que divergirem na releitura.
