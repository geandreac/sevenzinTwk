# Compila o instalador do Sevenzin (User + Admin) com Inno Setup. Publica os portateis antes, se faltarem.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
# (ASCII apenas: o Windows PowerShell 5.1 le .ps1 sem BOM como ANSI e quebra com acentos/traco longo.)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$portateis = @(
    (Join-Path $raiz 'dist\Sevenzin\Sevenzin.exe'),
    (Join-Path $raiz 'dist\SevenzinAdmin\SevenzinAdmin.exe')
)
$iss = Join-Path $PSScriptRoot 'Sevenzin.iss'

if ($portateis | Where-Object { -not (Test-Path $_) }) {
    Write-Host "Portatil(eis) ausente(s) - publicando primeiro..."
    & (Join-Path $PSScriptRoot 'publish-portable.ps1')
}

$iscc = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }

if (-not $iscc) {
    Write-Warning "Inno Setup (ISCC.exe) nao encontrado. Instale em https://jrsoftware.org/isdl.php e rode de novo."
    Write-Host "Alternativa: abra $iss no Inno Setup e clique em Build."
    exit 1
}

Write-Host "ISCC: $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }
$saida = Join-Path $raiz 'dist\installer'
Write-Host "Instalador em: $saida"
Get-ChildItem $saida -Filter *setup.exe -ErrorAction SilentlyContinue |
    Select-Object FullName, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime | Format-Table -Auto
