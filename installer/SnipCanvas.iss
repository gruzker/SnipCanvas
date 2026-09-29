#ifndef ReleaseDir
  #define ReleaseDir "..\dist"
#endif
#define AppVersion GetVersionNumbersString(ReleaseDir + "\SnipCanvas.exe")

[Setup]
AppId={{E6488497-589B-44F1-BBBC-F6A2E58193E8}
AppName=SnipCanvas
AppVersion={#AppVersion}
AppVerName=SnipCanvas {#AppVersion}
DefaultDirName={localappdata}\Programs\SnipCanvas
DefaultGroupName=SnipCanvas
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
UninstallDisplayIcon={app}\SnipCanvas.exe
SetupIconFile=..\assets\snipcanvas.ico
WizardStyle=modern
WizardImageFile=wizard-large.bmp,wizard-large-2x.bmp
WizardSmallImageFile=wizard-small.bmp,wizard-small-2x.bmp
DisableWelcomePage=no
Compression=lzma2
SolidCompression=yes
OutputDir={#ReleaseDir}
OutputBaseFilename=SnipCanvas-Setup
; Makes Setup tell Windows to refresh its icon cache, so an updated icon shows on the taskbar and Start menu straight away.
ChangesAssociations=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayName=SnipCanvas
VersionInfoVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#ReleaseDir}\SnipCanvas.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\QUICKSTART.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "{#ReleaseDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\licenses\*.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\SnipCanvas"; Filename: "{app}\SnipCanvas.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\SnipCanvas"; Filename: "{app}\SnipCanvas.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SnipCanvas.exe"; Description: "Open SnipCanvas"; Flags: nowait postinstall skipifsilent unchecked

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SnipCanvas', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\SnipCanvas.exe') + '" --tray') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SnipCanvas');
end;

procedure InitializeWizard();
begin
  WizardForm.WelcomeLabel2.Caption := 'Capture, annotate, and share screenshots with SnipCanvas.' + #13#10 + #13#10 +
    'Before updating, choose Quit SnipCanvas from the system tray so your current screenshot is saved.';
end;
