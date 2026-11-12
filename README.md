# Sevenzin — otimizador de PC (reconstrução segura)

Aplicativo de **diagnóstico e otimização** do Windows, em WPF (.NET 8), que **reconstrói de forma segura** um
programa analisado por engenharia reversa. O princípio é simples: **medir antes de otimizar**, só leitura primeiro,
tudo **reversível, auditado e confirmado** por você.

> **Princípio de operação:** toda alteração no Windows tem **explicação, confirmação explícita e reversão**
> (prévia, backup, desfazer e auditoria no motor de tweaks). Nenhuma otimização roda sem o usuário pedir.
> As telas avançadas (UAC, Defender, ISO/USB, assistente) entram por fases — ver `docs/revisao_ajustes.md`.

## Funcionalidades

- **Login + Admin** por e-mail e senha: hash **bcrypt**, sessão com expiração/renovação/logout, proteção de rota por
  papel (administrador × usuário), proteção contra força bruta e seed do primeiro admin por variável de ambiente.
- **Início** — monitores ao vivo de CPU (`GetSystemTimes`), GPU, memória e latência (leituras reais; "—" quando a
  métrica é indisponível, nunca um valor forjado) + faixa de sistema + atalhos.
- **Ajustes** — otimizações **reversíveis** com prévia, backup do estado anterior, desfazer e auditoria.
- **GPU** — diagnóstico somente leitura (sem DDU, sem alterar flags do driver), **modos de vídeo suportados**
  (resolução × Hz via DEVMODE, só listagem) + link oficial do fabricante.
- **Instaladores** — utilitários de **fontes oficiais** com verificação por assinatura/hash antes de executar e
  **fila persistida** em JSON local (sobrevive a reinícios).
- **Jogos** — perfil por jogo **temporário** (afinidade/prioridade revertidas ao fechar) + **working set e timer de
  1 ms opt-in** com aviso, também revertidos ao fechar + **detecção automática de jogos Steam/Epic instalados**
  (só leitura; adicionar ao catálogo não aplica nada).
- **Capturas** — 4 modos (tela inteira/monitor/região/janela) + editor de anotações; salva local por padrão; envio
  só com webhook configurado e confirmação por envio.
- **Assistente** — conversa por endpoint HTTPS **configurado por você**; cada mensagem é enviada só quando você
  aperta Enviar; contexto do PC só com consentimento.
- **Windows** — estado só leitura + plano de energia "Sevenzin" (duplicável/restaurável) + **análise de ISO do
  Windows somente leitura** (edições, apps e features via DISM).
- **BIOS** — tela **informativa e segura** (estado só leitura + instruções com classificação de risco).
- **Inventário / Telemetria / Benchmark** — inventário com grau de confiança (Fato/Inferência), medição A/B e
  benchmark de frametime interno.

## Arquitetura

- `src/Sevenzin.Diagnostics/` — biblioteca de coleta e lógica, **sem UI** (WMI, contadores de performance, motor de
  tweaks reversível, autenticação). Testável de forma isolada.
- `src/Sevenzin.Wpf/` — aplicativo **WPF** (tema escuro alinhado ao Figma, tipografia Inter, ícone oficial "S em raio").
- `src/Sevenzin.Admin/` — **app Admin separado** (auditoria de tweaks, log de acessos, segurança) — local, não
  publicado no GitHub.
- `src/Sevenzin.Report/` — runner de linha de comando (`relatorio-pc.md`/`.json`, self-test).
- `tests/Sevenzin.Tests/` — **158 testes** automatizados (motor de tweaks, autenticação + MFA, GPU, inventário,
  modos de vídeo, CPU, timer, fila, detecção de jogos, DISM, assistente…).
- `docs/` — análise do original, design system, paridade funcional e revisões.

## Como usar

Requisitos: **.NET SDK 8** no Windows.

```bash
# compilar
dotnet build src/Sevenzin.Wpf/Sevenzin.Wpf.csproj -c Debug

# rodar o app
dotnet run --project src/Sevenzin.Wpf/Sevenzin.Wpf.csproj -c Debug

# testes
dotnet test
```

Ao abrir pela primeira vez, o app pede para **criar o primeiro administrador** (e-mail + senha forte: ≥12 caracteres,
com maiúsculas/minúsculas, número e símbolo). Depois disso, o login passa a ser normal.

Para provisionar o admin sem interface (ex.: em uma instalação de escola), defina antes de abrir:

```bash
set SEVENZIN_ADMIN_EMAIL=admin@suaescola.edu
set SEVENZIN_ADMIN_SENHA=UmaSenhaForte#2026
```

> ⚠️ A tela **Ajustes** altera o registro do Windows de verdade (reversível e auditado, pede administrador). Teste em
> uma máquina/VM que você controla e, de preferência, com um ponto de restauração criado.

## Como clonar

```bash
git clone https://github.com/geandreac/sevenzinTwk.git
cd sevenzinTwk
dotnet build src/Sevenzin.Wpf/Sevenzin.Wpf.csproj -c Debug
dotnet run --project src/Sevenzin.Wpf/Sevenzin.Wpf.csproj -c Debug
```
