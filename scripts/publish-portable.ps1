# Publica os dois apps como PORTÁTEIS: um .exe self-contained single-file por app (roda em qualquer
# Windows x64 sem instalar .NET). Saída em dist\. Uso: powershell -File scripts\publish-portable.ps1
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = 'C:\re\geandre\dotnet'
$env:PATH = "C:\re\geandre\dotnet;$env:PATH"
$raiz = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $raiz 'dist'

# O otimizador distribuido leva o servidor de contas embutido (Sevenzin.Wpf.csproj le estas variaveis no build).
# Sem elas, em outro PC o app cairia no modo de contas locais ("Crie o primeiro administrador"): entao nao gera.
foreach ($v in 'SEVENZIN_SUPABASE_URL','SEVENZIN_SUPABASE_ANON') {
    if (-not [Environment]::GetEnvironmentVariable($v)) {
        $x = [Environment]::GetEnvironmentVariable($v, 'User')
        if (-not $x) { $x = [Environment]::GetEnvironmentVariable($v, 'Machine') }
        if (-not $x) { throw "$v nao definida: o portatil sairia sem servidor de contas. Defina-a e rode de novo." }
        Set-Item "env:$v" $x
    }
}

# WPF não suporta trimming; single-file + self-contained basta. Compressão reduz o tamanho do exe.
$comum = @(
    '-c','Release','-r','win-x64','--self-contained','true',
    '-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true','-p:DebugType=none'
)

foreach ($app in @(
    @{ Proj = 'src\Sevenzin.Wpf\Sevenzin.Wpf.csproj';     Saida = 'Sevenzin' },
    @{ Proj = 'src\Sevenzin.Admin\Sevenzin.Admin.csproj'; Saida = 'SevenzinAdmin' }
)) {
    # O app Admin e exclusivo do administrador e so existe na maquina dele (gitignored): fora dela, pula.
    if (-not (Test-Path (Join-Path $raiz $app.Proj))) { Write-Host "== $($app.Saida): projeto ausente (so local) - pulado =="; continue }
    $out = Join-Path $dist $app.Saida
    Write-Host "== Publicando $($app.Saida) =="
    dotnet publish (Join-Path $raiz $app.Proj) @comum -o $out
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $($app.Saida)" }
}

Write-Host "`nPortáteis em: $dist"
Get-ChildItem $dist -Recurse -Filter *.exe | Select-Object FullName, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime | Format-Table -Auto
