import re, json
from pathlib import Path

t = Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\docs\otimizacoes-originais\01_registro_setvalues.txt").read_text(encoding="utf-8", errors="replace")
print("linhas de exemplo:", [l.strip() for l in t.splitlines() if "CpuPriorityClass" in l][:3])

alvos = ["ConsentPromptBehaviorAdmin","ConsentPromptBehaviorUser","DefaultConsent","FilterAdministratorToken",
"PromptOnSecureDesktop","ValidateAdminCodeSignatures","EnableUIADesktopToggle","EnableSecureUIAPaths",
"CpuPriorityClass","IoPriority","DevicePriority","ThreadPriority","KernelThreadPriorityFloor",
"DisableGMMPowerGating","DisableSAMUPowerGating","DisableVCNPowerGating","DisableDrmdmaPowerGating","EnableUlps",
"AsicOnLowPower","PP_GPUPowerDownEnabled","PP_SclkDeepSleepDisable","PP_DisablePowerContainment",
"HibernateEnabled","DisableStartupSound","FontSmoothing","MinAnimate","VisualFXSetting","UserPreferencesMask",
"AppsUseLightTheme","SystemUsesLightTheme","ShowTaskViewButton","ShowCortanaButton","ShowCopilotButton",
"BingSearchEnabled","AllowTelemetry","DontSendAdditionalData","EnableLMHOSTS","MouseHoverTime","MouseSensitivity",
"UserWriteCacheSetting","EnableVirtualizationBasedSecurity","MitigationOptions","FeatureSettingsOverride",
"DpcWatchdogPeriod","DpcTimeout","MSISupported","MSIIRQ","GlobalTimerResolutionRequests","TaskbarAnimations",
"EnableVirtualization","ConsentPromptBehavior","EnableLUA","DisableRealtimeMonitoring","DisableAntiSpyware",
"EnableSmartScreen","HwSchMode","HiberbootEnabled","SystemResponsiveness","Win32PrioritySeparation",
"NetworkThrottlingIndex","RMPriorityThrottleDelay","DisablePagingExecutive","LargeSystemCache","TdrDelay",
"DisableDynamicPstate","ChillEnabled","DvrEnabled","GPU Priority","Priority","SFIO Priority","Scheduling Category",
"MenuShowDelay","DragFullWindows","EnableTransparency","EnableAeroPeek","ToastEnabled","UseOLEDTaskbarTransparency",
"GameDVR_Enabled","AppCaptureEnabled","MouseSpeed","MouseThreshold1","MouseThreshold2","HideSCALocation",
"Start","NoAutoUpdate","FeatureSettings","FeatureSettingsOverrideMask","MitigationAuditOptions","Affinity"]

resultado = {}
for a in alvos:
    vals = sorted(set((v[0].strip(), v[1]) for v in re.findall(r'LSetValue\("%s",\s*([^,]+),\s*RegistryValueKind\.(\w+)' % re.escape(a), t)))
    resultado[a] = [f"{x}|{k}" for x, k in vals] if vals else "N/A"

Path(r"Z:\Computador\Documentos\Projetos\sevenzinTwk\docs\audit-valores.json").write_text(json.dumps(resultado, ensure_ascii=False, indent=2), encoding="utf-8")
for k, v in resultado.items():
    print(k, "=>", v)
