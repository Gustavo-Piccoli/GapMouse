[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDir,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidateSet('x64', 'arm64')][string]$Arch,
    [string]$OutputDir
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Invalid version '$Version'" }
if (-not (Test-Path -LiteralPath $PublishDir -PathType Container)) { throw "$PublishDir not found" }

Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $OutputDir) { $OutputDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'artifacts\dist' }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$zip = Join-Path (Resolve-Path -LiteralPath $OutputDir).Path "GapMouse-$Version-$Arch-portable.zip"

$staging = Join-Path ([IO.Path]::GetTempPath()) ("gapmouse-zip-" + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $staging | Out-Null
    foreach ($name in 'GapMouse.exe', 'LICENSE.txt') {
        $src = Join-Path $PublishDir $name
        if (-not (Test-Path -LiteralPath $src)) { throw "$src not found" }
        Copy-Item -LiteralPath $src -Destination $staging
    }
    Get-ChildItem -LiteralPath $PublishDir -Filter '*.dll' -File | Copy-Item -Destination $staging
    [IO.File]::WriteAllBytes((Join-Path $staging 'portable'), [byte[]]@())

    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}

$zip
