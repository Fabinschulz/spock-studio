#requires -Version 5.1
<#
.SYNOPSIS
  Publica o runtime .NET como executável autocontido e o copia para o sidecar do Tauri.
.DESCRIPTION
  O Tauri espera binaries/<nome>-<target-triple>[.exe]. O usuário final não precisa do SDK .NET.
.PARAMETER Rid
  Identificador de runtime do .NET (padrão: win-x64).
#>
param(
    [string]$Rid = 'win-x64',
    [switch]$FetchPythonEmbed
)

$ErrorActionPreference = 'Stop'

$triples = @{
    'win-x64'     = @{ Triple = 'x86_64-pc-windows-msvc';  Ext = '.exe' }
    'osx-arm64'   = @{ Triple = 'aarch64-apple-darwin';    Ext = '' }
    'osx-x64'     = @{ Triple = 'x86_64-apple-darwin';     Ext = '' }
    'linux-x64'   = @{ Triple = 'x86_64-unknown-linux-gnu'; Ext = '' }
}
if (-not $triples.ContainsKey($Rid)) { throw "RID não suportado: $Rid" }
$t = $triples[$Rid]

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'runtime\Mariliz.Runtime\Mariliz.Runtime.csproj'
$out = Join-Path $root "dist\runtime\$Rid"
$dest = Join-Path $root 'desktop\host\src-tauri\binaries'

dotnet publish $project -c Release -r $Rid --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }

New-Item -ItemType Directory -Force $dest | Out-Null
$exe = Join-Path $out ("Mariliz.Runtime" + $t.Ext)
$target = Join-Path $dest ("mariliz-runtime-" + $t.Triple + $t.Ext)
Copy-Item $exe $target -Force
"Sidecar: $target ({0:N1} MB)" -f ((Get-Item $target).Length / 1MB)

# Medido em 2026-10-31: sidecar 70,8 MB; zip oficial python-3.12.10-embed-amd64 10,6 MB (orçamento do interpretador: 25 MB).
# A inteligência e as dependências (bytecode, orçamento 30 MB) saem de scripts/package-intelligence.py (ADR-0001).
$budget = 25MB
$resources = Join-Path $root 'desktop\host\src-tauri\resources'
$builder = if ($env:MARILIZ_BUILD_PYTHON) { $env:MARILIZ_BUILD_PYTHON } else { 'python' }
& $builder (Join-Path $root 'scripts\package-intelligence.py') bundle --resources $resources
if ($LASTEXITCODE -ne 0) { throw 'Empacotamento da inteligência falhou' }

function Enable-EmbedSiteImports {
    param([string]$Dir)
    $pth = Get-ChildItem $Dir -Filter 'python*._pth' -File | Select-Object -First 1
    if (-not $pth) { return }
    $lines = Get-Content $pth.FullName
    $lines = $lines | ForEach-Object { if ($_ -match '^\#import site') { 'import site' } else { $_ } }
    if ($lines -notcontains 'import site') { $lines += 'import site' }
    Set-Content -Path $pth.FullName -Value $lines -Encoding ascii
}

$pythonHome = $env:MARILIZ_PYTHON_HOME
$fetchEmbed = $FetchPythonEmbed -or ($env:MARILIZ_FETCH_PYTHON_EMBED -eq '1')
if (-not $pythonHome -and $fetchEmbed) {
    if ($Rid -ne 'win-x64') {
        throw 'O download automático do CPython embed só está disponível para win-x64. Use MARILIZ_PYTHON_HOME.'
    }
    $embedUrl = 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip'
    $cache = Join-Path $root '.cache\python-embed-amd64'
    $zip = Join-Path $cache 'python-3.12.10-embed-amd64.zip'
    New-Item -ItemType Directory -Force $cache | Out-Null
    if (-not (Test-Path $zip)) {
        "Baixando CPython embed: $embedUrl"
        Invoke-WebRequest -Uri $embedUrl -OutFile $zip -UseBasicParsing
    }
    $extract = Join-Path $cache 'extracted'
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Enable-EmbedSiteImports $extract
    $pythonHome = $extract
}

$pythonDest = Join-Path $resources 'python'
if ($pythonHome -and (Test-Path (Join-Path $pythonHome 'python.exe'))) {
    $pythonBytes = (Get-ChildItem $pythonHome -Recurse -File | Measure-Object Length -Sum).Sum
    if ($pythonBytes -gt $budget) {
        throw ("O interpretador em MARILIZ_PYTHON_HOME tem {0:N1} MB e passa do orçamento de 25 MB." -f ($pythonBytes / 1MB))
    }
    if (Test-Path $pythonDest) { Remove-Item $pythonDest -Recurse -Force }
    Copy-Item $pythonHome $pythonDest -Recurse -Force
    New-Item -ItemType File -Force (Join-Path $pythonDest '.gitkeep') | Out-Null
    "Interpretador embarcado: $pythonDest ({0:N1} MB)" -f ($pythonBytes / 1MB)
} else {
    "Interpretador embarcado: ausente. Defina MARILIZ_PYTHON_HOME ou execute com -FetchPythonEmbed (win-x64)."
}
