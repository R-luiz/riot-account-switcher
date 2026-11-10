# Riot Account Switcher

A small Windows app for switching between your own Riot accounts and opening League of Legends. Sign in once through Riot's official screen; unlock saved sign-ins with Windows Hello. The switcher never asks for, stores, pastes, or autofills your Riot password.

**Unofficial software, not affiliated with or endorsed by Riot Games.**

## Install

1. Open [the latest release](https://github.com/R-luiz/riot-account-switcher/releases/latest).
2. Download `RiotAccountSwitcher-<version>-Setup-x64.exe` from **Assets**, not GitHub's source-code ZIP.
3. Run the installer, then open **Riot Account Switcher** from the Start menu.

Installation is for your Windows user only and does not request administrator access. A Desktop shortcut is optional. No terminal, SDK, Pengu Loader, browser extension, or Riot API key is required.

Requirements:

- Windows 10 or 11 on an Intel/AMD 64-bit PC. ARM and macOS/Linux are not supported by this release.
- .NET Framework 4.8 or newer. The installer checks this and explains how to obtain it if missing; it does not silently download software.
- Windows Hello configured under **Settings > Accounts > Sign-in options**.
- Riot Client and League of Legends installed. Run the switcher and Riot as the same normal Windows user.

Prefer no installer? Download `RiotAccountSwitcher-<version>-Portable-x64.zip`, extract it, and run `RiotAccountSwitcher.exe`. Keep its `.exe.config` file beside it. "Portable" means no installation, **not portable saved accounts**: account data still belongs to the current Windows user.

## Windows security warnings

**The initial release is unsigned.** An unknown-publisher or SmartScreen warning is possible; some Windows policies and Smart App Control can block it. A clean antivirus scan does not establish SmartScreen reputation or guarantee that another antivirus will accept the app.

Download only from this repository's releases. Each release includes `SHA256SUMS.txt`; compare a downloaded file's hash using:

```powershell
Get-FileHash .\RiotAccountSwitcher-1.0.0-Setup-x64.exe -Algorithm SHA256
```

Checksums identify the published bytes; they are not a code-signing certificate or proof of safety. Do not disable Defender, add exclusions, or install a self-signed root certificate to use this app. If Windows blocks it, keep the protection enabled and report the detection name and release version without uploading your account data.

Trusted publisher signing requires a verified signing identity; even signed new apps can receive reputation warnings. See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Use

- **+ Add account**: closes League and the Riot Client, then opens Riot's official sign-in screen. Sign in normally, including MFA, and enable **Stay signed in**.
- **Click an account**: confirm Windows Hello. The switcher saves the outgoing session, restores the selected session, and opens League. Clicking the already-active account just opens League.
- **Account menu > Rename label**: use a label such as Main or Smurf.
- **Account menu > Remove**: forget its saved sign-in. Removing the active account also closes the clients and signs it out after confirmation.

The application checks for running League/Valorant game processes and committed League phases before switching. Do not use it during matchmaking or a match; unavailable client APIs can limit phase detection.

Use the switcher rather than Riot's **Sign out** when changing accounts: signing out can revoke a saved session. Riot can expire sessions or request verification at any time. Sign in manually again when asked. The launcher currently targets League's live patchline, not arbitrary Riot games.

## Privacy and security boundaries

Saved session snapshots are encrypted with Windows DPAPI for the current user, using additional entropy derived from a Windows Hello signature. The Hello private key remains managed by Windows. There is no DPAPI-only fallback if Hello is unavailable.

**Sessions are credentials too.** Riot needs the active session in its own data folder. This does not protect it against same-user malware, an unlocked compromised PC, or a compromised Riot Client. Account names, identifiers, labels, and action logs are readable; session snapshots are encrypted in the vault. The app reads local client APIs on loopback and has no analytics or account-upload service.

A Windows reinstall, Hello key reset, or loss of DPAPI keys can make saved sessions unreadable. Sign in again; do not copy a vault to another PC expecting it to decrypt. Every recipient must add their own accounts.

This tool replaces Riot Client session files and starts/stops client processes; it does not modify game memory or Vanguard. Riot's internal file formats and local APIs are unofficial and may change. No Riot approval, anti-cheat compatibility guarantee, or security certification is claimed.

## Updates and uninstall

Close the switcher, then run a newer installer. Installation/uninstallation is blocked while the switcher is open; it is not forcibly terminated mid-switch.

Uninstall from **Settings > Apps > Installed apps**. Uninstall removes the application and installer-created shortcuts, but deliberately preserves saved accounts, logs, the Hello credential, and Riot's own data. To forget accounts, remove them inside the app before uninstalling. Uninstalling is not a Riot sign-out or server-side session revocation.

Local files:

| Location | Contents |
|---|---|
| `%LOCALAPPDATA%\Programs\RiotAccountSwitcher` | Installed app and uninstaller |
| `%LOCALAPPDATA%\RiotAccountSwitcher\vault.json` | Account metadata and encrypted sessions |
| `%LOCALAPPDATA%\RiotAccountSwitcher\switcher.log` | Action/error log, potentially containing account names |
| `%LOCALAPPDATA%\Riot Games\Riot Client\Data` | Riot-owned active session; never share |

Windows Hello credential name: `RiotAccountSwitcher`. Never attach your vault, Riot data, lockfiles, or unredacted logs to issues.

## Build and release

Source: C# 10, WPF, .NET Framework 4.8, x64. Build on Windows with a .NET SDK supporting C# 10 or newer, the .NET Framework 4.8 targeting pack, and [Inno Setup 6.7 or newer](https://jrsoftware.org/isdl.php).

```powershell
.\build-release.ps1
# If Inno Setup is installed elsewhere:
.\build-release.ps1 -IsccPath 'C:\path\to\ISCC.exe'
```

The script builds the application and creates the installer, portable ZIP, and SHA-256 manifest under `dist/`. Payloads use an explicit file allowlist: application, configuration, README, and license only. No account data, logs, or development dependencies are bundled. `install.ps1` is a developer convenience that builds these same assets and starts the installer.

The GitHub Actions workflow builds and exercises installation, application launch, upgrade protection, and uninstall on a clean Windows runner. Manual runs produce downloadable workflow artifacts. Pushing a `v<version>` tag matching the project version prepares a draft GitHub release after successful checks. Review and scan those exact assets before publishing the draft. Actions are pinned to commit hashes.

## Verification scope

The original implementation was exercised with two accounts on one Windows PC, including Windows Hello and restored sign-ins in both directions. Public packaging checks do not authenticate a Riot account in CI. Long-term session expiry, other regions, Hello-reset recovery, and every Windows/security-policy combination have not been verified.

## License

[MIT](LICENSE). Riot Games names and trademarks belong to their respective owners.

