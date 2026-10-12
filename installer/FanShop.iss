; Build from the exact verified publish directory used for the auto-update ZIP.
#ifndef AppVersion
  #error AppVersion must be passed by CI
#endif
#ifndef PublishDir
  #error PublishDir must be passed by CI
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir must be passed by CI
#endif

[Setup]
AppId={{A40B9BC8-3A17-4C42-A5FC-4ED54ADFE784}
AppName=FanShop
AppVersion={#AppVersion}
AppVerName=FanShop {#AppVersion}
AppPublisher=FanShop
AppPublisherURL=https://github.com/kihort-si/FanShop
AppSupportURL=https://github.com/kihort-si/FanShop/issues
AppUpdatesURL=https://github.com/kihort-si/FanShop/releases
DefaultDirName={localappdata}\Programs\FanShop
DefaultGroupName=FanShop
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#InstallerOutputDir}
OutputBaseFilename=FanShopSetup-{#AppVersion}
SetupIconFile=..\icon.ico
UninstallDisplayIcon={app}\FanShop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\FanShop\FanShop"; Filename: "{app}\FanShop.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\FanShop"; Filename: "{app}\FanShop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\FanShop.exe"; Description: "{cm:LaunchProgram,FanShop}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

; No blanket deletion of {app} or AppData: personal files and the database survive uninstall.
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\.fanshop-update-journal.json')) then
    Result := 'FanShop: сначала завершите восстановление незавершённого обновления и повторите установку.';
end;
