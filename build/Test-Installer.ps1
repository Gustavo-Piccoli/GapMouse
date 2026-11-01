[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallerPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) { throw "$InstallerPath not found" }
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path

$running = @(Get-Process -Name 'GapMouse' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw 'A GapMouse process is already running. Close it first: the installer would detect it through the single-instance mutex.'
}

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{DBDB1941-7D22-4FE9-85C8-99227AAA11FE}_is1'
if (Test-Path -LiteralPath $uninstallKey) {
    throw 'A real GapMouse installation is present (uninstall key exists). Uninstall it first: this test would hijack or orphan it.'
}
$uninstallKeyMachine = $uninstallKey -replace '^HKCU:', 'HKLM:'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'GapMouse'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'GapMouse.lnk'

function Get-RunValue {
    $item = Get-ItemProperty -LiteralPath $runKey -Name $runName -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    $item.$runName
}

function Set-RunValue([string]$Value) {
    if (-not (Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey -Force | Out-Null }
    Set-ItemProperty -LiteralPath $runKey -Name $runName -Value $Value
}

function Remove-RunValue {
    Remove-ItemProperty -LiteralPath $runKey -Name $runName -ErrorAction SilentlyContinue
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function New-Scratch {
    $path = Join-Path ([IO.Path]::GetTempPath()) ('gapmouse-inst-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    $path
}

function Show-Log([string]$LogPath) {
    if (Test-Path -LiteralPath $LogPath) {
        Write-Host "---- Inno log: $LogPath ----"
        Get-Content -LiteralPath $LogPath | ForEach-Object { Write-Host $_ }
        Write-Host '---- end of Inno log ----'
    }
    else {
        Write-Host "No Inno log at $LogPath"
    }
}

# Runs the installer silently and returns its exit code. Prints the Inno log when the code is not in -ExpectedExit.
function Invoke-Install([string]$Dir, [string]$LogPath, [string]$Tasks, [int[]]$ExpectedExit = @(0)) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$Dir`"", "/LOG=`"$LogPath`"", "/TASKS=`"$Tasks`"")
    $p = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
    if ($ExpectedExit -notcontains $p.ExitCode) { Show-Log $LogPath }
    $p.ExitCode
}

# The uninstaller copies itself to temp and re-launches, so the parent's exit does not mean it finished.
function Invoke-Uninstall([string]$Dir) {
    $unins = Join-Path $Dir 'unins000.exe'
    Assert (Test-Path -LiteralPath $unins) "$unins exists"
    Start-Process -FilePath $unins -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ((Test-Path -LiteralPath $uninstallKey) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
    Assert (-not (Test-Path -LiteralPath $uninstallKey)) 'uninstall key disappears within 60 s'
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ((Test-Path -LiteralPath (Join-Path $Dir 'GapMouse.exe')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
}

$originalRun = Get-RunValue
$dirs = New-Object System.Collections.Generic.List[string]
$started = $null
try {
    # Phase 1: install with the startup task, then uninstall.
    Write-Host '== Phase 1: install with startup task'
    $dir = New-Scratch; $dirs.Add($dir)
    $log = "$dir.install1.log"
    Remove-RunValue
    $code = Invoke-Install $dir $log 'startup'
    Assert ($code -eq 0) "installer exit code is 0 (was $code)"
    Assert (Test-Path -LiteralPath (Join-Path $dir 'GapMouse.exe')) 'GapMouse.exe installed'
    Assert (Test-Path -LiteralPath (Join-Path $dir 'LICENSE.txt')) 'LICENSE.txt installed'
    Assert (-not (Test-Path -LiteralPath (Join-Path $dir 'portable'))) 'no portable marker installed'
    Assert (-not (Test-Path -LiteralPath (Join-Path $dir 'GapMouse.settings.json'))) 'no settings file installed'
    $expected = '"' + (Join-Path $dir 'GapMouse.exe') + '" --minimized'
    $actual = Get-RunValue
    Assert ($actual -ceq $expected) "Run value is exactly '$expected' (was '$actual')"
    Assert (Test-Path -LiteralPath $uninstallKey) 'uninstall key exists under HKCU'
    Assert (-not (Test-Path -LiteralPath $uninstallKeyMachine)) 'uninstall key does not exist under HKLM'
    Assert (Test-Path -LiteralPath $shortcut) "Start Menu shortcut exists ($shortcut)"
    Invoke-Uninstall $dir
    Assert (-not (Test-Path -LiteralPath (Join-Path $dir 'GapMouse.exe'))) 'GapMouse.exe removed by uninstall'
    Assert ($null -eq (Get-RunValue)) 'Run value removed by uninstall'
    Assert (-not (Test-Path -LiteralPath $shortcut)) 'shortcut removed by uninstall'

    # Phase 2: no startup task; uninstall must not touch a foreign Run value.
    Write-Host '== Phase 2: install without startup task'
    $dir = New-Scratch; $dirs.Add($dir)
    $log = "$dir.install2.log"
    $code = Invoke-Install $dir $log ''
    Assert ($code -eq 0) "installer exit code is 0 (was $code)"
    Assert ($null -eq (Get-RunValue)) 'Run value absent without startup task'
    $foreign = '"C:\Elsewhere\GapMouse.exe" --minimized'
    Set-RunValue $foreign
    Invoke-Uninstall $dir
    Assert ((Get-RunValue) -ceq $foreign) 'foreign Run value untouched by uninstall'
    Remove-RunValue

    # Phase 3: second install while the app runs.
    Write-Host '== Phase 3: install while the app is running'
    $dir = New-Scratch; $dirs.Add($dir)
    $log = "$dir.install3a.log"
    $code = Invoke-Install $dir $log ''
    Assert ($code -eq 0) "installer exit code is 0 (was $code)"
    $exe = Join-Path $dir 'GapMouse.exe'
    $started = Start-Process -FilePath $exe -ArgumentList '--minimized' -PassThru
    $mutexReady = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not $mutexReady -and [DateTime]::UtcNow -lt $deadline) {
        if ($started.HasExited) { throw 'Installed app exited before creating its single-instance mutex' }
        $m = $null
        if ([System.Threading.Mutex]::TryOpenExisting('Local\GapMouse.SingleInstance', [ref]$m)) {
            $m.Dispose()
            $mutexReady = $true
        }
        else { Start-Sleep -Milliseconds 250 }
    }
    Assert $mutexReady 'app created its single-instance mutex within 20 s'
    Assert (-not $started.HasExited) 'installed app is still running'
    # Inno preserves source timestamps, so a sentinel is needed to detect an overwrite.
    $sentinel = [DateTime]::SpecifyKind([DateTime]'2000-01-01T00:00:00', [DateTimeKind]::Utc)
    (Get-Item -LiteralPath $exe).LastWriteTimeUtc = $sentinel
    Assert ((Get-Item -LiteralPath $exe).LastWriteTimeUtc -eq $sentinel) 'sentinel timestamp applied to the running exe'
    $log = "$dir.install3b.log"
    # The expected exit list is empty of real codes on purpose: Invoke-Install then prints the Inno log once.
    $code = Invoke-Install $dir $log '' @(-1)
    Write-Host "Second silent install while running exited with code $code"
    Assert ($code -ne 0) 'second install while the app runs exits non-zero'
    $logText = Get-Content -LiteralPath $log -Raw
    Assert ($logText -match 'is currently running') 'Inno log reports that the app is currently running'
    Assert ((Get-Item -LiteralPath $exe).LastWriteTimeUtc -eq $sentinel) 'GapMouse.exe untouched (sentinel timestamp intact) by the blocked install'
    Stop-Process -Id $started.Id -Force
    $started.WaitForExit(10000) | Out-Null
    $started = $null
    Invoke-Uninstall $dir

    Write-Host 'Installer OK'
}
finally {
    if ($null -ne $started -and -not $started.HasExited) { Stop-Process -Id $started.Id -Force -ErrorAction SilentlyContinue }
    if ($null -ne $originalRun) { Set-RunValue $originalRun } else { Remove-RunValue }
    foreach ($d in $dirs) {
        if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
