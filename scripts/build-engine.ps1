#requires -Version 5.1
<#
.SYNOPSIS
  Publica o motor .NET como executável autocontido e o copia para o sidecar do Tauri.
.DESCRIPTION
  O Tauri espera binaries/<nome>-<target-triple>[.exe]. O usuário final não precisa do SDK .NET.
.PARAMETER Rid
  Identificador de runtime do .NET (padrão: win-x64).
#>
param([string]$Rid = 'win-x64')

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
$project = Join-Path $root 'src\Mariliz.Engine\Mariliz.Engine.csproj'
$out = Join-Path $root "artifacts\engine\$Rid"
$dest = Join-Path $root 'apps\desktop-host\src-tauri\binaries'

dotnet publish $project -c Release -r $Rid --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }

New-Item -ItemType Directory -Force $dest | Out-Null
$exe = Join-Path $out ("Mariliz.Engine" + $t.Ext)
$target = Join-Path $dest ("mariliz-engine-" + $t.Triple + $t.Ext)
Copy-Item $exe $target -Force
"Sidecar: $target ({0:N1} MB)" -f ((Get-Item $target).Length / 1MB)
