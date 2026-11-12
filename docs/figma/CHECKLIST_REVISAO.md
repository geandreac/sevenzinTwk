# CHECKLIST_REVISÃO — Item 4: Fluxo de Admin/Login

Arquivo Figma: **Projeto Sevenzin 2026** · fileKey `SxC2LoJThQfhbyqQtuTf56` · Login `10:866` / estados `10:1048` · Admin (página) `16:713`.
Itens 1–3 do checklist original (ícones, tipografia, logo) e a revisão de telas estão em `levantamento.md`, `ajustes_tela_inicial.md` e `revisao_telas.md`.

## Arquitetura (SOLID, UI-agnóstica em Sevenzin.Diagnostics)
| Camada | Tipo | Arquivo |
|---|---|---|
| Domínio | `Usuario` (e-mail, papel, hash, criadoUtc), `PapelUsuario` | `src/Sevenzin.Diagnostics/Usuarios.cs` |
| Hash | `SegurancaSenha` (bcrypt, work factor 12, anti-timing) | `Usuarios.cs` |
| Validação | `ValidacaoCredenciais` (e-mail + senha) | `Usuarios.cs` |
| Repositório | `IRepositorioUsuarios` + `RepositorioUsuariosArquivo` (JSON) + `RepositorioUsuariosMemoria` (testes) | `Usuarios.cs` |
| Serviço de contas | `ServicoContas` (criar/remover/validar) | `Usuarios.cs` |
| Serviço de auth | `ServicoAutenticacao` (login + força bruta + erro genérico) | `Autenticacao.cs` |
| Sessão | `Sessao` + `ServicoSessao` (abrir/validar/renovar/encerrar) | `Autenticacao.cs` |
| Autorização | `Autorizacao.PodeAcessarAdmin` (papel + validade) | `Autenticacao.cs` |
| Seed | `SemeadorAdmin` (variáveis de ambiente) | `Autenticacao.cs` |
| Composição | `Servicos` (localizador estático do processo) | `src/Sevenzin.Wpf/Servicos.cs` |
| UI Login | `LoginWindow` | `src/Sevenzin.Wpf/LoginWindow.xaml(.cs)` |
| Gate | `App.OnStartup` (seed → login → MainWindow) | `src/Sevenzin.Wpf/App.xaml.cs` |
| Proteção de rota | `MainWindow` (esconde aba Admin) + `AdminView` (guarda de sessão) | `MainWindow.xaml(.cs)`, `Views/AdminView.xaml.cs` |

## Itens do checklist (item 4)
| # | Requisito | Status | Como |
|---|---|---|---|
| 4.1 | Login por e-mail + senha, **sem criar conta/social/licença**, admin-only | ✅ | `LoginWindow` só tem e-mail+senha; removido o "Ainda não tem licença? Comprar" do Figma. Sem signup público — contas vêm do seed (env) ou são criadas por um admin na tela Admin. |
| 4.2 | Layout fiel ao frame de Login do Figma | ✅ | `LoginWindow.xaml` reproduz logo S-raio, "SEVENZIN / PC OPTIMIZER", "Acesse sua conta", campos com ícone/olho, "Lembrar de mim", "Esqueci minha senha", "Entrar" e painel decorativo. Evidência: `v3_login.png`. |
| 4.3 | Camadas controller/service/repository com SOLID | ✅ | Ver tabela de arquitetura — repositório abstraído por interface, serviços sem dependência de UI. |
| 4.4 | Validação front **e** back | ✅ | `LoginWindow`/`AdminView` validam na UI; `ServicoContas`/`ServicoAutenticacao`/`ValidacaoCredenciais` revalidam no domínio. |
| 4.5 | Erros genéricos (não revela se o e-mail existe) | ✅ | `ServicoAutenticacao` devolve a mesma mensagem ("E-mail ou senha inválidos.") para senha errada e e-mail inexistente, **e** gasta o mesmo tempo (verificação bcrypt contra `HashFalso`) para não vazar por timing. Teste dedicado. |
| 4.6 | Hash forte (argon2 **ou** bcrypt) | ✅ | **bcrypt** (BCrypt.Net-Next 4.2.0), work factor 12, salt aleatório embutido. PBKDF2 anterior removido. |
| 4.7 | Sessão/token com expiração, renovação e logout | ✅ | `ServicoSessao`: token aleatório de 32 bytes, expiração absoluta de 30 min **deslizante** (renova a cada navegação), `Encerrar()` no botão "Sair". |
| 4.8 | Rotas protegidas por papel Admin | ✅ | `MainWindow` não cria a aba Admin sem `PodeAcessarAdmin`; `AdminView` ainda checa a sessão (defesa em profundidade) e mostra "Sem permissão". |
| 4.9 | Força bruta (limite + bloqueio temporário) | ✅ | `PoliticaForcaBruta` (padrão: 5 falhas em 5 min → bloqueio de 5 min); durante o bloqueio nem a senha certa entra. Testes de bloqueio e de expiração do bloqueio. |
| 4.10 | "Esqueci minha senha" só se existir no Figma | ✅ | Existe no Figma → implementado como diálogo informativo seguro (app offline: redefinir via outro admin ou re-seed por env), **sem** fluxo falso de e-mail. |
| 4.11 | Seed do admin por variável de ambiente (sem credencial no código) | ✅ | `SemeadorAdmin` lê `SEVENZIN_ADMIN_EMAIL` / `SEVENZIN_ADMIN_SENHA`; idempotente; nenhuma credencial no código. |
| 4.12 | Sem stubs/TODOs/placeholders | ✅ | Todo o fluxo é funcional (login, gate, criação de contas, logout, auditoria). |
| 4.13 | Testes de login | ✅ | `tests/.../AutenticacaoTests.cs`: sucesso, senha errada, e-mail inexistente (mesmo erro), campos vazios, bloqueio por força bruta, expiração do bloqueio, expiração/renovação de sessão, **sem permissão**, hash bcrypt, seed por env. `UsuariosTests.cs` migrado para a nova API. |
| 4.14 | Regressão | ✅ | `dotnet test` → **78/78 aprovados** (67 anteriores + novos). |

## Decisões e divergências (justificadas)
- **Senha nunca em texto**: só o hash bcrypt é persistido (`usuarios.json` em `%LOCALAPPDATA%\Sevenzin`). Teste garante que o arquivo não contém a senha.
- **Primeiro administrador (bootstrap)**: se não há contas **e** não há seed por env, o Login permite criar o primeiro admin (único caso de criação pela tela de login). Some assim que existir qualquer conta. Evita o impasse "sem conta, ninguém entra".
- **"Lembrar de mim"**: guarda só o **e-mail** (nunca a senha) em `ultimo_email.txt`.
- **Sem licença/HWID/Discord**: o Figma de Login traz "Comprar licença"; removido pelo charter (o original usava login HWID/Discord). O gate é 100% local.
- **"Esqueci minha senha" offline**: sem servidor de e-mail, não há reset automático; o diálogo orienta o caminho seguro.
- **Modo render** (`--render ... login|<tela>`): abre sessão admin sintética para permitir os screenshots; não persiste nada.

## Comandos e evidências
- `dotnet add ... BCrypt.Net-Next` → 4.2.0 instalado.
- `dotnet build src\Sevenzin.Wpf` → **0 erros, 0 avisos**.
- `dotnet test` → **78/78 aprovados**.
- Renders: `v3_login.png` (tela de login — modo primeiro administrador), `v3_admin.png` (tela Admin protegida, com usuário + "Sair").

## Pendências
- Comparar a tela Admin contra os frames ADM-00..10 (`16:713`) em detalhe de layout (hoje a UI é funcional e segue o sistema visual do app; a comparação fina fica como próximo passo).
