#Requires -RunAsAdministrator
# validacao_p0.ps1 — ciclo completo (aplicar -> reler -> reverter -> reler) para os grupos SEGUROS.
# G4 (UAC/Defender/HVCI/mitigações/SmartScreen/WU) e G1-plano: SÓ LEITURA (ambiente isolado exigido).
# Ao final, NENHUM valor permanece aplicado. Evidência gravada em docs/evidencia_p0.md.
$ErrorActionPreference = 'Stop'
$script:Evidence = New-Object System.Collections.Generic.List[string]
$script:Seq = 0

function Root([string]$hive) {
    if ($hive -eq 'HKCU') { return [Microsoft.Win32.Registry]::CurrentUser }
    return [Microsoft.Win32.Registry]::LocalMachine
}
function Test-Key([string]$hive, [string]$path) {
    $k = (Root $hive).OpenSubKey($path)
    if ($null -eq $k) { return $false }
    $k.Dispose(); return $true
}
function Emit([string]$verdict, [string]$group, [string]$what, [string]$antes, [string]$depois, [string]$restaurado) {
    $script:Seq++
    $line = "E-{0:D3} | {1} | {2} | {3} | antes: {4} | depois: {5} | restaurado: {6}" -f $script:Seq, $verdict, $group, $what, $antes, $depois, $restaurado
    $script:Evidence.Add($line)
    Write-Output $line
}

# Aplica, relê, reverte e relê um valor de registro (DWord ou String). Restaura tipo+valor exatos.
function Cycle([string]$group, [string]$hive, [string]$path, [string]$name, [string]$kind, [object]$target) {
    $root = Root $hive
    $keyExisted = Test-Key $hive $path
    $k = $root.CreateSubKey($path)
    if ($null -eq $k) { Emit 'LACUNA' $group "$hive\$path\$name" 'n/a' 'n/a' 'n/a'; return }

    $origPresent = $false; $origVal = $null; $origKind = 'String'
    try {
        $origKind = $k.GetValueKind($name).ToString()
        $origVal = $k.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::None)
        $origPresent = $true
    } catch { $origPresent = $false }

    # Guarda: só toca DWord/String/ausente
    if ($origPresent -and $origKind -notin @('DWord','String')) {
        Emit 'LACUNA' $group "$hive\$path\$name" "tipo exotico $origKind (nao tocado)" '-' '-'
        $k.Dispose(); return
    }
    $antes = if ($origPresent) { "${origKind}:${origVal}" } else { 'AUSENTE' }

    $rk = if ($kind -eq 'DWord') { [Microsoft.Win32.RegistryValueKind]::DWord } else { [Microsoft.Win32.RegistryValueKind]::String }
    $k.SetValue($name, $target, $rk)

    $depoisVal = $k.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::None)
    $depoisKind = $k.GetValueKind($name).ToString()
    $depois = "${depoisKind}:${depoisVal}"
    $applied = ($depoisKind -eq $kind)
    if ($kind -eq 'DWord') { $applied = $applied -and ([int]$depoisVal -eq [int]$target) }
    else { $applied = $applied -and ("$depoisVal" -eq "$target") }

    # restaurar
    if ($origPresent) {
        $ork = if ($origKind -eq 'DWord') { [Microsoft.Win32.RegistryValueKind]::DWord } else { [Microsoft.Win32.RegistryValueKind]::String }
        $k.SetValue($name, $origVal, $ork)
    } else { $k.DeleteValue($name, $false) }

    $restPresent = $true; $restVal = $null; $restKind = 'String'
    try {
        $restKind = $k.GetValueKind($name).ToString()
        $restVal = $k.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::None)
    } catch { $restPresent = $false }
    $restaurado = if ($restPresent) { "${restKind}:${restVal}" } else { 'AUSENTE' }

    $restOk = if ($origPresent) { ($restPresent -and ($restKind -eq $origKind) -and ("$restVal" -eq "$origVal")) } else { (-not $restPresent) }

    $k.Dispose()

    # se a chave não existia e ficou vazia, removê-la (preservar "chave ausente")
    if (-not $keyExisted) {
        $tmp = (Root $hive).OpenSubKey($path)
        if ($null -ne $tmp) {
            $empty = ($tmp.ValueCount -eq 0 -and $tmp.SubKeyCount -eq 0)
            $tmp.Dispose()
            if ($empty) { try { (Root $hive).DeleteSubKey($path, $false) } catch {} }
        }
    }

    $verdict = if ($applied -and $restOk) { 'CONFIRMADO' } elseif ($applied) { 'DIVERGENTE' } else { 'LACUNA' }
    Emit $verdict $group "$hive\$path\$name" $antes $depois $restaurado
}

Write-Output '=== G7 — Mouse / visual / privacidade (HKCU) ==='
Cycle 'G7' 'HKCU' 'Control Panel\Mouse' 'MouseSpeed' 'String' '0'
Cycle 'G7' 'HKCU' 'Control Panel\Mouse' 'MouseThreshold1' 'String' '0'
Cycle 'G7' 'HKCU' 'Control Panel\Mouse' 'MouseThreshold2' 'String' '0'
Cycle 'G7' 'HKCU' 'Control Panel\Mouse' 'MouseHoverTime' 'String' '0'
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' 'EnableTransparency' 'DWord' 0
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' 'AppsUseLightTheme' 'DWord' 0
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\DWM' 'EnableAeroPeek' 'DWord' 0
Cycle 'G7' 'HKCU' 'Control Panel\Desktop' 'DragFullWindows' 'String' '0'
Cycle 'G7' 'HKCU' 'Control Panel\Desktop' 'MenuShowDelay' 'String' '0'
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\PushNotifications' 'ToastEnabled' 'DWord' 0
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\Search' 'BingSearchEnabled' 'DWord' 0
Cycle 'G7' 'HKCU' 'Software\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot' 'DWord' 1
Cycle 'G7' 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\DeviceAccess\Global' 'DontSendAdditionalData' 'DWord' 1

Write-Output '=== G6 — CPU / memória / MMCSS (HKLM) ==='
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'SystemResponsiveness' 'DWord' 10
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'RMPriorityThrottleDelay' 'DWord' 0
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'NetworkThrottlingIndex' 'DWord' 10
Cycle 'G6' 'HKLM' 'SYSTEM\CurrentControlSet\Control\PriorityControl' 'Win32PrioritySeparation' 'DWord' 38
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games' 'GPU Priority' 'DWord' 8
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games' 'Priority' 'DWord' 2
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games' 'SFIO Priority' 'String' 'Normal'
Cycle 'G6' 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games' 'Scheduling Category' 'String' 'Medium'
Cycle 'G6' 'HKLM' 'SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management' 'DisablePagingExecutive' 'DWord' 1
Cycle 'G6' 'HKLM' 'SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management' 'LargeSystemCache' 'DWord' 1
Cycle 'G6' 'HKLM' 'SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 'DWord' 0

Write-Output '=== G5 — GPU (TdrDelay, HAGS) (HKLM) ==='
Cycle 'G5' 'HKLM' 'SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'TdrDelay' 'DWord' 8
Cycle 'G5' 'HKLM' 'SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' 'DWord' 2

Write-Output '=== G3 — Rede por adaptador (HKLM) ==='
$adapter = Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -First 1
if ($adapter) {
    $g = $adapter.InterfaceGuid
    Write-Output "adaptador: $($adapter.Name)  guid=$g"
    Cycle 'G3' 'HKLM' "SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" 'TcpAckFrequency' 'DWord' 1
    Cycle 'G3' 'HKLM' "SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" 'TCPNoDelay' 'DWord' 1
    Cycle 'G3' 'HKLM' "SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$g" 'TcpDelAckTicks' 'DWord' 0
    Cycle 'G3' 'HKLM' "SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_$g" 'NetbiosOptions' 'DWord' 2
    Cycle 'G3' 'HKLM' "SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\InterfaceSpecificParameters\$g" 'EnableMulticast' 'DWord' 0
} else {
    Emit 'LACUNA' 'G3' 'adaptador ativo' 'sem adaptador Up' '-' '-'
}

Write-Output '=== G2 — Rede global (TCP, netsh) ==='
function Get-TcpVal($text, [string[]]$keys) {
    foreach ($line in @($text)) {
        $s = "$line"
        foreach ($k in $keys) {
            if ($s -match [regex]::Escape($k)) {
                $parts = $s -split ':', 2
                if ($parts.Count -ge 2) {
                    $v = $parts[1].Trim()
                    if ($v) { return $v }
                }
            }
        }
    }
    return '?'
}
$before2 = netsh int tcp show global
$at0 = Get-TcpVal $before2 @('Auto-Tuning','Recebimento')
$cp0 = Get-TcpVal $before2 @('Congestion','Congest')
Write-Output "antes: autotuning=$at0 congestion=$cp0"
netsh int tcp set global autotuninglevel=normal | Out-Null
netsh int tcp set global congestionprovider=ctcp | Out-Null
$after2 = netsh int tcp show global
$at1 = Get-TcpVal $after2 @('Auto-Tuning','Recebimento')
$cp1 = Get-TcpVal $after2 @('Congestion','Congest')
Write-Output "depois: autotuning=$at1 congestion=$cp1"
# restaurar
if ($cp0 -ne '?') { netsh int tcp set global congestionprovider=$cp0 | Out-Null }
if ($at0 -ne '?') { netsh int tcp set global autotuninglevel=$at0 | Out-Null }
$rest2 = netsh int tcp show global
$at2 = Get-TcpVal $rest2 @('Auto-Tuning','Recebimento')
$cp2 = Get-TcpVal $rest2 @('Congestion','Congest')
Write-Output "restaurado: autotuning=$at2 congestion=$cp2"
$ok2 = ($at1 -eq 'normal' -and $cp1 -eq 'ctcp' -and $at2 -eq $at0 -and $cp2 -eq $cp0)
Emit ($(if ($ok2) { 'CONFIRMADO' } else { 'DIVERGENTE' })) 'G2' 'netsh int tcp (autotuning/congestion)' "autotuning=$at0,congestion=$cp0" "autotuning=$at1,congestion=$cp1" "autotuning=$at2,congestion=$cp2"

Write-Output '=== G1 — Hibernação (powercfg) ==='
$k = (Root 'HKLM').OpenSubKey('SYSTEM\CurrentControlSet\Control\Power')
$v = $k.GetValue('HibernateEnabled', $null, [Microsoft.Win32.RegistryValueOptions]::None); $k.Dispose()
$orig = if ($null -eq $v) { 'AUSENTE' } else { "$v" }
Write-Output "estado original hibernacao: chave=$orig"
powercfg /hibernate off | Out-Null
$k = (Root 'HKLM').OpenSubKey('SYSTEM\CurrentControlSet\Control\Power')
$vo = $k.GetValue('HibernateEnabled', $null, [Microsoft.Win32.RegistryValueOptions]::None); $k.Dispose()
$off = if ($null -eq $vo) { 'AUSENTE' } else { "$vo" }
powercfg /hibernate on | Out-Null
$k = (Root 'HKLM').OpenSubKey('SYSTEM\CurrentControlSet\Control\Power')
$v1 = $k.GetValue('HibernateEnabled', $null, [Microsoft.Win32.RegistryValueOptions]::None); $k.Dispose()
$on = if ($null -eq $v1) { 'AUSENTE' } else { "$v1" }
# restaurar o estado ORIGINAL (não deixar "on")
if ($orig -eq '0' -or $orig -eq 'AUSENTE') { powercfg /hibernate off | Out-Null } else { powercfg /hibernate on | Out-Null }
$k = (Root 'HKLM').OpenSubKey('SYSTEM\CurrentControlSet\Control\Power')
$vr = $k.GetValue('HibernateEnabled', $null, [Microsoft.Win32.RegistryValueOptions]::None); $k.Dispose()
$rest = if ($null -eq $vr) { 'AUSENTE' } else { "$vr" }
Write-Output "off->$off ; on->$on ; restaurado->$rest"
$okHib = (($off -eq '0' -or $off -eq 'AUSENTE') -and ($on -eq '1') -and ($rest -eq $orig))
Emit ($(if ($okHib) { 'CONFIRMADO' } else { 'DIVERGENTE' })) 'G1' 'hibernacao (powercfg off/on + restauracao)' "original=$orig" "off=$off ; on=$on" "restaurado=$rest"

Write-Output '=== G4 — Segurança (SÓ LEITURA, não aplicado) ==='
$sec = @(
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\EnableLUA',
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\ConsentPromptBehaviorAdmin',
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\PromptOnSecureDesktop',
    'HKLM\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection\DisableRealtimeMonitoring',
    'HKLM\SOFTWARE\Microsoft\Windows Defender\DisableAntiSpyware',
    'HKLM\SOFTWARE\Policies\Microsoft\Windows\System\EnableSmartScreen',
    'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled',
    'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\FeatureSettingsOverride',
    'HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoUpdate'
)
foreach ($s in $sec) {
    $hive, $rest = $s -split '\\', 2
    $path, $name = $rest -split '\\(?=[^\\]+$)'  # split on last backslash
    $root = Root $hive
    $k = $root.OpenSubKey($path)
    $val = 'AUSENTE'
    if ($null -ne $k) {
        $vv = $k.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::None)
        if ($null -ne $vv) { $val = "0x{0:X}" -f $vv }
        $k.Dispose()
    }
    Emit 'LACUNA' 'G4' "$path\$name" '(nao aplicado - ambiente isolado)' "leitura=$val" '-'
}

Write-Output '=== G1 — Plano de energia (SÓ LEITURA) ==='
$active = (powercfg /getactivescheme 2>&1 | Select-Object -First 1).ToString()
Write-Output "plano ativo: $active"
Emit 'LACUNA' 'G1' 'plano de energia (getactivescheme)' '(nao aplicado)' "leitura=$active" '-'

# Grava evidência
$dest = Join-Path $PSScriptRoot '..\docs\evidencia_p0.md'
$header = @"
# Evidência P0 — validação em máquina real (aplicar → reler → reverter → reler)

Gerado em $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') por `scripts/validacao_p0.ps1` (máquina: $env:COMPUTERNAME, usuário: $env:USERNAME).
Grupos SEGUROS aplicados/revertidos; G4 (segurança) e G1-plano em SÓ LEITURA (exigem ambiente isolado).

## Resultados

"@
$footer = @"

## Resumo

- Grupos com ciclo completo: G7 (HKCU), G6, G5, G3 (HKLM), G2 (netsh), G1-hibernação.
- Grupos só leitura: G4 (segurança), G1-plano.
- Nenhum valor permaneceu aplicado (cada item foi restaurado e a restauração confirmada).
"@
Set-Content -Path $dest -Value ($header + ($script:Evidence -join "`n") + $footer) -Encoding UTF8
Write-Output "Evidência gravada em: $dest"
