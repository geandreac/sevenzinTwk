import re
t = open(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\src\Sevenzin.Diagnostics\Tweaks.cs", encoding="utf-8").read()
# cada Tweak: Id + Nome + Descricao + Categoria (4 strings), depois Colmeia
itens = re.findall(r'new Tweak\(\s*"([^"]+)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]+)"', t)
seg = [i for i, n, d, c in itens if c == "Segurança"]
print("Segurança:", len(seg))
for i in seg:
    print(" ", i)
print("Total tweaks:", len(itens))
