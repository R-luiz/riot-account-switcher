[CmdletBinding()]
param(
    [string] $IsccPath,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'dist' }
$buildOptions = @{ OutputDirectory = $OutputDirectory }
if ($IsccPath) { $buildOptions.IsccPath = $IsccPath }
$release = & (Join-Path $PSScriptRoot 'build-release.ps1') @buildOptions
$release | Format-List | Out-Host
$installer = Start-Process -FilePath $release.Setup -Wait -PassThru
if ($installer.ExitCode -ne 0) {
    throw "Installer exited with code $($installer.ExitCode)."
}
