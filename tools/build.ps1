<#
.SYNOPSIS
    Builds the installer: Release\SimoSlime-Setup-<version>.exe.

.DESCRIPTION
    Regenerates the scenes, makes a Release build, reads the version the generator wrote to
    ProjectSettings\ProjectSettings.asset, then compiles installer\SimoSlime.iss with Inno
    Setup 7. Exits 0 on success.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$unity = Join-Path $PSScriptRoot 'unity.ps1'

& $unity -Task generate
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL: generate'; exit 1 }
& $unity -Task build
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL: Release build'; exit 1 }

$version = Get-BundleVersion

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    'C:\Program Files\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 7\ISCC.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 7 (ISCC.exe) is not installed' }

$buildDir = Join-Path $Repo 'Build\Release'
& $iscc /Q "/DAppVersion=$version" "/DBuildDir=$buildDir" (Join-Path $Repo 'installer\SimoSlime.iss')
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: ISCC exit $LASTEXITCODE"; exit 1 }

$setup = Join-Path $Repo "Release\SimoSlime-Setup-$version.exe"
if (-not (Test-Path $setup)) { Write-Host "FAIL: no $setup"; exit 1 }
Write-Host "Built $setup"
exit 0
