; DevSuite - Inno Setup script. Installs the Hub and CacheSweep for the current user.
; Build:  tools\build.ps1, then ISCC.exe installer\DevSuite.iss   (CI passes /DAppVer=x.y.z)
#ifndef AppVer
  #define AppVer "0.9.0"
#endif
#define AppName   "DevSuite"
#define Publisher "Enes Aksu"
#define SiteUrl   "https://github.com/Zeatrex01/cachesweeper"

[Setup]
AppId={{7C3E5A92-41B6-4D0F-9E28-6A1F0D3B7C54}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher={#Publisher}
AppPublisherURL={#SiteUrl}
AppSupportURL={#SiteUrl}/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
PrivilegesRequired=lowest
OutputDir=..\dist-installer
OutputBaseFilename=DevSuite-{#AppVer}-setup
SetupIconFile=..\assets\DevSuite.ico
UninstallDisplayIcon={app}\DevSuiteHub.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0

; The installer UI follows the Windows language. The app is English unless the
; language pack component is selected.
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Types]
Name: "standard"; Description: "DevSuite"
Name: "custom";   Description: "Choose components"; Flags: iscustom

[Components]
Name: "app"; Description: "DevSuite Hub and CacheSweep";       Types: standard custom; Flags: fixed
Name: "tr";  Description: "Turkish language pack (lang.json)"; Types: custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\*.exe";   DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\lang\tr.json"; DestDir: "{app}"; DestName: "lang.json"; Components: tr; Flags: ignoreversion
Source: "..\README.md";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";      DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";   Filename: "{app}\DevSuiteHub.exe"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DevSuiteHub.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DevSuiteHub.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent unchecked

; User settings (%APPDATA%\CacheSweep) are deliberately left in place on uninstall.
