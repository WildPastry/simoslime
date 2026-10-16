; SimoSlime's per-user installer (Inno Setup 7). Built by tools/build.ps1, which passes the
; version read from ProjectSettings.asset and the Release build folder.

#ifndef AppVersion
  #error Pass /DAppVersion=<version>; tools/build.ps1 does.
#endif
#ifndef BuildDir
  #error Pass /DBuildDir=<Release build folder>; tools/build.ps1 does.
#endif

[Setup]
; Fixed forever: a new version with the same AppId replaces the old one in place.
AppId={{AB94FE52-C183-4AEE-94E3-3E5B9487E074}
AppName=SimoSlime
AppVersion={#AppVersion}
AppPublisher=WildPastry
; Per-user, so no admin prompt: {autopf} is %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\SimoSlime
DisableProgramGroupPage=yes
SetupArchitecture=x64
OutputDir=..\Release
OutputBaseFilename=SimoSlime-Setup-{#AppVersion}
UninstallDisplayName=SimoSlime
UninstallDisplayIcon={app}\SimoSlime.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Burst's debug folder must not ship; Unity 6.6 names it *_BackUpThisFolder_ButDontShipItWithYourGame.
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*_BurstDebugInformation_DoNotShip,*_BackUpThisFolder_ButDontShipItWithYourGame"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SimoSlime"; Filename: "{app}\SimoSlime.exe"
Name: "{autodesktop}\SimoSlime"; Filename: "{app}\SimoSlime.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SimoSlime.exe"; Description: "{cm:LaunchProgram,SimoSlime}"; Flags: postinstall nowait skipifsilent

[Registry]
; Unity keeps PlayerPrefs, including the saved window size, under this key. Created empty here
; so uninstall removes it, and the publisher key too once nothing else is in it.
Root: HKCU; Subkey: "Software\WildPastry"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\WildPastry\SimoSlime"; Flags: uninsdeletekey

[UninstallDelete]
; Unity writes Player.log and crash reports to %USERPROFILE%\AppData\LocalLow\WildPastry\SimoSlime.
Type: filesandordirs; Name: "{localappdata}\..\LocalLow\WildPastry\SimoSlime"
Type: dirifempty; Name: "{localappdata}\..\LocalLow\WildPastry"
