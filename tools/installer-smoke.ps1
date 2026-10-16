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

    With -Upgrade <older setup.exe> it first installs that older version and leaves it running,
    then installs this version over it. Extra checks: setup closed the running game itself, and
    there is exactly one SimoSlime Apps entry, at this version.

.EXAMPLE
    tools\installer-smoke.ps1 -Upgrade Release\SimoSlime-Setup-0.0.1.exe
#>
param(
    [string]$Upgrade
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$appId = Get-AppId
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\SimoSlime'
$exe = Join-Path $installDir 'SimoSlime.exe'
$appsEntry = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($appId)_is1"
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SimoSlime.lnk'
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SimoSlime.lnk'
$prefsKey = 'HKCU:\Software\WildPastry\SimoSlime'
$publisherKey = 'HKCU:\Software\WildPastry'
$localLow = Join-Path $env:USERPROFILE 'AppData\LocalLow\WildPastry\SimoSlime'
$playerLog = Join-Path $localLow 'Player.log'

$version = Get-BundleVersion
$setup = Join-Path $Repo "Release\SimoSlime-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "No $setup; run tools\build.ps1 first" }

$failures = 0
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "ok    $What" } else { Write-Host "FAIL  $What"; $script:failures++ }
}

# Runs a setup or uninstaller silently, logging to Logs\<LogName>, and waits for it.
function Invoke-Silently([string]$Program, [string]$LogName) {
    Start-Process $Program -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$logs\$LogName`"" -Wait -PassThru
}

# Starts the installed game windowed and returns it 8 s later, once it has had time to fail.
function Start-Game {
    $game = Start-Process $exe -ArgumentList '-screen-fullscreen 0 -screen-width 1280 -screen-height 720' -WindowStyle Normal -PassThru
    Start-Sleep -Seconds 8
    $game.Refresh()
    return $game
}

# Uninstalls whatever the Apps entry points at, wherever it was installed. The uninstaller
# re-launches itself from %TEMP% and returns at once, so wait on its effects.
function Uninstall {
    if (-not (Test-Path $appsEntry)) { return }
    $entry = Get-ItemProperty $appsEntry
    $uninstaller = $entry.UninstallString.Trim('"')
    $folder = $entry.InstallLocation
    Invoke-Silently $uninstaller 'uninstall.log' | Out-Null
    $until = (Get-Date).AddSeconds(60)
    while (((Test-Path $appsEntry) -or ($folder -and (Test-Path $folder))) -and (Get-Date) -lt $until) {
        Start-Sleep -Milliseconds 500
    }
}

$logs = Join-Path $Repo 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

Write-Host "Removing any existing install"
Get-Process SimoSlime -ErrorAction SilentlyContinue | Stop-Process -Force
Uninstall

if ($Upgrade) {
    if (-not (Test-Path $Upgrade)) { throw "No older setup at $Upgrade" }
    Write-Host "Installing the older $Upgrade"
    $old = Invoke-Silently $Upgrade 'install-old.log'
    Check ($old.ExitCode -eq 0) "older setup exit code 0 (got $($old.ExitCode))"
    $oldVersion = if (Test-Path $appsEntry) { (Get-ItemProperty $appsEntry).DisplayVersion }
    Check ($oldVersion -and $oldVersion -ne $version) "an older version is installed ($oldVersion)"

    Write-Host 'Running it while this version installs over it'
    $running = Start-Game
    Check (-not $running.HasExited) 'older game running before the upgrade'
}

Write-Host "Installing $setup"
$install = Invoke-Silently $setup 'install.log'
Check ($install.ExitCode -eq 0) "setup exit code 0 (got $($install.ExitCode))"
if ($Upgrade) {
    # Silent setup closes apps holding its files through the Restart Manager and logs it; Inno
    # logs "Some applications could not be shut down." when that fails. Whether the old game
    # exited is read before this script kills a leftover, so only setup can make it pass.
    $closedBySetup = $running.WaitForExit(5000)
    if (-not $closedBySetup) { Stop-Process -Id $running.Id -Force }
    $installLog = Get-Content "$logs\install.log" -Raw
    Check ($closedBySetup -and $installLog -match 'Shutting down applications using our files' -and $installLog -notmatch 'could not be shut down') 'setup closed the running older game'
    $entries = @(Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' |
        ForEach-Object { Get-ItemProperty $_.PSPath } | Where-Object { $_.DisplayName -eq 'SimoSlime' })
    Check ($entries.Count -eq 1) "one SimoSlime Apps entry (got $($entries.Count))"
}
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
$game = Start-Game
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
# The installer creates the key empty, so check for a value the game itself wrote on quit.
$prefs = @(Get-Item $prefsKey -ErrorAction SilentlyContinue | ForEach-Object { $_.GetValueNames() } | Where-Object { $_ -like 'Screenmanager*' })
Check ($prefs.Count -gt 0) "game saved PlayerPrefs before uninstall ($($prefs.Count) Screenmanager values)"

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
# Only an editor open on this project shares the folder (the mirror's batch runs are elsewhere).
$projectArg = '-projectpath\s+"?' + [regex]::Escape($Repo) + '"?(\s|$)'
$editorRunning = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/', '\') -match $projectArg }).Count -gt 0
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
