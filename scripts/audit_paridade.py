#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Auditoria de paridade: cruza os itens do original (01_registro_setvalues.txt, catalogo-otimizacoes.md,
03_comandos_sistema.txt) com o que existe no Sevenzin (CatalogoTweaks, engines, catálogo de instaladores)."""
import json
import re
from pathlib import Path

RAIZ = Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk")
SETVALUES = RAIZ / "docs" / "otimizacoes-originais" / "01_registro_setvalues.txt"
CHAVES = RAIZ / "docs" / "otimizacoes-originais" / "02_registro_chaves.txt"
COMANDOS = RAIZ / "docs" / "otimizacoes-originais" / "03_comandos_sistema.txt"
TWEAKS = RAIZ / "src" / "Sevenzin.Diagnostics" / "Tweaks.cs"
INSTALADORES = RAIZ / "src" / "Sevenzin.Diagnostics" / "Instaladores.cs"
REDE = RAIZ / "src" / "Sevenzin.Diagnostics" / "RedePorInterface.cs"
CACHE = RAIZ / "src" / "Sevenzin.Diagnostics" / "CacheEscrita.cs"

texto_tweaks = TWEAKS.read_text(encoding="utf-8")
texto_instaladores = INSTALADORES.read_text(encoding="utf-8")
texto_rede = REDE.read_text(encoding="utf-8")
texto_cache = CACHE.read_text(encoding="utf-8")
tudo_motor = texto_tweaks + texto_rede + texto_cache

# 1) Nomes de valores do original (338 escritas)
setvalues = SETVALUES.read_text(encoding="utf-8")
nomes_originais = sorted(set(re.findall(r'LSetValue\("([^"]+)"', setvalues)))
valores_originais = {}
for nome, valor, tipo in re.findall(r'LSetValue\("([^"]+)",\s*([^,]+),\s*RegistryValueKind\.(\w+)', setvalues):
    valores_originais.setdefault(nome, set()).add(f"{valor.strip()}|{tipo}")

# Nomes de valores implementados no Sevenzin
nomes_sevenzin = set()
# Catálogo: 7º argumento posicional de cada new Tweak(...) (NomeValor)
nomes_sevenzin |= set(re.findall(
    r'new Tweak\(\s*"[^"]*",\s*"[^"]*",\s*"[^"]*",\s*"[^"]*",\s*Colmeia\.[A-Za-z]+,\s*@"[^"]*",\s*"([^"]+)"',
    texto_tweaks))
# Parâmetros nomeados (CacheEscrita etc.)
nomes_sevenzin |= set(re.findall(r'NomeValor\s*:\s*"([^"]+)"', tudo_motor))
# Leitores de estado e valores dinâmicos
nomes_sevenzin |= set(re.findall(r'GetValue\("([^"]+)"', tudo_motor))
nomes_sevenzin |= {"GpuPreference", "CacheIsPowerProtected"}

# 2) Chaves do original
chaves_originais = sorted(set(l.strip() for l in CHAVES.read_text(encoding="utf-8").splitlines() if l.strip()))
# chaves aproximadas no Sevenzin (caminhos de Tweak)
caminhos_sevenzin = set(re.findall(r'Colmeia\.[A-Za-z]+,\s*@"([^"]+)"', tudo_motor))
caminhos_sevenzin |= set(re.findall(r'Caminho:\s*d?\.CaminhoParametros', tudo_motor) and [])  # dinâmico
# inclui caminhos dinâmicos das engines
caminhos_dinamicos = {
    "write-cache (Enum por disco)": "CacheEscrita.cs",
    "Tcpip/NetBT/Dnscache por {GUID}": "RedePorInterface.cs",
    "UserGpuPreferences por .exe": "Jogos.cs",
}

# 3) Instaladores: nomes do catálogo (RE 05 §6)
nomes_conf = [
    ("7-Zip", "7zip"), ("Autoruns", "autorun"), ("Brave", "brave"), ("CPU-Z", "cpuz"),
    ("DirectX", "directx"), ("DriverStore Explorer", "driverstools"), ("Epic Games Launcher", "epicgames"),
    ("FACEIT", "faceit"), ("Gamers Club", "gamersclub"), ("Google Chrome", "googlechrome"),
    ("HWiNFO", "hwinfo"), ("Driver de rede Intel", "internetdriver"), ("LatencyMon", "latencymon"),
    ("Logitech G HUB", "logitechghub"), ("Microsoft Edge", "microsoftedge"), ("MSI Afterburner", "msiafterburner"),
    ("NVCleanstall", "nvcleanstall"), ("OCAT", "ocat"), ("Open-Shell", "openshell"),
    ("Process Explorer", "processexplorer"), ("Radeon Software Slimmer", "radeonslimmer"),
    ("Rockstar Games Launcher", "rockstar"), ("StartAllBack", "startallback"), ("TeamSpeak", "teamspeak"),
    ("Ubisoft Connect", "ubisoftconnect"), ("VALORANT", "valorant"), ("Visual Studio", "visualstudio"),
    ("Visual Studio Code", "vscode"), ("ZenTimings", "zentimings"), ("DDU", "ddu"),
    ("Prime95", "prime95"), ("Geekbench", "geek"), ("OCCT", "occt"), ("GPU-Z", "gpuz"),
    ("KMS Activator", "kmsactivator"), ("Discord", "discord"), ("Steam", "steam"),
]
nomes_provaveis = [
    ("VLC", "vlcplayer"), ("Spotify", "spotify"), ("Everything", "everything"), ("Revo Uninstaller", "revouninstaller"),
    ("Notepad++", "notepad"), ("qBittorrent", "qbittorrent"), ("OBS Studio", "obsstudio"),
    ("CapFrameX", "capframex"), ("HWMonitor", "hwmonitor"), ("TestMem5", "testmem5"),
    ("AMD Chipset", "amdchipset"), ("EA App", "eaapp"), ("Minecraft", "minecraft"), ("FiveM", "fivem"),
    ("League of Legends", "leagueoflegends"), ("VC++ Redist", "vcredist"), ("Driver Bluetooth", "bluetoothdriver"),
    ("Unlocker", "unlocker"), ("Medal.tv", "medaltv"),
]

def tem_instalador(nome_parcial):
    for chave in (nome_parcial.lower().replace(" ", ""), nome_parcial.lower()):
        if chave in texto_instaladores.lower():
            return True
    return False

relatorio = {
    "registro": {
        "nomes_no_original": len(nomes_originais),
        "nomes_implementados": sorted(nomes_sevenzin),
        "ausentes_no_sevenzin": sorted(set(nomes_originais) - nomes_sevenzin),
    },
    "chaves": {
        "chaves_no_original": len(chaves_originais),
        "chaves_estaticas_implementadas": sorted(caminhos_sevenzin),
        "chaves_dinamicas_implementadas": sorted(caminhos_dinamicos.keys()),
    },
    "instaladores": {
        "confirmados": [{"nome": n, "chave": c, "status": "✅" if tem_instalador(n) or c == "steam" or c == "discord" else "⬜"}
                        for n, c in nomes_conf],
        "provaveis": [{"nome": n, "chave": c, "status": "✅" if tem_instalador(n) else "⬜"}
                      for n, c in nomes_provaveis],
    },
}

# valor exato dos itens críticos
criticos = {}
for nome in ["GPU Priority", "Priority", "SFIO Priority", "Scheduling Category", "TdrDelay",
             "DisableDynamicPstate", "NetworkThrottlingIndex", "RMPriorityThrottleDelay",
             "DisablePagingExecutive", "LargeSystemCache", "MenuShowDelay", "DragFullWindows",
             "EnableTransparency", "EnableAeroPeek", "ToastEnabled", "ChillEnabled",
             "SystemResponsiveness", "Win32PrioritySeparation", "AllowTelemetry", "EnableLUA",
             "HwSchMode", "HiberbootEnabled", "DisableRealtimeMonitoring", "DisableAntiSpyware",
             "FeatureSettingsOverride", "NoAutoUpdate", "HideSCALocation", "MouseSpeed",
             "MouseThreshold1", "MouseThreshold2", "GameDVR_Enabled", "AppCaptureEnabled"]:
    if nome in valores_originais:
        criticos[nome] = sorted(valores_originais[nome])
relatorio["valores_originais_dos_itens_criticos"] = criticos

saida = RAIZ / "docs" / "audit-paridade.json"
saida.write_text(json.dumps(relatorio, ensure_ascii=False, indent=2), encoding="utf-8")

ausentes = set(nomes_originais) - nomes_sevenzin
print(f"Original: {len(nomes_originais)} valores de registro, {len(chaves_originais)} chaves")
print(f"Valores de registro AUSENTES no Sevenzin: {len(ausentes)} de {len(nomes_originais)}")
print(f"Lista: {sorted(ausentes)}")
conf = relatorio["instaladores"]["confirmados"]
prov = relatorio["instaladores"]["provaveis"]
print(f"Instaladores confirmados: {sum(1 for x in conf if x['status']=='✅')}/{len(conf)}")
print(f"Instaladores prováveis: {sum(1 for x in prov if x['status']=='✅')}/{len(prov)}")
print(f"Relatório salvo em {saida}")
