# Engenharia reversa do `geandre.exe`

Inventário técnico do programa original (de autoria do próprio autor do projeto), produzido por
**engenharia reversa estática** para guiar a reconstrução segura no Sevenzin. O binário **nunca foi
executado**; toda afirmação é marcada como `[CONFIRMADO]` (lido do arquivo/recurso), `[INFERIDO]`
(deduzido com evidência citada) ou `[LACUNA]` (indeterminado pelo método estático).

- **Alvo:** `geandre.exe` — `Honest Tech 2.5.1.0` / `ProductName=Fps` (.NET Framework 4.8, WinForms + Guna.UI2).
- **SHA-256:** `70DC5163CF1343D22484D0B2702AFDA125D37A38904D74EE9F90FAA489AFCB72` · 68.518.400 bytes.
- **Método:** leitura de PE/CLR, recursos gerenciados, strings (`#Strings`), scripts PowerShell embutidos e
  manifesto. IL não descompilável (metadados ofuscados por Eazfuscator + `#US` cifrado); a lógica interna
  foi reconstruída por nomes de membros e scripts, não por corpo de método.

## Índice (método de 7 fases)

| arquivo | conteúdo |
|---|---|
| [`00_resumo.md`](00_resumo.md) | Consolidação: contagens, lacunas priorizadas, plano de recriação (Fases A/B/C), veredito de paridade |
| [`01_triagem.md`](01_triagem.md) | PE, CLR, metadados, recursos, proteções |
| [`02_estrutura.md`](02_estrutura.md) | Camadas, recursos, P/Invoke, backend, domínio |
| [`03_telas.md`](03_telas.md) | 22 telas + mapa de navegação |
| [`04_funcionalidades.md`](04_funcionalidades.md) | 16 seções de funcionalidades |
| [`05_metricas_instaladores_jogos.md`](05_metricas_instaladores_jogos.md) | Métricas, instaladores, jogos, mídia |
| [`06_fluxos.md`](06_fluxos.md) | 14 fluxos (F1–F14) |
| [`metricas.json`](metricas.json) | Catálogo de 38 métricas (13 confirmadas / 13 inferidas / 12 lacunas) |

Contagem total das fases 01–06: **729 CONFIRMADO · 108 INFERIDO · 107 LACUNA**.

## Proveniência e limites

- Os documentos citam artefatos de apoio da **área de trabalho local** da análise
  (`output/strings_*.txt`, `resource_inventory.json`, `resources_clean/`, `resources_imgs/`, etc.).
  Esses artefatos brutos — vários binários/imagens e arquivos de centenas de MB — **não são versionados**
  aqui; ficam no workspace de análise. Os `.md` acima são autossuficientes para a reconstrução.
- O que **não** pôde ser extraído (vai para `[LACUNA]`): textos exatos de UI, catálogo de downloads
  (URL/hash/args) e qualquer medição de desempenho em tempo real — ver `00_resumo.md` §10 e §13.
- Relação com o Sevenzin: este inventário embasa [`../paridade_funcional.md`](../paridade_funcional.md).
  O charter de segurança do Sevenzin exclui deliberadamente o conjunto perigoso descrito aqui
  (desligar UAC, remoção forçada de Edge/OneDrive, flash de BIOS, exfiltração, etc.) — ver `00_resumo.md` §11, Fase C.
