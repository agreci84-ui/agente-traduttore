# Traduttore dal vivo per Mac

La versione per macOS di [Traduttore dal vivo](../traduttore-live/README.md): **traduce in tempo reale le voci di
una riunione online** (Teams, Zoom, Google Meet, Webex o qualsiasi altra app) e le mostra come sottotitoli in una
finestra sempre in primo piano.

Fa le stesse cose della versione per Windows e ne usa lo stesso codice per trascrizione, traduzione e divisione in
frasi: servizi di traduzione, modelli di Whisper, impostazioni e trascrizioni salvate funzionano allo stesso modo
(vedi il [README di Traduttore dal vivo](../traduttore-live/README.md)). Qui sotto ci sono solo le differenze.

```
audio del Mac ──► frasi ──► trascrizione (Whisper, sul Mac) ──► traduzione (AI integrata, ──► sottotitoli
 (Core Audio)     (pause)    l'audio non esce mai dal Mac        sul Mac, gratis)            + file di trascrizione
```

## Requisiti

- **macOS 14.2 (Sonoma) o successivo**, su Mac con Apple Silicon o con processore Intel.
- **8 GB di memoria** e **3 GB liberi sul disco** per l'AI di traduzione integrata.
- **Internet** alla prima partenza, per scaricare il modello di trascrizione (190 MB con l'impostazione predefinita)
  e l'AI integrata (2,5 GB), e durante l'uso per i servizi di traduzione online, se li accetti.

La traduzione la fa l'**AI integrata** (TranslateGemma di Google, dentro il programma): gratis, senza limiti, senza
account né chiavi, e il testo non lascia il Mac. La prima volta si scarica da sola; intanto, se accetti i servizi
online, traduce MyMemory. Vedi [Servizi di traduzione](../traduttore-live/README.md#servizi-di-traduzione).

Sui Mac con **Apple Silicon** Whisper e l'AI integrata usano la scheda grafica (Metal) e sono veloci. Sui Mac con
**processore Intel** lavora solo il processore: ogni frase richiede da 2 a 4 secondi per la traduzione, che compare
man mano. Se i sottotitoli restano indietro, scegli il modello di trascrizione *base* dal menu › *Precisione della
trascrizione*; se l'AI integrata non sta al passo, il programma passa da solo ai servizi online.

## Creare e installare l'app

Servono il [.NET SDK 10](https://dotnet.microsoft.com/download) (o successivo) e gli strumenti per sviluppatori di
Apple, che contengono il compilatore Swift: `xcode-select --install`. Poi, dalla cartella `traduttore-live-mac`:

```bash
./crea-app.sh            # per il Mac su cui lo esegui
./crea-app.sh arm64      # per i Mac con Apple Silicon
./crea-app.sh x86_64     # per i Mac con processore Intel
```

Il risultato è `out/Traduttore dal vivo.app` (circa 120 MB, runtime .NET incluso): trascinala in **Applicazioni**.
Per provare il programma durante lo sviluppo basta `dotnet run --project traduttore-live-mac`; in questo caso il
permesso di registrare l'audio lo chiede il Terminale (o l'editor) da cui lo avvii.

L'app è firmata solo localmente («ad hoc»), senza un certificato di Apple. Sul Mac dove l'hai creata si apre
normalmente; copiata su **un altro Mac**, la prima volta macOS la blocca perché non viene da uno sviluppatore
identificato: aprila con **clic destro › Apri** (o da *Impostazioni di Sistema › Privacy e sicurezza › Apri
comunque*). Per distribuirla senza avvisi servono un account Apple Developer, la firma con un certificato
*Developer ID* e la notarizzazione.

## Primo avvio: i permessi di macOS

macOS chiede tre conferme, una volta sola:

1. **Servizi di traduzione online**: la domanda del programma, come su Windows. Puoi cambiare idea dal
   menu › *Traduzione*.
2. **Registrazione dell'audio di sistema**: serve per sentire le voci della riunione. Se lo neghi non compare
   nessun sottotitolo; per darlo in seguito: menu › *Permesso di registrare l'audio…*, oppure *Impostazioni di
   Sistema › Privacy e sicurezza › Registrazione schermo e audio di sistema*, sezione *Solo registrazione audio di
   sistema*. Il programma ascolta solo l'audio, non lo schermo.
3. **Cartella Documenti**: per salvare le trascrizioni delle riunioni in `Documenti/Traduttore dal vivo`.

## Come si usa

Come su Windows (vedi [Come si usa](../traduttore-live/README.md#come-si-usa)), con queste differenze:

- **⌘ + rotellina** (o Ctrl + rotellina) ingrandisce o rimpicciolisce il testo;
- il menu si apre con **⋯**, con il clic destro (o Ctrl + clic) sulla finestra, oppure dalla barra dei menu ›
  *Traduttore dal vivo › Menu dei sottotitoli*; **⌘ ,** apre i servizi di traduzione, **⌘ Q** chiude;
- la finestra resta visibile su tutte le scrivanie (Spaces);
- **Cosa ascoltare** propone *Tutto l'audio del Mac* (predefinito: tutte le app, qualunque sia l'uscita audio)
  oppure *Solo l'audio di un'app*: l'elenco mostra le app collegate all'audio. Si ascolta tutta l'app, compresi i
  suoi processi secondari (Teams e Chrome riproducono l'audio in processi a parte); se l'app è aperta ma la
  riunione non è ancora iniziata, il programma comincia ad ascoltare appena l'app usa l'audio.

## Dove sono i file

| Cosa | Dove |
|---|---|
| Impostazioni (`impostazioni.json`) | `~/Library/Application Support/TraduttoreLive` (menu › *Apri la cartella delle impostazioni*) |
| Modelli di trascrizione, rilevatore di voce e AI integrata (2,5 GB) | `~/Library/Application Support/TraduttoreLive/modelli` |
| Ultimo errore dell'AI integrata, per l'assistenza | `~/Library/Application Support/TraduttoreLive/ai-integrata.log` |
| Chiavi ed email dei servizi | Portachiavi, voce «Traduttore dal vivo» (app *Accesso Portachiavi*) |
| Trascrizioni delle riunioni | `Documenti/Traduttore dal vivo` |

Le impostazioni sono le stesse della versione per Windows (vedi
[Impostazioni avanzate](../traduttore-live/README.md#impostazioni-avanzate)), tranne `DispositivoId`, che sul Mac non
si usa. Per **disinstallare** trascina l'app nel Cestino e cancella la cartella `TraduttoreLive` in
`~/Library/Application Support` e la voce «Traduttore dal vivo» nel Portachiavi.

## Se qualcosa non va

| Problema | Cosa fare |
|---|---|
| Non compare nessun sottotitolo | Controlla il permesso di registrare l'audio (menu › *Permesso di registrare l'audio…*): Traduttore dal vivo deve essere attivo. Dopo averlo dato, riavvia il programma. |
| «Audio interrotto (è cambiata l'uscita audio): riprovo…» | Hai collegato o scollegato cuffie o altoparlanti: il programma si ricollega da solo dopo un paio di secondi. |
| «In attesa che … sia avviato» | L'app scelta in *Cosa ascoltare* non è aperta. Si collega da solo appena la apri. |
| Le riunioni nel browser Safari non compaiono tra le app | Safari riproduce l'audio con un servizio di sistema: scegli *Tutto l'audio del Mac*. |
| La riga in alto dice «AI integrata: la sto scaricando» | È il primo avvio: l'AI integrata (2,5 GB) si scarica in background e intanto traducono i servizi online. Quando è pronta compare un avviso giallo. |
| Le traduzioni sono poco naturali | Controlla nel menu › *Traduzione* che sia scelta la modalità *Automatica* (o *Solo l'AI integrata*) e che *Usa l'AI integrata* sia attivo. |
| «Ollama non è avviato o non è installato» | Apri Ollama dalla cartella Applicazioni (poi resta attivo nella barra dei menu) o installalo. |
| macOS dice che l'app non si può aprire | Vedi [Creare e installare l'app](#creare-e-installare-lapp): clic destro › Apri. |

Gli altri problemi sono gli stessi della versione per Windows: vedi
[Se qualcosa non va](../traduttore-live/README.md#se-qualcosa-non-va).

## Come è fatto

| File | Contenuto |
|---|---|
| [cattura-audio/main.swift](cattura-audio/main.swift) | Programma in Swift che ascolta l'audio in uscita con i *process tap* di Core Audio (tutto l'audio o una sola app) e lo manda al programma come float mono a 16 kHz. |
| [Audio/CatturaAudio.cs](Audio/CatturaAudio.cs) | Avvia `cattura-audio` e ne riceve i campioni. |
| [Audio/Programmi.cs](Audio/Programmi.cs) | Elenco delle app collegate all'audio e del loro processo principale. |
| [Traduzione/Credenziali.cs](Traduzione/Credenziali.cs) | Chiavi ed email nel Portachiavi. |
| [MainWindow.axaml](MainWindow.axaml), [FinestraServizi.axaml](FinestraServizi.axaml) | Le finestre, con [Avalonia](https://avaloniaui.net). |
| [FinestraMac.cs](FinestraMac.cs) | La finestra su tutte le scrivanie. |
| [crea-app.sh](crea-app.sh), [Info.plist](Info.plist) | Creazione del pacchetto `.app`. |

Il resto (trascrizione, traduzione, divisione in frasi, impostazioni) è il codice della cartella
[traduttore-live](../traduttore-live), incluso nel progetto [TraduttoreLiveMac.csproj](TraduttoreLiveMac.csproj).
