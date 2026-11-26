# Vínculo da conta ao computador (HWID) e sessão única

Objetivo: cada acesso vendido vale para **um computador**. Quem receber o login de outra pessoa não consegue entrar
em outro PC, e a mesma conta não fica aberta em dois lugares ao mesmo tempo.

## Como funciona

| Etapa | Onde | O que acontece |
|---|---|---|
| 1. Identidade do PC | App (`IdentidadeDispositivo`) | Lê 4 identificadores: MachineGuid do Windows, UUID da placa (SMBIOS), serial da placa-mãe e ID do processador. Descarta valores genéricos de fábrica ("To be filled by O.E.M.", só zeros). Cada um vira um hash SHA-256 com sal do app. |
| 2. Login | App → Supabase Auth | E-mail e senha, e o código 2FA se estiver ativo. Nada muda aqui. |
| 3. Vínculo | App → `rpc/registrar_dispositivo` | Envia só os hashes. **O servidor decide**: no 1º login grava o PC; depois aceita se o hash geral for igual ou se **pelo menos 2 dos 4 componentes** coincidirem. Fora disso responde `outro_dispositivo`, registra "Bloqueado: outro computador" em `acessos`, e o app encerra a sessão. |
| 4. Sessão única | Servidor | Cada login aceito gera uma `sessao_id` nova e invalida a anterior. |
| 5. Conferência | App (a cada 5 min) → `rpc/validar_sessao` | Se a sessão deixou de ser a ativa (a conta entrou em outro PC, ou o admin liberou o vínculo), o app avisa e volta para o login. Erro de rede não derruba ninguém. |
| 6. Troca de PC | App Admin → `rpc/resetar_dispositivo` | Botão **Liberar PC** na lista de contas. Apaga o vínculo; o próximo login vincula o PC usado. Fica registrado em `acessos`. |

Regras de segurança aplicadas:
- **Falha fechada.** Sem resposta do servidor, resposta desconhecida ou função não publicada, o login é recusado.
- **Escrita só pelas funções.** A tabela `dispositivos` tem RLS sem policy de escrita. Nem o próprio usuário apaga ou troca o vínculo pela API REST, e só o admin lê.
- **Entradas validadas no servidor.** Hashes precisam ter 64 caracteres hex, com no máximo 8 componentes.
- **Admin isento do vínculo.** Precisa entrar no Admin de mais de um lugar.
- **Privacidade (LGPD).** Os identificadores brutos nunca saem do PC. O hash tem sal próprio, então não serve para rastrear o PC em outro serviço.
- **Modo local.** Contas em JSON ou Mongo, sem Supabase, não têm vínculo: as contas já ficam no próprio PC.

## Publicação (ordem importa)

1. No SQL Editor do Supabase, rode a seção "vínculo da conta ao computador" de `docs/supabase/schema.sql`. Ela é idempotente.
2. Rode `docs/supabase/testes-vinculo.sql`. Ele cria duas contas de teste temporárias, todos os blocos devem imprimir `OK`, e o script termina em ROLLBACK, sem gravar nada (nem as contas).
3. Só então publique a versão do app. Como a falha é fechada, um app novo contra um servidor sem as funções recusa todos os logins.
4. Contas que já existem se vinculam sozinhas no próximo login, ao PC em que entrarem primeiro.
5. Informe nos termos e no FAQ que o acesso é vinculado a um computador e que a troca é feita por ticket.

## Como validar a eficácia

### 1. Testes automáticos (a cada build)

`dotnet test` cobre a parte que roda no app (`IdentidadeDispositivoTests`):
- valores genéricos ou vazios não contam como identidade;
- o mesmo hardware sempre gera o mesmo HWID, e só hashes no formato que o servidor exige;
- trocar uma peça mantém 3 componentes iguais, que o servidor aceita;
- outro PC não coincide em nenhum componente;
- a resposta do servidor libera só `vinculado`, `ok` e `admin`. Qualquer outra coisa, inclusive nenhuma resposta, bloqueia.

### 2. Testes do servidor (antes de publicar e após mudar o SQL)

`docs/supabase/testes-vinculo.sql` prova, no próprio banco:

| # | Cenário | Esperado |
|---|---|---|
| 1 | Primeiro login | `vinculado` |
| 2 | Mesmo PC entra de novo | `ok`, e a sessão anterior deixa de valer |
| 3 | Uma peça trocada (3 de 4 iguais) | `ok` |
| 4 | Outro computador | `outro_dispositivo` |
| 5 | Só 1 componente igual | `outro_dispositivo` |
| 6 | Hash malformado | erro, nada gravado |
| 7 | Usuário tenta apagar ou alterar o próprio vínculo pela API | nada muda |
| 8 | Conta comum tenta liberar um PC | erro |
| 9 | Uma conta tenta ler o vínculo de outra | não vê nada |

### 3. Teste ponta a ponta (dois PCs reais e uma conta de teste)

| Passo | Ação | Resultado esperado | Onde conferir |
|---|---|---|---|
| A | Entrar no PC 1 | Entra | `dispositivos` ganha a linha da conta |
| B | Fechar e entrar de novo no PC 1 | Entra | `ultimo_utc` atualizado |
| C | Entrar com a mesma conta no PC 2 | "Esta conta está vinculada a outro computador" | `acessos`: "Bloqueado: outro computador" |
| D | Login automático ligado no PC 2 | Cai na tela de login, sem abrir o app | — |
| E | Admin → Liberar PC na conta | Mensagem de sucesso | `acessos`: "Computador liberado pelo admin" |
| F | Em até 5 min, no PC 1 ainda aberto | "Sua conta foi aberta em outro computador…" e volta ao login | — |
| G | Entrar no PC 2 | Entra e vincula o PC 2 | `dispositivos` com os hashes do PC 2 |
| H | Tirar a internet do PC 2 com o app aberto | O app continua aberto | Rede fora não derruba a sessão |
| I | Reinstalar o Windows no PC 2 (ou trocar o disco) | Entra normalmente | MachineGuid muda, os outros 3 componentes seguram |
| J | Conta com 2FA no PC 2 | Pede o código e depois confere o PC | — |

### 4. Testes adversariais (tentativas de burlar)

| Tentativa | Resultado esperado |
|---|---|
| Chamar `rpc/registrar_dispositivo` com o token de outra conta | Atua só na conta do token (`auth.uid()`), nunca em outra |
| `DELETE` ou `PATCH` em `/rest/v1/dispositivos` com o token do usuário | Nada muda (RLS) |
| Chamar `rpc/resetar_dispositivo` sem ser admin | Erro "apenas administradores…" |
| Chamar as funções sem login (só a chave anon) | Recusado (execute revogado de `anon`) |
| Bloquear o domínio do Supabase no firewall durante o login | Login recusado (falha fechada) |
| Copiar a pasta `%LOCALAPPDATA%\Sevenzin` (credenciais lembradas) para outro PC | Credenciais protegidas por DPAPI não abrem no outro PC; digitando a senha, cai no bloqueio do passo C |

### 5. Monitoramento contínuo

- Em **Admin → Acessos**, acompanhe "Bloqueado: outro computador". Uma conta com vários bloqueios seguidos indica compartilhamento de senha: fale com o cliente pelo ticket.
- Muitos pedidos de "Liberar PC" para a mesma conta também indicam revenda do acesso.
- Meta de qualidade: nenhum ticket de "fui bloqueado no meu próprio PC". Se aparecer, compare os componentes do PC (o valor genérico da placa é a causa mais comum) antes de mexer na regra dos 2 de 4.

## Limites conhecidos (seja honesto com o cliente e com você)

- **O HWID é declarado pelo cliente.** Um app modificado pode enviar os hashes de outro PC. Isso exige que o dono da conta entregue os próprios hashes e que alguém altere o binário, o que já é bem mais difícil do que repassar e-mail e senha. A **sessão única** continua valendo mesmo nesse caso: duas pessoas não usam a conta ao mesmo tempo sem uma derrubar a outra a cada 5 minutos.
- **O app roda no PC do cliente.** Quem altera o binário pode pular a checagem e usar as funções locais. A proteção forte aqui é a do servidor (contas, vínculo e sessão), não a do executável. Ofuscação e assinatura do binário são o próximo passo, se isso virar problema.
- **Máquinas virtuais** clonadas podem repetir componentes. Para o público do produto (PCs de jogo) isso é raro.
