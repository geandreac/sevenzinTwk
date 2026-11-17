import re
from pathlib import Path

t = Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\docs\otimizacoes-originais\03_comandos_sistema.txt").read_text(encoding="utf-8", errors="replace")
linhas = []
for l in t.splitlines():
    l = l.strip().strip('"')
    m = re.match(r'powercfg /setacvalueindex\s+[0-9a-fA-F-]+\s+([0-9a-fA-F-]+)\s+([0-9a-fA-F-]+)\s+(\S+)', l)
    if m:
        linhas.append((m.group(1), m.group(2), m.group(3)))

print("total indices:", len(linhas))
corpo = []
corpo.append("// Gerado por scripts/extrair_indices.py a partir de docs/otimizacoes-originais/03_comandos_sistema.txt")
corpo.append("// (comandos literais do geandre.exe). NÃO editar à mão.")
corpo.append("namespace Sevenzin.Diagnostics;")
corpo.append("")
corpo.append("public static class IndicesEnergiaOriginal")
corpo.append("{")
corpo.append("    public static readonly IReadOnlyList<(string Subgrupo, string Configuracao, string Valor)> Todos = new (string, string, string)[]")
corpo.append("    {")
for s, c, v in linhas:
    corpo.append(f'        ("{s}", "{c}", "{v}"),')
corpo.append("    };")
corpo.append("}")
Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\src\Sevenzin.Diagnostics\IndicesEnergiaOriginal.cs").write_text("\n".join(corpo) + "\n", encoding="utf-8")
print("gravado em src/Sevenzin.Diagnostics/IndicesEnergiaOriginal.cs")
