[Setup]
AppId={{0664896C-078B-4C7B-ABF5-5AB25C5D31C9}
AppName=Prism Crosshair
AppVersion={#PrismVersion}
AppPublisher=Prism Crosshair
AppPublisherURL=https://github.com/senta12531-hash/prism-crosshair
AppUpdatesURL=https://github.com/senta12531-hash/prism-crosshair/releases
DefaultDirName={localappdata}\Programs\Prism Crosshair
DefaultGroupName=Prism Crosshair
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\Prism.exe
SetupIconFile=Assets\Prism.ico
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
CloseApplicationsFilter=Prism.exe
RestartApplications=no
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
OutputDir=dist
OutputBaseFilename=Prism-Setup-win-x64
VersionInfoVersion={#PrismVersion}
VersionInfoDescription=Prism Crosshair Setup

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\portable\Prism.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "install-mode.txt"; DestDir: "{app}"; DestName: "Prism.install"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Prism Crosshair"; Filename: "{app}\Prism.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Prism Crosshair"; Filename: "{app}\Prism.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Prism.exe"; Description: "{cm:LaunchProgram,Prism Crosshair}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
  RunKey: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  RunKey := 'Software\Microsoft\Windows\CurrentVersion\Run';
  if RegQueryStringValue(HKCU, RunKey, 'Prism Crosshair', Command) and
     (CompareText(Command, ExpandConstant('"{app}\Prism.exe" --windows-startup')) = 0) then
    RegDeleteValue(HKCU, RunKey, 'Prism Crosshair');
end;
