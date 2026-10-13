; Inno Setup script for the WareHound Avalonia frontend.
; Built in CI with:
;   ISCC.exe /DAppVersion=1.2.3 /DSourceDir=<publish dir> /DOutputDir=<out dir> WareHound.Avalonia.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\installer-output"
#endif

#define AppName "WareHound (Avalonia)"
#define AppExe "WareHound.Avalonia.exe"

[Setup]
AppId={{6F3B2A41-9C7E-4D1A-B8E2-5A0C4F7D9E13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WareHound
DefaultDirName={autopf}\WareHound Avalonia
DefaultGroupName=WareHound
OutputDir={#OutputDir}
OutputBaseFilename=WareHound-Avalonia-Setup-{#AppVersion}-win-x64
LicenseFile=..\License.rtf
SetupIconFile=..\..\WareHound.Avalonia\Assets\avalonia-logo.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "https://npcap.com/#download"; Description: "Download Npcap (required for packet capture)"; Flags: shellexec postinstall skipifsilent unchecked; Check: not NpcapInstalled
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
function NpcapInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{sys}\Npcap\wpcap.dll')) or
            FileExists(ExpandConstant('{sys}\wpcap.dll'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not NpcapInstalled) and (not WizardSilent) then
    MsgBox('Npcap was not found. WareHound needs Npcap (WinPcap API-compatible mode) to capture packets.' + #13#10 +
           'Download it from https://npcap.com/', mbInformation, MB_OK);
end;
