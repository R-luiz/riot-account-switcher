[CmdletBinding()]
param(
    [string] $IsccPath,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'dist' }

$project = Join-Path $PSScriptRoot 'RiotAccountSwitcher.csproj'
[xml] $projectXml = Get-Content -LiteralPath $project -Raw
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode -or $versionNode.InnerText.Trim() -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw 'The project must specify a numeric Version in RiotAccountSwitcher.csproj.'
}
$version = $versionNode.InnerText.Trim()

if ($IsccPath) {
    if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
        throw "Inno Setup compiler not found: $IsccPath"
    }
    $compiler = (Resolve-Path -LiteralPath $IsccPath).ProviderPath
} else {
    $compiler = $null
    $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
    foreach ($root in $roots) {
        foreach ($name in @('Inno Setup 7', 'Inno Setup 6')) {
            $candidate = Join-Path (Join-Path $root $name) 'ISCC.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $compiler = $candidate
                break
            }
        }
        if ($compiler) { break }
    }
    if (-not $compiler) {
        throw 'Inno Setup 6.7 or later was not found. Install it, or supply -IsccPath with the path to ISCC.exe.'
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).ProviderPath
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('RiotAccountSwitcher-release-' + [guid]::NewGuid().ToString('N'))
$build = Join-Path $temp 'build'
$stage = Join-Path $temp 'stage'
$artifacts = Join-Path $temp 'artifacts'

try {
    New-Item -ItemType Directory -Path $build, $stage, $artifacts | Out-Null
    & dotnet build $project -c Release --nologo -o $build | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

    # Copy only the intended runtime and public documentation, never entire build directories.
    foreach ($name in @('RiotAccountSwitcher.exe', 'RiotAccountSwitcher.exe.config')) {
        Copy-Item -LiteralPath (Join-Path $build $name) -Destination $stage
    }
    foreach ($name in @('README.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage
    }

    $setupName = "RiotAccountSwitcher-$version-Setup-x64.exe"
    $portableName = "RiotAccountSwitcher-$version-Portable-x64.zip"
    $portable = Join-Path $artifacts $portableName
    $files = @('RiotAccountSwitcher.exe', 'RiotAccountSwitcher.exe.config', 'README.md', 'LICENSE') |
        ForEach-Object { Join-Path $stage $_ }
    Compress-Archive -LiteralPath $files -DestinationPath $portable -CompressionLevel Optimal

    $installerScript = Join-Path $PSScriptRoot 'packaging\installer.iss'
    & $compiler "/DMyAppVersion=$version" "/DStageDir=$stage" "/O$artifacts" $installerScript | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE." }

    $setup = Join-Path $artifacts $setupName
    if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
        throw "Inno Setup did not produce the expected installer: $setupName"
    }
    $checksums = Join-Path $artifacts 'SHA256SUMS.txt'
    @($setupName, $portableName) | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath (Join-Path $artifacts $_) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $_"
    } | Set-Content -LiteralPath $checksums -Encoding ASCII

    foreach ($name in @($setupName, $portableName, 'SHA256SUMS.txt')) {
        Copy-Item -LiteralPath (Join-Path $artifacts $name) -Destination (Join-Path $output $name) -Force
    }
    [pscustomobject]@{
        Setup = Join-Path $output $setupName
        Portable = Join-Path $output $portableName
        Checksums = Join-Path $output 'SHA256SUMS.txt'
    }
} finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
