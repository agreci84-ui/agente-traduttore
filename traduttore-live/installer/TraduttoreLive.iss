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
; Nome e versione del prodotto: la firma con SignPath li controlla (vedi .signpath nella radice del repository).
VersionInfoProductVersion={#Versione}
VersionInfoProductTextVersion={#Versione}
VersionInfoDescription=Installazione di {#Nome}
VersionInfoCopyright=Copyright (c) 2026 agreci84-ui. Licenza MIT.
AppPublisherURL=https://github.com/agreci84-ui/agente-traduttore
AppSupportURL=https://github.com/agreci84-ui/agente-traduttore/issues
AppUpdatesURL=https://github.com/agreci84-ui/agente-traduttore/releases

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
; InfoBeforeFile: l'informativa sulla privacy, mostrata prima di installare.
Name: "italiano"; MessagesFile: "compiler:Languages\Italian.isl"; InfoBeforeFile: "privacy-it.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "privacy-en.txt"

[CustomMessages]
italiano.Privacy=Privacy:
italiano.ServiziOnline=Usa i servizi di traduzione online gratuiti (ricevono il testo trascritto, non l'audio: MyMemory e, se li configuri, DeepL o Microsoft Translator). Senza, si traduce solo sul PC con TranslateGemma.
english.Privacy=Privacy:
english.ServiziOnline=Use the free online translation services (they receive the transcribed text, not the audio: MyMemory and, if you set them up, DeepL or Microsoft Translator). Without them, translation runs only on the PC with TranslateGemma.

[Tasks]
Name: "serviziOnline"; Description: "{cm:ServiziOnline}"; GroupDescription: "{cm:Privacy}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Registry]
; La scelta sui servizi online: il programma la legge al primo avvio dopo l'installazione e poi la toglie
; (da lì in avanti vale quella del menu Traduzione).
Root: HKCU; Subkey: "Software\TraduttoreLive"; ValueType: dword; ValueName: "ServiziOnline"; ValueData: 1; Tasks: serviziOnline; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\TraduttoreLive"; ValueType: dword; ValueName: "ServiziOnline"; ValueData: 0; Tasks: not serviziOnline; Flags: uninsdeletekey

[InstallDelete]
; Le librerie di Whisper cambiano da una versione all'altra: si tolgono quelle vecchie prima di copiare le nuove.
Type: filesandordirs; Name: "{app}\runtimes"

[Files]
Source: "{#Pubblicazione}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#Nome}"; Filename: "{app}\{#Eseguibile}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Eseguibile}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#Eseguibile}"; Description: "{cm:LaunchProgram,{#Nome}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Si tolgono i modelli di trascrizione scaricati (centinaia di MB) e le chiavi dei servizi di traduzione.
; Restano le impostazioni e, in Documenti, le trascrizioni delle riunioni. Il modello TranslateGemma è di Ollama.
Type: filesandordirs; Name: "{localappdata}\TraduttoreLive\modelli"
Type: dirifempty; Name: "{localappdata}\TraduttoreLive"
Type: files; Name: "{userappdata}\TraduttoreLive\credenziali.dat"
Type: files; Name: "{userappdata}\TraduttoreLive\chiave-api.dat"
