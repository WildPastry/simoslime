# Shared by the tool scripts; dot-source it: . (Join-Path $PSScriptRoot 'common.ps1')

$Repo = Split-Path -Parent $PSScriptRoot

# The version the generator wrote into ProjectSettings from ProjectConfig.Version.
function Get-BundleVersion {
    $settings = Join-Path $Repo 'ProjectSettings\ProjectSettings.asset'
    $line = Select-String -Path $settings -Pattern '^\s*bundleVersion:\s*(\S+)'
    if (-not $line) { throw "No bundleVersion in $settings" }
    return $line.Matches[0].Groups[1].Value
}

# The installer's fixed AppId, read from its one home in installer\SimoSlime.iss.
function Get-AppId {
    $iss = Join-Path $Repo 'installer\SimoSlime.iss'
    $line = Select-String -Path $iss -Pattern '^AppId=\{\{([0-9A-Fa-f-]+)\}'
    if (-not $line) { throw "No AppId in $iss" }
    return '{' + $line.Matches[0].Groups[1].Value + '}'
}
