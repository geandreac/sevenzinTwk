import re
from pathlib import Path

t = Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\docs\otimizacoes-originais\03_comandos_sistema.txt").read_text(encoding="utf-8", errors="replace")
linhas_ac = []
linhas_dc = []
for l in t.splitlines():
    l = l.strip().strip('"')
    m_ac = re.match(r'powercfg /setacvalueindex\s+[0-9a-fA-F-]+\s+([0-9a-fA-F-]+)\s+([0-9a-fA-F-]+)\s+(\S+)', l)
    if m_ac:
        linhas_ac.append((m_ac.group(1), m_ac.group(2), m_ac.group(3)))
    m_dc = re.match(r'powercfg /setdcvalueindex\s+[0-9a-fA-F-]+\s+([0-9a-fA-F-]+)\s+([0-9a-fA-F-]+)\s+(\S+)', l)
    if m_dc:
        linhas_dc.append((m_dc.group(1), m_dc.group(2), m_dc.group(3)))

print(f"total indices AC: {len(linhas_ac)} | DC: {len(linhas_dc)}")
corpo = [
    "// Gerado por scripts/extrair_indices.py a partir de docs/otimizacoes-originais/03_comandos_sistema.txt",
    "// (comandos literais AC + DC do geandre.exe). NÃO editar à mão.",
    "namespace Sevenzin.Diagnostics;",
    "",
    "public static class IndicesEnergiaOriginal",
    "{",
    "    public static readonly IReadOnlyList<(string Subgrupo, string Configuracao, string Valor)> Todos = new (string, string, string)[]",
    "    {",
]
for s, c, v in linhas_ac:
    corpo.append(f'        ("{s}", "{c}", "{v}"),')
corpo.append("    };")
corpo.append("")
corpo.append("    public static readonly IReadOnlyList<(string Subgrupo, string Configuracao, string Valor)> TodosDc = new (string, string, string)[]")
corpo.append("    {")
for s, c, v in linhas_dc:
    corpo.append(f'        ("{s}", "{c}", "{v}"),')
corpo.append("    };")
corpo.append("}")
Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\src\Sevenzin.Diagnostics\IndicesEnergiaOriginal.cs").write_text("\n".join(corpo) + "\n", encoding="utf-8")
print("gravado em src/Sevenzin.Diagnostics/IndicesEnergiaOriginal.cs")
