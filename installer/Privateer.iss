#define MyAppName "Privateer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Privateer"
#define MyAppExeName "Privateer.Desktop.exe"
#define MyPublishDir "..\artifacts\releases\1.0.0\installer\publish"
#define MyOutputDir "..\artifacts\releases\1.0.0\installer"

[Setup]
AppId={{1A3F6D5E-492D-4E33-9C97-85E452D6B0C4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/Fido4/Privateer
AppSupportURL=https://github.com/Fido4/Privateer
AppUpdatesURL=https://github.com/Fido4/Privateer/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#MyOutputDir}
OutputBaseFilename=PrivateerSetup-1.0.0
SetupIconFile=..\csharp\Privateer.Desktop\Assets\PrivateerAppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
