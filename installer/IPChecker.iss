#define MyAppName "IP Checker"
#define MyAppVersion "1.1.1"
#define MyAppPublisher "IP Checker"
#define MyAppExeName "IPChecker.exe"
#define MyAppMutex "Global\IPChecker_SingleInstance_v1"
#define MyAppId "{{A8F3C2E1-9B4D-4F6A-8C1E-2D5E7F9A0B3C}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=IPChecker-Setup-{#MyAppVersion}-x64
OutputDir=..\dist
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\IPChecker\Assets\AppIcon.ico
VersionInfoVersion=1.1.1.0
InfoBeforeFile=before-install.txt
AppMutex={#MyAppMutex}
CloseApplications=force
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
ChangesAssociations=no

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加タスク:"; Flags: unchecked

[InstallDelete]
; 旧バージョンのルート直下 NetConfig（v1.0.9 以前）を除去
Type: files; Name: "{app}\IPChecker.NetConfig.exe"
Type: files; Name: "{app}\IPChecker.NetConfig.dll"
Type: files; Name: "{app}\IPChecker.NetConfig.deps.json"
Type: files; Name: "{app}\IPChecker.NetConfig.runtimeconfig.json"
; v1.0.10+ の NetConfig サブフォルダ（v1.1.1 以降は同梱しない）
Type: filesandordirs; Name: "{app}\NetConfig"

[Files]
Source: "..\dist\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\redist\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\アンインストール {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Visual C++ ランタイムをセットアップしています..."; Flags: waituntilterminated; Check: VCRedistNeedsInstall
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function VCRedistNeedsInstall: Boolean;
var
  Version: String;
begin
  if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Version', Version) then
  begin
    Result := True;
    Exit;
  end;
  Result := CompareStr(Version, 'v14.30.30704') < 0;
end;

procedure StopRunningApp;
var
  ResultCode: Integer;
begin
  Exec('taskkill', '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1500);
end;

function InitializeSetup: Boolean;
begin
  StopRunningApp;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  StopRunningApp;
end;

function InitializeUninstall: Boolean;
begin
  StopRunningApp;
  Result := True;
end;
