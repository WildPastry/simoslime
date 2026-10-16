<#
.SYNOPSIS
    Runs one DevAutoplay script on the Development build, windowed, and reports PASS or FAIL.

.DESCRIPTION
    Launches Build\Dev\SimoSlime.exe windowed at -Width x -Height with -autoplay <script>.
    Screenshots go to Logs\shots\<name> and the player log to Logs\player-<name>.log, where
    <name> is the script, plus "-<Tag>" when -Tag is given. -ShotScale 2 captures at twice the
    window size, for resolutions larger than the display. Exits 0 when the log ends in
    AUTOPLAY PASS.

.EXAMPLE
    tools\autoplay.ps1 -Script fight-win -Width 1920 -Height 1080 -Tag 1080p
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Script,
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$ShotScale = 1,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$exe = Join-Path $Repo 'Build\Dev\SimoSlime.exe'
if (-not (Test-Path $exe)) { throw "No Development build at $exe; run tools\unity.ps1 -Task build -Development" }

$name = if ($Tag) { "$Script-$Tag" } else { $Script }
$shots = Join-Path $Repo "Logs\shots\$name"
$log = Join-Path $Repo "Logs\player-$name.log"
Remove-Item $log -ErrorAction SilentlyContinue

$gameArgs = @('-screen-fullscreen', '0', '-screen-width', $Width, '-screen-height', $Height,
    '-autoplay', $Script, '-shots', "`"$shots`"", '-logFile', "`"$log`"")
if ($ShotScale -gt 1) { $gameArgs += '-shotscale', $ShotScale }

# Normal window style: the agent's shell is hidden, and the game window would inherit that.
$game = Start-Process $exe -ArgumentList $gameArgs -WindowStyle Normal -PassThru
$timeoutSeconds = 90
if (-not $game.WaitForExit($timeoutSeconds * 1000)) {
    $game.Kill()
    Write-Host "FAIL: $name still running after $timeoutSeconds s, killed; see $log"
    exit 1
}

$result = if (Test-Path $log) { Select-String -Path $log -Pattern '^AUTOPLAY (PASS|FAIL).*' | Select-Object -Last 1 }
if (-not $result) {
    Write-Host "FAIL: $name logged no AUTOPLAY result; see $log"
    exit 1
}

Write-Host $result.Line
if ($result.Matches[0].Groups[1].Value -ne 'PASS') { exit 1 }
exit 0
