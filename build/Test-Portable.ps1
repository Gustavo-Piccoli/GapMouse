[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ZipPath,
    [int]$TimeoutSeconds = 30
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { throw "$ZipPath not found" }

$running = @(Get-Process -Name 'GapMouse' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw 'A GapMouse process is already running. Close it first: the single-instance mutex would make the launched copy exit silently.'
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("gapmouse-portable-" + [Guid]::NewGuid().ToString('N'))
$process = $null
try {
    New-Item -ItemType Directory -Path $temp | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $ZipPath).Path, $temp)

    $exe = Join-Path $temp 'GapMouse.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "GapMouse.exe not found in $ZipPath" }

    $process = Start-Process -FilePath $exe -ArgumentList '--minimized' -WorkingDirectory $temp -PassThru
    $logDir = Join-Path $temp 'logs'
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $found = $false
    $logText = ''
    while (-not $found -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
        if ($process.HasExited) { throw "GapMouse exited early with code $($process.ExitCode)" }
        if (Test-Path -LiteralPath $logDir) {
            $logText = ''
            foreach ($log in @(Get-ChildItem -LiteralPath $logDir -Filter 'gapmouse-*.log' -File)) {
                try {
                    $stream = [IO.File]::Open($log.FullName, 'Open', 'Read', 'ReadWrite')
                    try { $logText += (New-Object IO.StreamReader($stream)).ReadToEnd() } finally { $stream.Dispose() }
                }
                catch { }
            }
            if ($logText -match '\| START \|') { $found = $true }
        }
    }
    if (-not $found) {
        if ($logText) { $shown = $logText } else { $shown = 'no log file' }
        throw "No START log entry within $TimeoutSeconds s. Log: $shown"
    }

    $process.Refresh()
    if ($process.HasExited) { throw "GapMouse exited after START with code $($process.ExitCode)" }
    Write-Host "Portable OK: $ZipPath"
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; $process.WaitForExit(5000) | Out-Null }
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
