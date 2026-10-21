#ifndef AppVer
 #define AppVer "0.3.3"
#endif
#ifndef PayloadDir
 #error Pass /DPayloadDir=<published application directory>
#endif
#define AppName "Aion 2 Soul Tracker"
#define AppExe "Aion2SoulTracker.exe"

[Setup]
AppId={{E305BC0E-8523-474A-8EAE-F8BDF2B61D44}
AppName={#AppName}
AppVersion={#AppVer}
AppVerName={#AppName} {#AppVer}
AppPublisher=Aion 2 Soul Tracker contributors
VersionInfoDescription={#AppName} Setup
DefaultDirName={localappdata}\Programs\Aion2SoulTracker
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputBaseFilename=Aion2SoulTracker-Setup-v{#AppVer}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern dark
WizardBackColor=#101823
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
#ifdef SignEnabled
SignTool=release
SignedUninstaller=yes
#endif

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ru.DesktopShortcut=Создать ярлык на рабочем столе
en.DesktopShortcut=Create a desktop shortcut
ru.NpcapTitle=Подготовка к чтению игры
en.NpcapTitle=Prepare game capture
ru.NpcapSubtitle=Для получения прогресса персонажа нужен драйвер Npcap.
en.NpcapSubtitle=The Npcap driver is required to read character progress.
ru.NpcapText=Npcap пока не найден.%n%n1. Откройте npcap.com кнопкой ниже и скачайте установщик.%n2. Установите Npcap с настройками по умолчанию. Windows может запросить права администратора.%n3. Вернитесь сюда и нажмите «Проверить снова».%n%nЕсли у вас уже работает DPS-метр, Npcap обычно установлен. Npcap распространяется отдельно по условиям его лицензии.
en.NpcapText=Npcap was not found.%n%n1. Open npcap.com below and download its installer.%n2. Install Npcap with default options. Windows may ask for administrator permission.%n3. Return here and click Check again.%n%nIf a DPS meter already works on this PC, Npcap is usually installed. Its license requires separate distribution.
ru.NpcapOpen=Открыть npcap.com
en.NpcapOpen=Open npcap.com
ru.NpcapRecheck=Проверить снова
en.NpcapRecheck=Check again
ru.NpcapFound=Npcap найден. Можно продолжать.
en.NpcapFound=Npcap found. You can continue.
ru.NpcapMissing=Npcap пока не установлен.
en.NpcapMissing=Npcap is not installed yet.
ru.NpcapContinue=Установить трекер сейчас?%n%nЕго можно открыть, но для чтения игры потребуется установить Npcap.
en.NpcapContinue=Install the tracker now?%n%nYou can open it, but game capture will require Npcap.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  NpcapPage: TWizardPage;
  NpcapStatus: TNewStaticText;

function NpcapInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{sys}\Npcap\wpcap.dll')) or FileExists(ExpandConstant('{sys}\wpcap.dll'));
end;

procedure UpdateNpcapStatus;
begin
  if NpcapInstalled then NpcapStatus.Caption := CustomMessage('NpcapFound')
  else NpcapStatus.Caption := CustomMessage('NpcapMissing');
end;

procedure OpenNpcapClick(Sender: TObject);
var ErrorCode: Integer;
begin
  ShellExec('open', 'https://npcap.com/#download', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure RecheckClick(Sender: TObject);
begin
  UpdateNpcapStatus;
end;

procedure InitializeWizard;
var Info: TNewStaticText;
    OpenButton, RecheckButton: TNewButton;
begin
  NpcapPage := CreateCustomPage(wpSelectTasks, CustomMessage('NpcapTitle'), CustomMessage('NpcapSubtitle'));
  Info := TNewStaticText.Create(NpcapPage);
  Info.Parent := NpcapPage.Surface;
  Info.AutoSize := False;
  Info.WordWrap := True;
  Info.Width := NpcapPage.SurfaceWidth;
  Info.Caption := CustomMessage('NpcapText');
  Info.AdjustHeight;
  OpenButton := TNewButton.Create(NpcapPage);
  OpenButton.Parent := NpcapPage.Surface;
  OpenButton.Caption := CustomMessage('NpcapOpen');
  OpenButton.Top := Info.Top + Info.Height + ScaleY(14);
  OpenButton.Width := ScaleX(160);
  OpenButton.Height := WizardForm.NextButton.Height;
  OpenButton.OnClick := @OpenNpcapClick;
  RecheckButton := TNewButton.Create(NpcapPage);
  RecheckButton.Parent := NpcapPage.Surface;
  RecheckButton.Caption := CustomMessage('NpcapRecheck');
  RecheckButton.Top := OpenButton.Top;
  RecheckButton.Left := OpenButton.Left + OpenButton.Width + ScaleX(10);
  RecheckButton.Width := ScaleX(145);
  RecheckButton.Height := OpenButton.Height;
  RecheckButton.OnClick := @RecheckClick;
  NpcapStatus := TNewStaticText.Create(NpcapPage);
  NpcapStatus.Parent := NpcapPage.Surface;
  NpcapStatus.Top := OpenButton.Top + OpenButton.Height + ScaleY(14);
  NpcapStatus.Font.Style := [fsBold];
  UpdateNpcapStatus;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (NpcapPage <> nil) and (PageID = NpcapPage.ID) and NpcapInstalled;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (NpcapPage <> nil) and (CurPageID = NpcapPage.ID) then UpdateNpcapStatus;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (NpcapPage <> nil) and (CurPageID = NpcapPage.ID) and not NpcapInstalled and not WizardSilent then
    Result := MsgBox(CustomMessage('NpcapContinue'), mbConfirmation, MB_YESNO) = IDYES;
end;
