; Installer di Traduttore dal vivo per Windows (Inno Setup 6).
; Non compilarlo a mano: usa crea-installer.ps1, che prima pubblica il programma e poi passa
; versione e cartella dei file a questo script.

#ifndef Versione
  #define Versione "1.0.0"
#endif
#ifndef Pubblicazione
  #define Pubblicazione "out\publish"
#endif

#define Nome "Traduttore dal vivo"
#define Eseguibile "TraduttoreLive.exe"

[Setup]
; AppId identifica il programma per aggiornamenti e disinstallazione: non cambiarlo mai.
AppId={{8F88F7B5-312F-4302-A601-F0F94FEF4BC4}
AppName={#Nome}
AppVersion={#Versione}
AppVerName={#Nome} {#Versione}
VersionInfoVersion={#Versione}
VersionInfoProductName={#Nome}
VersionInfoDescription=Installazione di {#Nome}

; Installazione per l'utente corrente: non servono i diritti di amministratore,
; quindi funziona anche sui PC aziendali senza accesso amministrativo.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#Nome}
DisableProgramGroupPage=yes
UsedUserAreasWarning=no

; Windows 10 1809 o successivo, a 64 bit (sui PC ARM gira in emulazione x64).
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Se il programma è aperto durante un aggiornamento, l'installer lo chiude.
CloseApplications=yes
RestartApplications=no

WizardStyle=modern
SetupIconFile=..\icona.ico
UninstallDisplayIcon={app}\{#Eseguibile}
UninstallDisplayName={#Nome}

OutputDir=out
OutputBaseFilename=TraduttoreLive-Setup-{#Versione}
Compression=lzma2/max
SolidCompression=yes

ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

#ifdef Firma
; Firma dell'installer e del programma di disinstallazione. Il comando "firma" lo passa crea-installer.ps1
; (parametro /S di ISCC); i file del programma sono già firmati prima di arrivare qui.
SignTool=firma
SignedUninstaller=yes
#endif

[Languages]
Name: "italiano"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Le librerie di Whisper cambiano da una versione all'altra: si tolgono quelle vecchie prima di copiare le nuove.
Type: filesandordirs; Name: "{app}\runtimes"

[Files]
Source: "{#Pubblicazione}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#Nome}"; Filename: "{app}\{#Eseguibile}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Eseguibile}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#Eseguibile}"; Description: "{cm:LaunchProgram,{#Nome}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Si tolgono i modelli scaricati (centinaia di MB) e la chiave API salvata.
; Restano le impostazioni e, in Documenti, le trascrizioni delle riunioni.
Type: filesandordirs; Name: "{localappdata}\TraduttoreLive\modelli"
Type: dirifempty; Name: "{localappdata}\TraduttoreLive"
Type: files; Name: "{userappdata}\TraduttoreLive\chiave-api.dat"
