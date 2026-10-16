<#
.SYNOPSIS
    Runs Unity in batchmode on a mirror of this project, never on the repo folder itself.

.DESCRIPTION
    Mirrors Assets, Packages and ProjectSettings to %LOCALAPPDATA%\SimoSlime-mirror with
    robocopy /MIR, runs the task there and logs to Logs\unity-<task>.log, so the human's open
    editor never blocks it. The editor version comes from ProjectSettings\ProjectVersion.txt.

      tests     EditMode tests. Fails unless Logs\tests.xml reports failed="0".
      generate  SceneGenerator.GenerateAll, then copies the generated files back to the repo.
      build     A Windows x64 player in Build\Release, or Build\Dev with -Development.

    Exits 0 on success.

.EXAMPLE
    tools\unity.ps1 -Task build -Development
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('tests', 'generate', 'build')]
    [string]$Task,
    [switch]$Development,
    [string]$Out
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$mirror = Join-Path $env:LOCALAPPDATA 'SimoSlime-mirror'
$logs = Join-Path $repo 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

$versionFile = Join-Path $repo 'ProjectSettings\ProjectVersion.txt'
$versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(\S+)'
if (-not $versionLine) { throw "No m_EditorVersion in $versionFile" }
$version = $versionLine.Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version is not installed at $unity" }

function Sync-Dir([string]$From, [string]$To) {
    robocopy $From $To /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
    # robocopy exit codes below 8 mean success (0 = nothing to copy, 1 = copied, ...).
    if ($LASTEXITCODE -ge 8) { throw "robocopy $From -> $To failed with code $LASTEXITCODE" }
}

function Format-Arg([string]$Value) {
    if ($Value -match '\s') { return '"' + $Value + '"' }
    return $Value
}

foreach ($dir in 'Assets', 'Packages', 'ProjectSettings') {
    Sync-Dir (Join-Path $repo $dir) (Join-Path $mirror $dir)
}

$log = Join-Path $logs "unity-$Task.log"
$unityArgs = @('-batchmode', '-projectPath', $mirror, '-logFile', $log)
switch ($Task) {
    'tests' {
        $results = Join-Path $logs 'tests.xml'
        Remove-Item $results -ErrorAction SilentlyContinue
        # No -quit: the test runner exits by itself, and -quit would stop it before it runs.
        $unityArgs += '-runTests', '-testPlatform', 'EditMode', '-testResults', $results
    }
    'generate' {
        $unityArgs += '-quit', '-executeMethod', 'SimoSlime.Editor.SceneGenerator.GenerateAll'
    }
    'build' {
        if (-not $Out) { $Out = if ($Development) { 'Build\Dev' } else { 'Build\Release' } }
        $outDir = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $repo $Out }
        $outDir = $outDir.TrimEnd('\')
        $unityArgs += '-quit', '-buildTarget', 'win64',
            '-executeMethod', 'SimoSlime.Editor.BuildScript.BuildWindows64', '-buildOutput', $outDir
        if ($Development) { $unityArgs += '-development' }
    }
}

Write-Host "Unity $version, task $Task, log $log"
$start = New-Object System.Diagnostics.ProcessStartInfo $unity
$start.Arguments = ($unityArgs | ForEach-Object { Format-Arg $_ }) -join ' '
$start.UseShellExecute = $false
# WaitForExit on the process alone: Unity leaves helper processes (compiler server, licensing
# client) running, and waiting on the whole tree would hang.
$process = [System.Diagnostics.Process]::Start($start)
$process.WaitForExit()
$code = $process.ExitCode

switch ($Task) {
    'tests' {
        if (-not (Test-Path $results)) {
            Write-Host "FAIL: no test results at $results (Unity exit $code)"
            exit 1
        }
        $run = ([xml](Get-Content $results -Raw)).'test-run'
        Write-Host "Tests: $($run.total) total, $($run.passed) passed, $($run.failed) failed"
        if ($run.failed -ne '0' -or [int]$run.total -eq 0 -or $code -ne 0) {
            Write-Host "FAIL: see $results and $log (Unity exit $code)"
            exit 1
        }
    }
    'generate' {
        if ($code -ne 0) { Write-Host "FAIL: Unity exit $code, see $log"; exit 1 }
        # Copy back what the generator writes, plus the folder .meta beside each folder.
        foreach ($dir in 'Assets\Scenes', 'Assets\Prefabs', 'Assets\Art', 'Assets\TextMesh Pro') {
            $from = Join-Path $mirror $dir
            if (Test-Path $from) {
                Sync-Dir $from (Join-Path $repo $dir)
                if (Test-Path "$from.meta") { Copy-Item "$from.meta" "$(Join-Path $repo $dir).meta" -Force }
            }
        }
        foreach ($file in 'ProjectSettings\ProjectSettings.asset', 'ProjectSettings\EditorBuildSettings.asset') {
            Copy-Item (Join-Path $mirror $file) (Join-Path $repo $file) -Force
        }
        # A .meta Unity minted for a repo asset that had none: copy it so both sides agree.
        Get-ChildItem (Join-Path $mirror 'Assets') -Recurse -Filter '*.meta' | ForEach-Object {
            $relative = $_.FullName.Substring($mirror.Length + 1)
            $target = Join-Path $repo $relative
            $asset = $target.Substring(0, $target.Length - '.meta'.Length)
            if (-not (Test-Path $target) -and (Test-Path $asset)) {
                Copy-Item $_.FullName $target
                Write-Host "Copied missing meta: $relative"
            }
        }
    }
    'build' {
        $exe = Join-Path $outDir 'SimoSlime.exe'
        if ($code -ne 0 -or -not (Test-Path $exe)) {
            Write-Host "FAIL: Unity exit $code, see $log"
            exit 1
        }
        Write-Host "Built $exe"
    }
}

Write-Host "OK: $Task"
exit 0
