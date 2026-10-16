<#
.SYNOPSIS
    Installs, runs and uninstalls Release\SimoSlime-Setup-<version>.exe silently, asserting that
    uninstall leaves nothing behind.

.DESCRIPTION
    Removes any existing SimoSlime install first (including the human's own), so run it before a
    hand test. Checks: the exe, both shortcuts and the Apps entry after install; the game stays
    up for 8 s with no exception in Player.log; and after uninstall the folder, shortcuts, Apps
    entry, PlayerPrefs registry key and LocalLow log folder are all gone. Exits 0 when every
    check passes.
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$appId = '{AB94FE52-C183-4AEE-94E3-3E5B9487E074}'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\SimoSlime'
$exe = Join-Path $installDir 'SimoSlime.exe'
$uninstaller = Join-Path $installDir 'unins000.exe'
$appsEntry = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($appId)_is1"
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SimoSlime.lnk'
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SimoSlime.lnk'
$prefsKey = 'HKCU:\Software\WildPastry\SimoSlime'
$publisherKey = 'HKCU:\Software\WildPastry'
$localLow = Join-Path $env:USERPROFILE 'AppData\LocalLow\WildPastry\SimoSlime'
$playerLog = Join-Path $localLow 'Player.log'

$versionLine = Select-String -Path (Join-Path $repo 'ProjectSettings\ProjectSettings.asset') -Pattern '^\s*bundleVersion:\s*(\S+)'
$version = $versionLine.Matches[0].Groups[1].Value
$setup = Join-Path $repo "Release\SimoSlime-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "No $setup; run tools\build.ps1 first" }

$failures = 0
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "ok    $What" } else { Write-Host "FAIL  $What"; $script:failures++ }
}

# The uninstaller re-launches itself from %TEMP% and returns at once, so wait on its effects.
function Uninstall {
    if (-not (Test-Path $uninstaller)) { return }
    Start-Process $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$logs\uninstall.log`"" -Wait
    $until = (Get-Date).AddSeconds(60)
    while (((Test-Path $appsEntry) -or (Test-Path $installDir)) -and (Get-Date) -lt $until) {
        Start-Sleep -Milliseconds 500
    }
}

$logs = Join-Path $repo 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

Write-Host "Removing any existing install"
Get-Process SimoSlime -ErrorAction SilentlyContinue | Stop-Process -Force
Uninstall

Write-Host "Installing $setup"
$install = Start-Process $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$logs\install.log`"" -Wait -PassThru
Check ($install.ExitCode -eq 0) "setup exit code 0 (got $($install.ExitCode))"
Check (Test-Path $exe) "SimoSlime.exe in $installDir"
Check (Test-Path $startMenu) 'Start menu shortcut'
Check (Test-Path $desktop) 'desktop shortcut'
Check (Test-Path $appsEntry) 'Apps entry'
if (Test-Path $appsEntry) {
    $entry = Get-ItemProperty $appsEntry
    Check ($entry.DisplayVersion -eq $version) "Apps entry version $version (got $($entry.DisplayVersion))"
    Check ($entry.Publisher -eq 'WildPastry') 'Apps entry publisher WildPastry'
}
Check (-not (Test-Path (Join-Path $installDir '*DontShip*'))) 'no Burst debug folder shipped'

Write-Host 'Running the installed game for 8 s'
Remove-Item $playerLog -ErrorAction SilentlyContinue
$game = Start-Process $exe -ArgumentList '-screen-fullscreen 0 -screen-width 1280 -screen-height 720' -WindowStyle Normal -PassThru
Start-Sleep -Seconds 8
$game.Refresh()
Check (-not $game.HasExited) 'game still running after 8 s'
# Close it like a user would, so Unity saves its PlayerPrefs on the way out.
if (-not $game.HasExited) {
    $game.CloseMainWindow() | Out-Null
    if (-not $game.WaitForExit(15000)) { Stop-Process -Id $game.Id -Force }
}
Check (Test-Path $playerLog) 'Player.log written'
if (Test-Path $playerLog) {
    Check (-not (Select-String -Path $playerLog -Pattern 'Exception' -Quiet)) 'no exception in Player.log'
}
Check (Test-Path $prefsKey) 'PlayerPrefs registry key exists before uninstall'

Write-Host 'Uninstalling'
Uninstall
Check (-not (Test-Path $installDir)) 'install folder gone'
Check (-not (Test-Path $startMenu)) 'Start menu shortcut gone'
Check (-not (Test-Path $desktop)) 'desktop shortcut gone'
Check (-not (Test-Path $appsEntry)) 'Apps entry gone'
Check (-not (Test-Path $prefsKey)) 'PlayerPrefs registry key gone'
# The Unity editor shares this folder as its persistentDataPath once Company and Product are set,
# writing TestResults.xml and Unity\<id>\Editor\... there. While an editor runs it may hold those
# open, so on this machine only the game's own files must go; with no editor the whole folder must.
$leftovers = @(Get-ChildItem $localLow -Recurse -Force -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName.Substring($localLow.Length + 1) })
$gameLeftovers = @($leftovers | Where-Object { $_ -ne 'TestResults.xml' -and $_ -notmatch '^Unity\\[^\\]+\\Editor\\' })
$editorRunning = @(Get-Process Unity -ErrorAction SilentlyContinue).Count -gt 0
Check ($gameLeftovers.Count -eq 0) "no game files left in LocalLow ($($gameLeftovers -join ', '))"
if ($editorRunning) {
    if ($leftovers.Count -gt 0) { Write-Host "note  a Unity editor is running; its own files stay in $localLow" }
}
else {
    Check (-not (Test-Path $localLow)) 'LocalLow folder gone'
}
Check (-not (Test-Path $publisherKey) -or @(Get-ChildItem $publisherKey).Count -gt 0) 'empty WildPastry registry key gone'

if ($failures -gt 0) { Write-Host "FAIL: $failures check(s) failed"; exit 1 }
Write-Host 'OK: installer smoke test'
exit 0
