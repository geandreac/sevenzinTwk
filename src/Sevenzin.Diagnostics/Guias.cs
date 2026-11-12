namespace Sevenzin.Diagnostics;

/// <summary>Um guia do Sevenzin: título, resumo e passos numerados. Conteúdo baseado SOMENTE em
/// funcionalidades implementadas e confirmadas (nunca instrui sobre recursos que não existem).</summary>
public sealed record Guia(string Titulo, string Resumo, string[] Passos);

/// <summary>Catálogo de guias (tela 11 do original, 03_telas §11 — os textos exatos do original são
/// lacuna no #US cifrado; estes guias são reconstruídos a partir do que o Sevenzin realmente faz).</summary>
public static class CatalogoGuias
{
    public static readonly IReadOnlyList<Guia> Todos = new[]
    {
        new Guia("Medir antes de otimizar",
            "O princípio do Sevenzin: só mude o que você consegue medir. Use Telemetria e Benchmark para provar o ganho — nunca confie em promessa.",
            new[]
            {
                "Abra Benchmark e rode a cena para ter o FPS/1% low de referência.",
                "Em Telemetria, inicie uma sessão e defina a LINHA DE BASE.",
                "Aplique UMA otimização (em Ajustes ou no perfil de jogo).",
                "Meça de novo e compare: se a diferença for menor que o ruído, o resultado é inconclusivo — desfaça se quiser.",
                "Prefira uma mudança por vez; em massa é impossível saber o que funcionou.",
            }),
        new Guia("Perfil de jogo reversível",
            "Prioridade, afinidade, working set e timer de 1 ms por jogo — temporários e reversíveis.",
            new[]
            {
                "Em Jogos, clique em Adicionar jogo e escolha o .exe.",
                "Ajuste prioridade (nunca Realtime) e afinidade (só P-cores aparece se sua CPU for híbrida).",
                "Working set e timer de 1 ms são OPT-IN: leia o aviso de energia/micro-travadas antes de ligar.",
                "Abra o jogo e clique em Aplicar — ou ligue Aplicar ao abrir para o modo automático.",
                "Tudo é revertido ao fechar o jogo ou pelo botão Reverter.",
            }),
        new Guia("Tweaks com desfazer e auditoria",
            "Cada ajuste mostra o valor atual e o novo, guarda o anterior e registra a auditoria.",
            new[]
            {
                "Em Ajustes, leia a descrição e o risco de cada item antes de ligar.",
                "Itens de máquina (HKLM) pedem administrador; itens de SEGURANÇA pedem confirmação explícita.",
                "Ligar aplica; desligar DESFAZ com o valor anterior exato.",
                "A auditoria fica em %LOCALAPPDATA%\\Sevenzin (veja na tela Admin ou em Configurações).",
            }),
        new Guia("Tela Proteção com responsabilidade",
            "A Proteção mostra o estado real de UAC, Defender, SmartScreen, HVCI e mitigações.",
            new[]
            {
                "A tela só lê o estado — nada muda sem você confirmar.",
                "Desligar mecanismos de segurança deixa o PC vulnerável; o valor anterior sempre fica salvo para desfazer.",
                "Itens com * só valem após reiniciar.",
                "Defender com Tamper Protection ativa ignora os valores por registro — a UI avisa.",
            }),
        new Guia("Capturas e anotações",
            "Quatro modos de captura + editor de anotações, salvando local por padrão.",
            new[]
            {
                "Escolha o modo: tela inteira, monitor, região (arraste) ou janela.",
                "Desenhe com lápis, retângulo, seta ou texto; Desfazer remove a última anotação.",
                "Salvar PNG grava na pasta local; Copiar envia para a área de transferência.",
                "Enviar só existe com webhook configurado em Configurações e pede confirmação a cada envio.",
            }),
        new Guia("Instaladores verificados",
            "Utilitários de fontes oficiais, com verificação antes de executar.",
            new[]
            {
                "Cada item abre a página OFICIAL — o download direto só existe onde a URL é estável.",
                "Baixar e verificar checa a assinatura (e o hash quando fixado) antes de liberar o executar.",
                "A instalação rápida usa o winget com lista fechada de 5 IDs e confirmação explícita.",
                "O andamento fica na fila local, que sobrevive a reinícios.",
            }),
    };
}
