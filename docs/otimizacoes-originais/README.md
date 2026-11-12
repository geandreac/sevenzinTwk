# Dados brutos das otimizações do binário original

Extração **somente leitura** do código descompilado de `geandre.exe` (Honest/"Fps.Assistant").
Nada aqui é executado — é referência para o catálogo em [`../catalogo-otimizacoes.md`](../catalogo-otimizacoes.md).

- `01_registro_setvalues.txt` — toda escrita de registro (`SetValue(nome, valor, tipo)`), deduplicada.
- `02_registro_chaves.txt` — todos os caminhos de chave abertos/criados.
- `03_comandos_sistema.txt` — comandos de SO (powercfg, netsh, Set-MpPreference, Disable-NetAdapter*, Remove-AppxPackage, bcdedit…).

Os nomes das chaves usam `\` duplicado por virem de literais C#. O `honest (2)/geandre.exe` é
byte‑idêntico ao analisado (SHA256 `70dc5163…`), então cobre as duas "versões".
