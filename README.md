<p align="center">
  <img src="docs/adicionais/Logo/SevenzinTwks_discord_1024.png" alt="Logo do SevenzinTwks" width="160">
</p>

<h1 align="center">SevenzinTwks</h1>

<p align="center">
  <b>Otimização de PC para jogos, com segurança: mede antes, explica cada ajuste e desfaz com um clique.</b><br>
  Windows 10 e 11 · 64 bits
</p>

<p align="center">
  <a href="https://discord.gg/W7wC4dWNYc"><b>Comprar acesso no Discord</b></a> ·
  <a href="https://github.com/geandreac/sevenzinTwk/releases/latest">Baixar a versão mais recente</a>
</p>

---

## O que é

O SevenzinTwks é um otimizador de Windows feito para quem joga e quer o PC mais rápido **sem arriscar o sistema**.

A maioria dos otimizadores aplica dezenas de mudanças de uma vez, sem dizer o que fez, e deixa você sem caminho de
volta. O SevenzinTwks trabalha ao contrário:

| Princípio | Na prática |
|---|---|
| **Medir antes de otimizar** | Mostra o estado real do PC (CPU, GPU, memória, latência, boot) antes de qualquer mudança. Quando um dado não está disponível, aparece "—", nunca um número inventado. |
| **Cada ajuste é explicado** | Todo item diz o que muda, o risco e se precisa reiniciar. |
| **Tudo tem volta** | Backup do valor anterior, botão de desfazer e histórico de tudo o que foi aplicado. |
| **Ponto de restauração** | Antes de otimizar, o app cria um ponto de restauração do Windows e confere se ele foi criado de verdade. |
| **Você decide** | Nada é alterado, baixado ou executado sem a sua confirmação. Nada é enviado pela internet em segundo plano. |

## Funcionalidades

### Uso diário

- **Início:** painel ao vivo de CPU, memória e latência, com gráficos em tempo real, resumo do sistema e um botão
  "Otimizar" que mostra antes a lista exata do que será aplicado.
- **Ajustes:** 84 otimizações reversíveis, organizadas por tema: desempenho, jogos, GPU, mouse, rede, memória,
  visual, privacidade, telemetria e segurança. Cada uma liga e desliga com um toque, com prévia, backup e desfazer.
- **Jogos:** perfil por jogo, com prioridade, afinidade de núcleos (P-cores, sem o Core 0, sem SMT, 1º CCD) e modo de
  compatibilidade. Tudo é **temporário**: volta ao normal quando o jogo fecha. Também detecta os jogos instalados na
  Steam e na Epic e diz o quanto o PC está pronto para jogar.

### Hardware

- **GPU:** ajustes de driver NVIDIA e AMD, 7 perfis gráficos prontos, instalação do driver NVIDIA sem os
  componentes extras (direto dos servidores oficiais, com assinatura conferida) e overclock dentro dos limites da
  própria placa.
- **Meu PC:** inventário completo do hardware, com a origem e o grau de confiança de cada informação, sem coletar
  dados pessoais.
- **BIOS:** diagnóstico da plataforma (Secure Boot, TPM, modo de boot) e orientações seguras. O app **não altera** a
  BIOS: só informa.

### Sistema

- **Windows:** informações do sistema, programas que abrem com o Windows (liga e desliga cada um, com o tempo de boot
  medido), timers de boot e plano de energia próprio do SevenzinTwks.
- **Instaladores:** 69 programas de fontes oficiais em 5 grupos (jogos e comunidade, desempenho e diagnóstico,
  drivers e componentes, aplicativos essenciais, sistema e criação), com busca e filtros. Cada download tem a
  **assinatura digital conferida** antes de liberar a execução. Há também a instalação rápida pelo WinGet, com o
  andamento de cada pacote.
- **Limpeza:** analisa e remove temporários, caches e a Lixeira, sem tocar em arquivos pessoais.
- **Proteção:** mostra o estado real de UAC, Defender, SmartScreen, isolamento de núcleo, Windows Update, Secure Boot e
  TPM. Os ajustes desta tela são de risco alto, por isso exigem confirmação explícita e podem ser desfeitos.

### Medição e conta

- **Benchmark e Telemetria:** benchmark de frametime e sessões de medição de CPU, clock e memória, para comparar o
  **mesmo PC antes e depois** de uma mudança, em vez de confiar em promessa.
- **Conta e segurança:** login com e-mail e senha, verificação em duas etapas (app autenticador) e atualização
  automática pelo próprio app.

## Como adquirir e instalar

1. **Compre o acesso.** Entre no Discord do SevenzinTwks e fale com o **Geandre**:
   **https://discord.gg/W7wC4dWNYc**. Depois da compra, você recebe o seu login (e-mail e senha).
2. **Baixe o instalador.** Na página de [versões](https://github.com/geandreac/sevenzinTwk/releases/latest), baixe o
   arquivo `SevenzinTwks-<versão>-setup.exe`. Se preferir não instalar, use o
   `SevenzinTwks-<versão>-portable-win-x64.zip`.
3. **Instale.** Abra o instalador e confirme a permissão de administrador. Ela é necessária porque várias
   otimizações mexem em configurações do sistema.
4. **Entre com a sua conta.** Use o login recebido no Discord. Recomendamos ativar a verificação em duas etapas em
   **Configurações › Perfil**.
5. **Comece medindo.** Veja o painel do **Início**, depois use **Otimizar** ou escolha os ajustes um a um em
   **Ajustes**. O ponto de restauração é criado antes, automaticamente.

**Requisitos:** Windows 10 ou 11 de 64 bits e permissão de administrador. Não é preciso instalar .NET nem outro
componente: tudo vem no instalador.

**Atualizações:** o app avisa quando há versão nova. Você também pode buscar em **Configurações › Preferências ›
Buscar atualizações**.

**Suporte:** dúvidas, problemas e sugestões vão para o [Discord](https://discord.gg/W7wC4dWNYc).

> **Sobre os ganhos:** o desempenho de cada PC depende do hardware, dos drivers e dos jogos. Por isso o SevenzinTwks
> não promete números: ele mede o antes e o depois para você ver o resultado no seu próprio computador.

---

## Para desenvolvedores

Projeto em **WPF / .NET 8**. A lógica fica numa biblioteca sem interface, testável de forma isolada.

| Pasta | Conteúdo |
|---|---|
| `src/Sevenzin.Diagnostics/` | Motor sem UI: coleta de hardware, motor de ajustes reversíveis, instaladores verificados, autenticação |
| `src/Sevenzin.Wpf/` | Aplicativo WPF (telas, tema e componentes) |
| `src/Sevenzin.Report/` | Gerador de relatório do PC por linha de comando |
| `tests/Sevenzin.Tests/` | Testes automatizados (xUnit) |
| `docs/` | Design system, catálogo de otimizações, roteiros de teste e planos |

Requer o **.NET SDK 8** no Windows:

```powershell
dotnet build src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug
dotnet test
dotnet run --project src\Sevenzin.Wpf\Sevenzin.Wpf.csproj -c Debug
```

As contas ficam no servidor: para entrar no app, mesmo compilado localmente, é preciso um login válido.

> A tela **Ajustes** altera o registro do Windows de verdade (com backup e desfazer). Para desenvolver, prefira uma
> máquina virtual ou um PC de testes.
