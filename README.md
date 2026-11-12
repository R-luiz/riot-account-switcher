# Riot Account Switcher

Switch between your own Riot accounts and open League of Legends without retyping your password each time.

Sign in once through the official Riot Client, enable **Stay signed in**, then choose an account in the switcher. **Windows Hello** protects access to the saved sign-ins.

Created by [R-luiz](https://github.com/R-luiz). An independent, open-source project—not affiliated with or endorsed by Riot Games.

## Get started

1. Download `RiotAccountSwitcher-<version>-Setup-x64.exe` from [the latest release](https://github.com/R-luiz/riot-account-switcher/releases/latest).
2. Install it and open **Riot Account Switcher** from the Start menu.
3. Choose **+ Add account**. Sign in in Riot's own window, complete any verification, and tick **Stay signed in**.
4. Add your other accounts the same way. Click an account to switch to it.

The installer works for your Windows user without administrator access. A Desktop shortcut is optional. You do not need Pengu Loader, a browser extension, a Riot developer API key, or programming tools.

**Requirements:** Windows 10/11 on an Intel/AMD 64-bit PC, .NET Framework 4.8 or newer, Windows Hello configured, and Riot Client with League of Legends installed. Run Riot and the switcher as the same normal Windows user. The current launcher opens League's live version, not every Riot game.

For installation-free use, download the **Portable-x64.zip**, extract it, and run `RiotAccountSwitcher.exe`. Keep its `.exe.config` file beside it. Both editions save account data in your Windows profile; the portable ZIP does not make saved sign-ins transferable between PCs.

## How it works

The switcher uses **saved sessions, not saved passwords**. A session is the sign-in information Riot keeps when you choose **Stay signed in**.

When switching to a saved account, the app:

1. Checks for a running game and League phases where closing the client could interrupt play.
2. Asks Windows Hello to unlock the saved sign-ins. You confirm in Windows' PIN, fingerprint, or face dialog.
3. Closes the League and Riot clients so their session files can be replaced.
4. Saves an encrypted snapshot of the outgoing account's Riot Client `Data` folder. It refreshes this snapshot each time you switch away because Riot updates its session tokens.
5. Restores the selected account's snapshot and launches the official Riot Client to open League.

It reads account identity and client status through Riot's **local API on your computer**. It does not automate the login form or bypass Riot's authentication. If a session expires or Riot requests verification, you sign in normally again.

Opening an already-active account simply opens League. Initial onboarding may not need a Hello prompt because there is no saved session to unlock yet.

## Privacy and security boundaries

### What the app protects

- **Your Riot password stays in Riot's sign-in flow.** The switcher does not request, store, paste, or autofill it.
- **Saved inactive sessions are encrypted.** Windows DPAPI protects each snapshot for your Windows user, with additional key material derived through Windows Hello. The Hello private key stays managed by Windows; the derived key is not saved to disk.
- **There is no weaker fallback.** If Windows Hello is unavailable, the app cannot save or unlock encrypted snapshots without it.
- **Account data stays on your computer.** The switcher has no telemetry or account-upload service. Its client API requests go to localhost. The official Riot Client still connects to Riot as usual.

The source code and [build workflow](.github/workflows/release.yml) are public so you can inspect how the app works and how release files are produced.

### What to keep in mind

Saved sessions grant account access, just like Riot's own **Stay signed in** feature. While an account is active, Riot needs its session in Riot's data folder; encrypting the switcher's saved copies does not protect that active session against malware running as your Windows user. Account names, identifiers, labels, and action logs are readable.

Do not share your vault or Riot session files. After a Windows reinstall, Hello key reset, or loss of Windows encryption keys, you may need to add your accounts again. Copying the vault to another PC is not a supported way to transfer sign-ins.

The app does not modify game memory or Vanguard. It relies on unofficial Riot client APIs and session files, which Riot may change. No anti-cheat compatibility or security-certification claim is made.

## Everyday use

- **Click an account:** switch to it, or open League if it is already active.
- **Account menu > Rename label:** give it a label such as Main or Smurf.
- **Account menu > Remove:** delete its saved entry. Removing the active account also closes the clients and clears its active local session after confirmation.

Finish matchmaking, champion select, or your game before switching. The app blocks switching when it detects a running game or a committed League phase, but phase detection depends on the local API being available.

Use the switcher rather than Riot's **Sign out** when changing accounts: signing out may revoke a saved session. Being asked to sign in again after a session expires is normal.

## Windows SmartScreen

**Current releases are unsigned.** Windows may show **“Windows protected your PC” / “Windows a protégé votre ordinateur”** and **“Unknown publisher” / “Éditeur inconnu.”** That specific message concerns publisher identity and download reputation; it is not, by itself, a malware detection.

Download from this repository's releases and use the included `SHA256SUMS.txt` to check that your file matches the published download:

```powershell
Get-FileHash .\RiotAccountSwitcher-*-Setup-x64.exe -Algorithm SHA256
```

A matching checksum confirms the bytes, not their safety. If you trust the source after checking it, Windows may offer **More info > Run anyway**. Some security policies block unsigned apps entirely. Do not disable antivirus protection, add exclusions, or install a self-signed trust certificate to run the app.

Releases are not code-signed yet; see the [code signing policy](#code-signing-policy). Even newly signed apps can receive reputation warnings, as explained in [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Updates, uninstall, and local files

To update, close the switcher and run the newer installer. Installation and uninstallation are blocked while the app is open, so it is not forcibly stopped during a switch.

Uninstall through **Settings > Apps > Installed apps**. This removes the installed app and its installer-created shortcuts, but leaves account data, logs, the Hello credential, and Riot's files alone. Remove accounts in the app first if you want to forget their saved entries. Uninstalling is not a Riot sign-out or server-side session revocation.

| Location | Contents |
|---|---|
| `%LOCALAPPDATA%\Programs\RiotAccountSwitcher` | Installed app and uninstaller |
| `%LOCALAPPDATA%\RiotAccountSwitcher\vault.json` | Account list and encrypted session snapshots |
| `%LOCALAPPDATA%\RiotAccountSwitcher\switcher.log` | Action/error log; may include account names |
| `%LOCALAPPDATA%\Riot Games\Riot Client\Data` | Riot's active session files |

Windows Hello credential name: `RiotAccountSwitcher`. When reporting a problem, share the error and app version—not your vault, session files, lockfiles, or unredacted logs.

## Build from source

The app uses C# 10, WPF, and .NET Framework 4.8, targeting x64. Building requires Windows, a .NET SDK supporting C# 10 or newer, the .NET Framework 4.8 targeting pack, and [Inno Setup 6.7 or newer](https://jrsoftware.org/isdl.php).

```powershell
.\build-release.ps1
# If the compiler is installed elsewhere:
.\build-release.ps1 -IsccPath 'C:\path\to\ISCC.exe'
```

The script creates the installer, portable ZIP, and SHA-256 manifest under `dist/`. Only the executable, configuration, README, and license enter the application payload—not local account data or logs. `install.ps1` builds these same assets and starts the installer.

The GitHub Actions workflow checks installation, application startup, upgrade protection, uninstall, and vault preservation on a clean Windows runner. A matching `v<version>` tag prepares a draft release for review before publication. Actions are pinned to commit hashes.

Actual Riot sign-ins were tested locally with two accounts and Windows Hello, including switching in both directions. CI checks packaging and application startup, not Riot authentication. Long-term expiry, other regions, and recovery after a Hello reset have not been verified.

## Code signing policy

**Published releases are unsigned.** An application to the SignPath Foundation free signing program was not approved in November 2026; the project may reapply later. No signing provider or certificate currently covers these releases.

- Maintainer, reviewer, and prospective signing approver: [R-luiz](https://github.com/R-luiz).
- Signing must use this repository's versioned source and GitHub Actions builds, with contribution review and manual release approval—not unrelated executables.
- Source-control and signing accounts must use multi-factor authentication. Signing credentials belong in the provider or protected build secrets, never in source code or release files.
- If releases become signed, the provider and certificate owner will be identified here and on release pages. A build will only be described as signed after its signatures are verified.
- The [privacy policy above](#privacy-and-security-boundaries) also applies to signing-enabled releases: no account uploads or telemetry.

## License

[MIT](LICENSE), copyright (c) 2026 R-luiz. Riot Games names and trademarks belong to their respective owners.
