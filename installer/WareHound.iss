; WareHound Installer Script for Inno Setup
; Requires Inno Setup 6.x or later

#define MyAppName "WareHound"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WareHound Team"
#define MyAppURL "https://github.com/YOUR-USERNAME/WareHound"
#define MyAppExeName "WareHound.UI.exe"

[Setup]
; Application information
AppId={{A1B2C3D4-5678-9ABC-DEF0-123456789ABC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Output settings
OutputDir=..\installer-output
OutputBaseFilename=WareHound-Setup-{#MyAppVersion}
; SetupIconFile=..\WareHound.UI\Resources\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

; Compatibility
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files
Source: "..\WareHound.UI\bin\x64\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Native sniffer DLL
Source: "..\bin\Release\WareHound.Sniffer.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch app after installation (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
var
  NpcapPage: TInputOptionWizardPage;
  DotNetPage: TOutputMsgWizardPage;

function IsDotNet8Installed: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsNpcapInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{sys}\npcap.dll')) or FileExists(ExpandConstant('{sys}\wpcap.dll'));
end;

procedure InitializeWizard;
begin
  // Create .NET Runtime check page
  DotNetPage := CreateOutputMsgPage(wpWelcome,
    '.NET Runtime Check',
    'Checking for required .NET 8.0 Desktop Runtime...',
    '');
    
  if IsDotNet8Installed then
    DotNetPage.Msg1Label.Caption := '[OK] .NET 8.0 Runtime is installed.'
  else
    DotNetPage.Msg1Label.Caption := '[WARNING] .NET 8.0 Desktop Runtime not found!' + #13#10 + #13#10 +
      'Please download and install from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 + #13#10 +
      'Select ".NET Desktop Runtime" for Windows x64.';

  // Create Npcap installation option page
  NpcapPage := CreateInputOptionPage(DotNetPage.ID,
    'Packet Capture Driver',
    'Npcap is required for network packet capture.',
    'WareHound requires Npcap (or WinPcap) to capture network packets.' + #13#10 + #13#10 +
    'Current status: ' + 
    IfThen(IsNpcapInstalled, '[OK] Packet capture driver detected.', '[MISSING] No packet capture driver found.'),
    True, False);
    
  if not IsNpcapInstalled then
  begin
    NpcapPage.Add('Download and install Npcap after setup completes');
    NpcapPage.Add('I will install Npcap manually later');
    NpcapPage.SelectedValueIndex := 0;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Open Npcap download page if user selected to install
    if (not IsNpcapInstalled) and (NpcapPage.SelectedValueIndex = 0) then
    begin
      ShellExec('open', 'https://npcap.com/#download', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
  end;
end;

function IfThen(Condition: Boolean; TrueValue, FalseValue: String): String;
begin
  if Condition then
    Result := TrueValue
  else
    Result := FalseValue;
end;
