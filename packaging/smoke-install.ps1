[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $SetupPath)

$ErrorActionPreference = 'Stop'
# This exercises real install/uninstall and shortcuts. Never run on someone's PC.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Run this smoke check only on a disposable GitHub-hosted Windows runner.'
}
$setup = (Resolve-Path -LiteralPath $SetupPath).ProviderPath
$install = Join-Path $env:LOCALAPPDATA 'Programs\RiotAccountSwitcher'
$data = Join-Path $env:LOCALAPPDATA 'RiotAccountSwitcher'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Riot Account Switcher\Riot Account Switcher.lnk'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Riot Account Switcher.lnk'
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9450129B-8667-4F18-9132-7BD6FD78AD9A}_is1'
if ((Test-Path -LiteralPath $install) -or (Test-Path -LiteralPath $data) -or (Test-Path -LiteralPath $registration)) {
    throw 'Smoke check requires a clean runner without an existing installation or account vault.'
}
function Invoke-Installer([string] $File, [string[]] $Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Installer did not finish within 60 seconds: $File" }
    $process.Refresh()
    return $process.ExitCode
}
$argsSilent = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-')
$code = Invoke-Installer $setup ($argsSilent + '/TASKS=desktopicon')
if ($code -ne 0) { throw "Installation failed: $code" }
$exe = Join-Path $install 'RiotAccountSwitcher.exe'
$config = "$exe.config"
$uninstall = Join-Path $install 'unins000.exe'
foreach ($path in @($exe, $config, $shortcut, $desktopShortcut, $uninstall, $registration)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Installation is missing $path" }
}
$shell = New-Object -ComObject WScript.Shell
if ($shell.CreateShortcut($shortcut).TargetPath -ne $exe) { throw 'Start menu shortcut points to the wrong executable.' }
if ((Get-ItemProperty -LiteralPath $registration).InstallLocation.TrimEnd('\') -ne $install) {
    throw 'Per-user uninstall registration points to the wrong directory.'
}

$app = Start-Process -FilePath $exe -PassThru
try {
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        $app.Refresh()
        if ($app.HasExited) { throw "Application exited during startup: $($app.ExitCode)" }
        if ($app.MainWindowHandle -ne [IntPtr]::Zero -and (Test-Path (Join-Path $data 'vault.json'))) { $ready = $true; break }
    }
    if (-not $ready) { throw 'Application did not reach its initialized account window.' }
    $originalHash = (Get-FileHash -LiteralPath $exe).Hash
    $code = Invoke-Installer $setup $argsSilent
    if ($code -eq 0) { throw 'Installer allowed replacement while the account switcher was running.' }
    $app.Refresh()
    if ($app.HasExited -or (Get-FileHash -LiteralPath $exe).Hash -ne $originalHash) {
        throw 'Blocked upgrade terminated or changed the running application.'
    }
    Write-Output 'PASS: per-user installation, shortcuts, registered uninstall, application startup, running-app upgrade protection.'
} finally {
    $app.Refresh()
    if (-not $app.HasExited) {
        if (-not $app.CloseMainWindow() -or -not $app.WaitForExit(10000)) { throw 'Application did not close normally.' }
    }
    $app.Dispose()
}

# Actual first-run vault has no accounts. Preserve its exact bytes across upgrade/uninstall.
$vault = Join-Path $data 'vault.json'
$vaultHash = (Get-FileHash -LiteralPath $vault).Hash
$code = Invoke-Installer $setup $argsSilent
if ($code -ne 0) { throw "Upgrade after closing the app failed: $code" }
if ((Get-FileHash -LiteralPath $vault).Hash -ne $vaultHash) { throw 'Upgrade changed private account data.' }
$code = Invoke-Installer $uninstall $argsSilent
if ($code -ne 0) { throw "Uninstall failed: $code" }
# The uninstaller deletes its own files using a child process.
for ($i = 0; $i -lt 40 -and (Test-Path -LiteralPath $exe); $i++) { Start-Sleep -Milliseconds 250 }
foreach ($path in @($exe, $shortcut, $desktopShortcut, $registration)) {
    if (Test-Path -LiteralPath $path) { throw "Uninstall left behind $path" }
}
if (-not (Test-Path -LiteralPath $vault) -or (Get-FileHash -LiteralPath $vault).Hash -ne $vaultHash) {
    throw 'Uninstall removed or changed private account data.'
}
Write-Output 'PASS: closed-app upgrade, uninstall removes application/shortcuts/registration, private vault preserved byte-for-byte.'
