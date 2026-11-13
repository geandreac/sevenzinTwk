# Compila o instalador do Sevenzin (User) com Inno Setup. Gera antes o portátil se faltar.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$portatil = Join-Path $raiz 'dist\Sevenzin\Sevenzin.exe'
$iss = Join-Path $PSScriptRoot 'Sevenzin.iss'

if (-not (Test-Path $portatil)) {
    Write-Host "Portátil ausente — publicando primeiro…"
    & (Join-Path $PSScriptRoot 'publish-portable.ps1')
}

$iscc = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }

if (-not $iscc) {
    Write-Warning "Inno Setup (ISCC.exe) não encontrado. Instale em https://jrsoftware.org/isdl.php e rode de novo."
    Write-Host "Alternativa: abra $iss no Inno Setup e clique em Build."
    exit 1
}

Write-Host "ISCC: $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }
Write-Host "`nInstalador em: $(Join-Path $raiz 'dist\installer')"
Get-ChildItem (Join-Path $raiz 'dist\installer') -Filter *setup.exe -ErrorAction SilentlyContinue |
    Select-Object FullName, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime | Format-Table -Auto
