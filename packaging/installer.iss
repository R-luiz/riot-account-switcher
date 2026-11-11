#ifndef MyAppVersion
  #error MyAppVersion must be supplied by build-release.ps1
#endif
#ifndef StageDir
  #error StageDir must be supplied by build-release.ps1
#endif

[Setup]
AppId={{9450129B-8667-4F18-9132-7BD6FD78AD9A}
AppName=Riot Account Switcher
AppVersion={#MyAppVersion}
AppPublisher=R-luiz
AppPublisherURL=https://github.com/R-luiz/riot-account-switcher
AppSupportURL=https://github.com/R-luiz/riot-account-switcher/issues
AppUpdatesURL=https://github.com/R-luiz/riot-account-switcher/releases
AppCopyright=Copyright (c) 2026 R-luiz
VersionInfoVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\RiotAccountSwitcher
DefaultGroupName=Riot Account Switcher
PrivilegesRequired=lowest
MinVersion=10.0
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\RiotAccountSwitcher
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\RiotAccountSwitcher.exe
OutputBaseFilename=RiotAccountSwitcher-{#MyAppVersion}-Setup-x64
Compression=zip
SolidCompression=no
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#StageDir}\RiotAccountSwitcher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\RiotAccountSwitcher.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Riot Account Switcher"; Filename: "{app}\RiotAccountSwitcher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Riot Account Switcher"; Filename: "{app}\RiotAccountSwitcher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\RiotAccountSwitcher.exe"; Description: "Launch Riot Account Switcher"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 528040);
  if not Result then
    MsgBox('Riot Account Switcher requires .NET Framework 4.8 or later.' + #13#10 + #13#10 +
      'Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48 and run Setup again.',
      mbError, MB_OK);
end;
