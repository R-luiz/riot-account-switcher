# Riot Account Switcher

A small Windows app for switching between your own Riot accounts and opening League of Legends. Sign in once through Riot's official screen; unlock saved sign-ins with Windows Hello. The switcher never asks for, stores, pastes, or autofills your Riot password.

**Unofficial software, not affiliated with or endorsed by Riot Games.** Created by [R-luiz](https://github.com/R-luiz).

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

**Releases are not code-signed.** When you run the downloaded installer, Microsoft Defender SmartScreen shows "Windows protected your PC" ("Windows a protégé votre ordinateur") with **Unknown publisher** ("Éditeur inconnu").

That dialog means Windows found no publisher signature and the file is too new to have download reputation. It is a reputation prompt, not a malware detection. Naming the author in the app or installer cannot change it: Windows only shows a publisher taken from an Authenticode signature issued by a trusted certificate authority to a verified legal identity, and Microsoft states that newly signed apps can still be warned until reputation builds. See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

Before running it, download only from this repository's releases and compare the file's hash with that release's `SHA256SUMS.txt`:

```powershell
Get-FileHash .\RiotAccountSwitcher-*-Setup-x64.exe -Algorithm SHA256
```

If the hash matches and you trust this source, **More info > Run anyway** ("Informations complémentaires" > "Exécuter quand même") starts the installer. A checksum identifies the published bytes; it is not a signature or proof of safety, and a clean antivirus scan does not guarantee that another product will accept the app.

Smart App Control or an organization's policy can block unsigned apps with no option to continue. Do not disable Defender, add exclusions, or install a self-signed root certificate to use this app. If an antivirus reports a detection, keep the protection enabled and report the detection name and release version without uploading your account data.

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

## Code signing policy

**Status: the application was submitted to SignPath Foundation on 11 November 2026 and is awaiting review; no approval has been granted. Published releases remain unsigned.** This project does not currently claim SignPath endorsement or certificate coverage.

- Maintainer, reviewer, and prospective signing approver: [R-luiz](https://github.com/R-luiz).
- Any future signing request must originate from this repository's versioned source and GitHub Actions build. The maintainer reviews contributions and manually approves releases; downloaded or unrelated third-party executables must not be signed under this project.
- Maintainer accounts used for source control and signing must use multi-factor authentication. Provider enrollment and approval are prerequisites, not completed setup.
- Any granted signing credentials stay in the signing provider or protected build secrets, never in the repository, release files, or chat.
- If approved, the project will identify the signing service and certificate owner in this section and on its release pages. The current binaries must not be described as signed until their signatures are verified.
- Privacy: the switcher does not upload account data or telemetry. It reads local Riot APIs and session files; launching the official Riot Client allows that client to contact Riot under Riot's own policies. See [Privacy and security boundaries](#privacy-and-security-boundaries).

## License

[MIT](LICENSE), copyright (c) 2026 R-luiz. Riot Games names and trademarks belong to their respective owners.

