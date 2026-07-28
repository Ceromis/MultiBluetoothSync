; MultiBluetoothSync 安装脚本
; Inno Setup 6

#define MyAppName "MultiBluetoothSync"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ceromis"
#define MyAppExeName "MultiBluetoothSync.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\output
OutputBaseFilename=MultiBluetoothSync_Setup
SetupIconFile=app_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=多蓝牙耳机协同工具
VersionInfoProductName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"
Name: "autostart"; Description: "开机自动启动"; GroupDescription: "附加选项:"

[Files]
Source: "app\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "vbcable\*"; DestDir: "{app}\vbcable"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MultiBluetoothSync"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\vbcable\VBCABLE_Setup_x64.exe"; Description: "安装 VB-Audio Virtual Cable 虚拟音频驱动"; StatusMsg: "正在安装 VB-Audio Virtual Cable..."; Flags: shellexec waituntilterminated runascurrentuser
Filename: "{app}\{#MyAppExeName}"; Description: "启动 MultiBluetoothSync"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM {#MyAppExeName} >nul 2>&1"; Flags: runhidden; RunOnceId: "killapp"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Messages]
SetupAppTitle=MultiBluetoothSync 安装程序
SetupWizardTitle=MultiBluetoothSync 安装向导

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
  begin
    MsgBox('安装完成！' + #13#10 + #13#10 +
           '请立即重启电脑。' + #13#10 +
           '重启后，将 Windows 播放设备设为 "CABLE Input"，' + #13#10 +
           '然后启动 MultiBluetoothSync 选择蓝牙耳机即可使用。',
           mbInformation, MB_OK);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
