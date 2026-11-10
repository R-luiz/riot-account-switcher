# Riot Account Switcher 1.0.0

First public Windows x64 release.

## Install

Download **RiotAccountSwitcher-1.0.0-Setup-x64.exe**, run it, then open Riot Account Switcher from the Start menu. No administrator privileges, SDK, terminal commands, or Pengu Loader required. Requires Windows 10/11 x64, .NET Framework 4.8, Windows Hello, Riot Client, and League of Legends.

For installation-free use, extract **RiotAccountSwitcher-1.0.0-Portable-x64.zip** and run the executable with its configuration file beside it. Each user must sign in to their own Riot accounts with **Stay signed in** enabled.

## Included

- Account picker with Windows Hello-protected saved Riot sessions; no password collection or autofill.
- Current-user installer, optional Desktop shortcut, Start menu entry, and Windows uninstall registration.
- Running-app protection during installation/uninstallation; saved account data preserved across upgrade/uninstall.
- Portable ZIP and **SHA256SUMS.txt** for verifying the published downloads.
- Public source, MIT license, and pinned GitHub Actions build with clean-Windows installer lifecycle checks.

## Security notice

**This release is unsigned.** SmartScreen may show an unknown-publisher warning; Smart App Control or organizational policies may block execution. Antivirus acceptance cannot be guaranteed. Do not disable security software or add exclusions. Checksums are integrity checks, not proof of safety or a trusted publisher signature.

Sessions are credentials. The vault encrypts saved snapshots, but Riot's active session remains accessible to the current Windows user. Never share your vault, Riot data, lockfiles, or unredacted logs. Uninstall preserves account data and is not a Riot sign-out. This is unofficial software, not endorsed by Riot Games.
