[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Version,
    [ValidateSet('x64', 'arm64')][string[]]$Arch = @('x64', 'arm64'),
    [switch]$RequireInstaller
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Invalid version '$Version'" }

foreach ($a in $Arch) {
    $zipPath = Join-Path $Directory "GapMouse-$Version-$a-portable.zip"
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw "$zipPath not found" }

    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $zipPath).Path)
    try {
        $entries = @($zip.Entries)
        $names = @($entries | ForEach-Object { $_.FullName })
        $nested = @($names | Where-Object { $_ -match '[\/]' })
        if ($nested.Count -gt 0) { throw "$zipPath has subfolders or nested entries: $($nested -join ', ')" }
        $pdbs = @($names | Where-Object { $_ -like '*.pdb' })
        if ($pdbs.Count -gt 0) { throw "$zipPath contains .pdb files: $($pdbs -join ', ')" }
        foreach ($required in 'GapMouse.exe', 'LICENSE.txt', 'portable') {
            if ($names -cnotcontains $required) { throw "$zipPath is missing $required" }
        }
        $portable = $entries | Where-Object { $_.FullName -ceq 'portable' }
        if ($portable.Length -ne 0) { throw "$zipPath 'portable' marker must be 0 bytes (is $($portable.Length))" }
        $dlls = @($names | Where-Object { $_ -like '*.dll' })
        if ($dlls.Count -lt 1) { throw "$zipPath contains no .dll files" }
        $allowed = @('GapMouse.exe', 'LICENSE.txt', 'portable') + $dlls
        $extra = @($names | Where-Object { $allowed -cnotcontains $_ })
        if ($extra.Count -gt 0) { throw "$zipPath has unexpected entries: $($extra -join ', ')" }
    }
    finally {
        $zip.Dispose()
    }

    if ($RequireInstaller) {
        $setup = Join-Path $Directory "GapMouse-Setup-$Version-$a.exe"
        if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw "$setup not found" }
    }
}

$sumsPath = Join-Path $Directory 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $sumsPath -PathType Leaf)) { throw "$sumsPath not found" }
$bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $sumsPath).Path)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$sumsPath has a BOM" }
if ($bytes -contains 13) { throw "$sumsPath contains CR characters (LF line endings required)" }
if ($bytes.Length -eq 0 -or $bytes[$bytes.Length - 1] -ne 10) { throw "$sumsPath must end with a trailing LF" }

$text = [Text.Encoding]::UTF8.GetString($bytes)
$lines = @($text.Substring(0, $text.Length - 1).Split("`n"))
$listed = @{}
foreach ($line in $lines) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "$sumsPath has a malformed line: '$line'" }
    if ($listed.ContainsKey($Matches[2])) { throw "$sumsPath lists '$($Matches[2])' twice" }
    $listed[$Matches[2]] = $Matches[1]
}

$artifacts = @(Get-ChildItem -LiteralPath $Directory -File -Filter 'GapMouse-*')
if ($artifacts.Count -ne $lines.Count) {
    throw "$sumsPath has $($lines.Count) lines but the directory has $($artifacts.Count) GapMouse-* artifacts"
}
foreach ($file in $artifacts) {
    if (-not $listed.ContainsKey($file.Name)) { throw "$sumsPath has no entry for $($file.Name)" }
    $actual = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $listed[$file.Name]) { throw "Hash mismatch for $($file.Name): expected $($listed[$file.Name]), actual $actual" }
}

Write-Host "Package OK: $Directory ($($Arch -join ', '))"
