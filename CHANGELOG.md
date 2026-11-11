# Changelog

## 1.0.1

- Author credit: "Created by R-luiz" in the app window, linking to the author's GitHub profile.
- Installer file properties carry the copyright and file version.
- README explains the SmartScreen "Unknown publisher" prompt and how to verify a download before running it.

Account switching and saved data are unchanged. Releases remain unsigned, so the SmartScreen prompt still appears.

## 1.0.0

First public Windows x64 release.

- Account picker with Windows Hello-protected saved Riot sessions; no password collection or autofill.
- Current-user installer, optional Desktop shortcut, Start menu entry, and Windows uninstall registration.
- Running-app protection during installation/uninstallation; saved account data preserved across upgrade/uninstall.
- Portable ZIP and **SHA256SUMS.txt** for verifying the published downloads.
- Public source, MIT license, and pinned GitHub Actions build with clean-Windows installer lifecycle checks.

## Security notice for all releases

SmartScreen shows an unknown-publisher warning; Smart App Control or organizational policies may block execution. Antivirus acceptance cannot be guaranteed. Do not disable security software or add exclusions. Checksums are integrity checks, not proof of safety or a trusted publisher signature.

Sessions are credentials. The vault encrypts saved snapshots, but Riot's active session remains accessible to the current Windows user. Never share your vault, Riot data, lockfiles, or unredacted logs. Uninstall preserves account data and is not a Riot sign-out. This is unofficial software, not endorsed by Riot Games.
