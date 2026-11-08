; Nemoviz CC — the installer, after the author's Nemoviz HiFi Rack and Book Reader scripts.
;
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\NemovizCC.iss
;
; build-release.cmd runs it after `dotnet publish` (self-contained single file) into
; dist\NemovizCC; this script packages that folder and nothing else.

#define AppName      "Nemoviz CC"
#define AppVersion   "0.1.1"
#define AppVersionNo "0.1.1.0"
; The channel: empty gives "Nemoviz CC" everywhere; " Beta" would give the beta channel
; (no build numbers in installed names — they would become folder names).
#define AppChannel   ""
#define AppInstall   AppName + AppChannel
#define AppPublisher "Nemoguća vizija"
#define AppUrl       "https://github.com/gradic76/Nemoviz-CC"
#define AppExe       "NemovizCC.exe"
#define SourceDir    "..\dist\NemovizCC"

[Setup]
AppId={{6E4D2A9C-8B1F-4C3E-9A7D-2F5B0C1E8D43}
AppName={#AppInstall}
AppVersion={#AppVersion}
AppVerName={#AppInstall}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersionNo}
VersionInfoProductName={#AppInstall}
; Program Files, where a Windows program belongs. Nemoviz CC writes nothing beside the exe
; (settings, state and attachments are in %APPDATA%\ClaudeChat), so it works there; the user
; may still choose a per-user install — Inno asks for elevation only when the folder needs it.
DefaultDirName={autopf}\{#AppInstall}
DefaultGroupName={#AppInstall}
DisableProgramGroupPage=yes
PrivilegesRequiredOverridesAllowed=dialog
LicenseFile=..\LICENSE.txt
SetupIconFile=..\ClaudeChat\NemovizCC.ico
OutputDir=..\dist
OutputBaseFilename=NemovizCC-Setup
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppInstall}
Compression=lzma2/max
SolidCompression=yes
; x64 only: the bundled .NET and nvdaControllerClient.dll are 64-bit.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
; Croatian and both Serbians are Inno's unofficial translations, vendored (the author's
; reviewed copies) so the script compiles on any machine with Inno on it.
Name: "english";    MessagesFile: "compiler:Default.isl"
Name: "croatian";   MessagesFile: "jezici\Croatian.isl"
Name: "serbian";    MessagesFile: "jezici\SerbianLatin.isl"
Name: "serbiancyr"; MessagesFile: "jezici\SerbianCyrillic.isl"
Name: "slovenian";  MessagesFile: "compiler:Languages\Slovenian.isl"
Name: "german";     MessagesFile: "compiler:Languages\German.isl"
Name: "italian";    MessagesFile: "compiler:Languages\Italian.isl"
Name: "spanish";    MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french";     MessagesFile: "compiler:Languages\French.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "swedish";    MessagesFile: "compiler:Languages\Swedish.isl"
Name: "norwegian";  MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "finnish";    MessagesFile: "compiler:Languages\Finnish.isl"
Name: "danish";     MessagesFile: "compiler:Languages\Danish.isl"

[CustomMessages]
; An accessibility fix, not a translation: Inno's CreateDesktopIcon carries an accelerator '&'
; that the task check list draws as an underline but hands to the screen reader as a character,
; and the accelerator is never offered anyway. Without it the box is still reached with Tab.
english.CreateDesktopIcon=Create a desktop shortcut
croatian.CreateDesktopIcon=Stvori prečac na radnoj površini
serbian.CreateDesktopIcon=Stvori prečac na radnoj površini
serbiancyr.CreateDesktopIcon=Створи пречац на радној површини
slovenian.CreateDesktopIcon=Ustvari ikono na namizju
german.CreateDesktopIcon=Desktop-Symbol erstellen
italian.CreateDesktopIcon=Crea un'icona sul desktop
spanish.CreateDesktopIcon=Crear un acceso directo en el escritorio
french.CreateDesktopIcon=Créer une icône sur le Bureau
portuguese.CreateDesktopIcon=Criar atalho no Ambiente de Trabalho
swedish.CreateDesktopIcon=Skapa en genväg på skrivbordet
norwegian.CreateDesktopIcon=Lag ikon på skrivebordet
finnish.CreateDesktopIcon=Luo kuvake työpöydälle
danish.CreateDesktopIcon=Opret ikon på skrivebordet

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Everything in the publish folder except what belongs to a user. settings.json beside the exe
; is the PORTABLE build's settings (the zip adds it); state.json and attachments would be a
; developer's. Each name is checked and an error stops the build rather than shipping it.
#define Guard(str N) \
    (FileExists(SourceDir + "\" + N) || DirExists(SourceDir + "\" + N)) \
      ? Error("This belongs to the user, not to the program, and it is in the publish folder: " + N) \
      : N
#define UserOwned \
      Guard("settings.json") + "," + Guard("state.json") + "," + Guard("attachments") \
      + "," + Guard("restart.request")
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.tmp,{#UserOwned}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppInstall}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#AppInstall}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppInstall}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppInstall}}"; \
    Flags: nowait postinstall skipifsilent runasoriginaluser
; A silent update (the program runs setup with /relaunch=1) opens the program again by itself.
Filename: "{app}\{#AppExe}"; Flags: nowait postinstall runasoriginaluser; Check: RelaunchRequested

; Nothing of the user's is deleted on uninstall: settings and state are in %APPDATA%\ClaudeChat,
; conversations in %USERPROFILE%\.claude, projects wherever the user keeps them.

[Code]
function RelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:relaunch|0}') = '1';
end;
