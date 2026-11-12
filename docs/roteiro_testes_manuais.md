# Roteiro de testes manuais — Sevenzin

App WPF .NET 8. Marque cada item `[x]` ao validar. "Esperado" = o que deve acontecer.
Legenda: ⚠️ = altera o sistema de verdade · 🔒 = verificação de segurança (o app NÃO pode fazer).

## 0. Pré-requisitos
- [ ] Testar numa **máquina/VM que você controla** (a tela Ajustes mexe no registro do Windows de verdade).
- [ ] Criar um **ponto de restauração** do Windows antes de aplicar tweaks.
- [ ] Ter o .NET 8 do projeto no PATH.

Iniciar:
```bash
cd "Z:\Computador\Documentos\Projetos\sevenzinTwk"; $env:DOTNET_ROOT='C:\re\geandre\dotnet'; $env:PATH="C:\re\geandre\dotnet;$env:PATH"; dotnet run --project src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug
```

## 1. Inicialização e gate de login
- [ ] Ao abrir, aparece a **tela de Login** (não a janela principal). **Esperado:** gate obrigatório.
- [ ] Sem nenhuma conta, o título é **"Crie o primeiro administrador"** e há campo "Confirmar senha". **Esperado:** modo bootstrap.

## 2. Primeiro administrador (bootstrap)
- [ ] Digitar e-mail inválido (`abc`) + senha fraca (`123`) → **erros listados**; não cria.
- [ ] Senha com menos de 12, ou sem símbolo/maiúscula/número → **rejeitada** com a regra correspondente.
- [ ] E-mail válido + senha forte (ex.: `Sevenzin#2026`) + confirmar igual → **entra como admin** e abre a janela principal.
- [ ] Reabrir o app → agora mostra **login normal** (e-mail + senha + "Lembrar de mim" + "Esqueci minha senha" + "Entrar"), sem "Confirmar senha".

## 3. Login normal
- [ ] Credenciais corretas → entra.
- [ ] Senha errada → **"E-mail ou senha inválidos."** (mensagem genérica).
- [ ] E-mail inexistente → **a mesma** mensagem genérica (não revela se o e-mail existe). 🔒
- [ ] Errar a senha **5 vezes** → **bloqueio temporário**; mesmo com a senha certa, continua bloqueado por alguns minutos.
- [ ] Ícone do olho mostra/oculta a senha.
- [ ] "Lembrar de mim" marcado → ao reabrir, o **e-mail** vem preenchido (a senha **nunca**). 🔒
- [ ] "Esqueci minha senha" → diálogo explicando reset local (sem fluxo falso de e-mail).

## 4. Sessão e proteção de rota
- [ ] Botão **"Sair"** (topo direito) → volta ao login.
- [ ] Criar uma conta **não-admin** (ver passo 5), sair, logar com ela → a aba **Admin não aparece**. 🔒
- [ ] (Opcional) Deixar a janela ociosa > 30 min e navegar → sessão expira e pede login de novo.

## 5. Área Admin (só administrador)
- [ ] Topo: eyebrow "ADMINISTRAÇÃO" + título "Usuários" + **cartões Total/Administradores/Usuários** com contagem real.
- [ ] Em "Criar login": preencher Nome, E-mail; clicar **"Gerar senha forte"** → preenche senha e mostra a senha gerada; o **medidor** fica cheio e os 5 requisitos ficam verdes.
- [ ] Digitar uma senha que contenha o próprio nome/e-mail → requisito "Não contém o nome nem o e-mail" **não** fica verde; criação é barrada.
- [ ] Criar uma conta **Usuário** (sem marcar admin) e uma **Administrador** → aparecem na lista com avatar (iniciais) e selo de perfil; os cartões de métrica atualizam.
- [ ] Tentar **remover a própria conta logada** → botão desabilitado.
- [ ] Remover outra conta → some da lista.
- [ ] "AUDITORIA DE OTIMIZAÇÕES" lista as mudanças aplicadas (preenche após o passo 8).
- [ ] 🔒 Abrir `%LOCALAPPDATA%\Sevenzin\usuarios.json` → só há **hash bcrypt** (`$2...`), **nenhuma senha em texto**.

## 6. Navegação / sidebar
- [ ] Sidebar estreita (só ícones); **hover** mostra tooltip com o nome da rota.
- [ ] Botão **hambúrguer** expande a sidebar (mostra rótulos) e recolhe.
- [ ] Navegação por **teclado** (Tab/setas) percorre os itens; item ativo tem indicador lateral azul.
- [ ] Título no topo acompanha a tela selecionada.

## 7. Início (monitores)
- [ ] CPU/Memória/Latência mostram **valores reais** que mudam ao longo do tempo (sparkline oscila).
- [ ] GPU mostra **"—"** (indisponível), **sem** gráfico falso.
- [ ] Badge "Estável" na latência **some** se desconectar a rede (e volta ao reconectar). 🔒 (valor real)
- [ ] Faixa "Sistema" mostra CPU/GPU/RAM/SO reais; "Recursos" com os dois cartões em degradê.

## 8. Ajustes ⚠️
- [ ] Ligar um tweak (ex.: "Desligar Game DVR") → pede/usa admin; aplica.
- [ ] Conferir no Windows que a mudança ocorreu; **desligar** o tweak → **reverte**.
- [ ] Voltar ao Admin → a ação aparece na **auditoria** (de/para).
- [ ] 🔒 Confirmar que **não há** opção de "desligar o Windows Defender" nem "debloat que remove apps" (removidos pelo charter).

## 9. GPU
- [ ] Mostra dados reais da placa (fabricante/driver/VRAM/modo).
- [ ] "Abrir página oficial do driver" abre o site do fabricante.
- [ ] 🔒 Não há toggles que alterem clock/memória/flags do driver nem DDU.

## 10. Instaladores
- [ ] "Abrir página oficial" abre a fonte oficial.
- [ ] "Baixar e verificar" só prossegue se a **assinatura** conferir; nada executa sem você. 🔒

## 11. Jogos
- [ ] "Adicionar jogo" → escolher um `.exe`; cria um perfil.
- [ ] Perfil aplica afinidade/prioridade **temporárias**, revertidas ao fechar o jogo. 🔒

## 12. Capturas
- [ ] "Capturar tela" salva a imagem **localmente** (pasta configurável); "Abrir pasta" mostra o arquivo.
- [ ] 🔒 Nada é enviado à rede. **Confirme** com o Monitor de Recursos/firewall que o app não faz conexões de saída ao capturar.

## 13. Configurações
- [ ] Abas Geral/Capturas/Preferências funcionam; toggles salvam automaticamente.
- [ ] 🔒 "Iniciar com o Windows" usa atalho no usuário (sem admin) e **não** menciona Discord ID/licença.

## 14. Inventário / Telemetria / Benchmark
- [ ] Inventário: tabela só-leitura com coluna **Confiança** (Fato/Inferência).
- [ ] Telemetria: "Iniciar medição" gera série e comparação A/B.
- [ ] Benchmark: "Rodar benchmark" produz frametime interno reprodutível.

## 15. Telas intencionalmente ausentes (segurança) 🔒
- [ ] **BIOS** e **Windows (ISO)** mostram placeholder "em migração" — **confirmar que não há** flash de BIOS nem builder de ISO.

## 16. Verificações de segurança globais 🔒
Durante toda a sessão, confirmar que o app **NÃO**:
- [ ] desativa Windows Defender / UAC / SmartScreen / Windows Update;
- [ ] envia IP/HWID/screenshots para a rede (webhook/Discord) — monitorar tráfego;
- [ ] altera a hora do sistema (`SetSystemTime`);
- [ ] guarda qualquer senha em texto.

---
### Resultado
- Testes automatizados de referência: `dotnet test` → **81/81** (inclui login: sucesso, senha errada, inexistente, bloqueio, expiração, sem permissão, hash, seed).
- Anote aqui as divergências encontradas no teste manual: …
