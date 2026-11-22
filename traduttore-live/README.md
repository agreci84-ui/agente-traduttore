# Traduttore dal vivo

Un programma per Windows che **traduce in tempo reale le voci di una riunione online** (Teams, Zoom, Google Meet,
Webex o qualsiasi altro programma) e le mostra come sottotitoli in una finestra sempre in primo piano.

Ascolta quello che esce dalle casse o dalle cuffie, quindi sente **gli altri partecipanti**, non il tuo microfono, e
funziona con qualsiasi programma senza bisogno di installare niente al suo interno.

```
audio del PC ──► frasi ──► trascrizione (Whisper, sul PC) ──► traduzione (Claude) ──► sottotitoli
  (WASAPI)       (pause)    l'audio non esce mai dal PC        riceve solo il testo     + file di trascrizione
```

- **Trascrizione sul PC** con [Whisper](https://github.com/ggml-org/whisper.cpp): l'audio della riunione non viene mai
  inviato a nessuno. Prima di trascrivere, un rilevatore di voce (Silero VAD) scarta musica, rumori e silenzi.
- **Traduzione con Claude**, che riceve solo il testo trascritto e le ultime frasi come contesto, così pronomi,
  termini tecnici e frasi spezzate vengono tradotti in modo coerente. La traduzione compare mentre viene scritta.
- **Lingua riconosciuta frase per frase**: se in riunione si parlano più lingue, ogni frase viene tradotta dalla sua.
  Le frasi già nella lingua dei sottotitoli non vengono tradotte.
- **Trascrizione salvata** in `Documenti\Traduttore dal vivo\Riunione <data e ora>.txt`, con l'originale e la
  traduzione di ogni frase.

## Requisiti

- **Windows 10 (versione 1809 o successiva) o Windows 11, a 64 bit.** Per ascoltare *un solo programma* serve
  Windows 10 versione 2004 o successiva; con le versioni precedenti si può ascoltare tutto l'audio del PC.
- **Una chiave API di Claude**, da creare su [platform.claude.com](https://platform.claude.com/settings/keys). L'API
  ha una fatturazione a consumo, separata dagli abbonamenti a Claude (Pro, Max, Team): l'abbonamento non include
  l'API. Senza chiave il programma mostra solo la trascrizione nella lingua originale (verso l'inglese può tradurre
  da solo con Whisper, con una qualità inferiore).
- **Internet** alla prima partenza, per scaricare il modello di trascrizione (190 MB con l'impostazione predefinita),
  e durante l'uso per la traduzione.

Il programma esiste solo per Windows: la versione per Mac non c'è ancora.

## Installazione

1. **Esegui `TraduttoreLive-Setup-<versione>.exe`** (vedi [Creare l'installer](#creare-linstaller)). Non servono i
   diritti di amministratore: il programma si installa solo per l'utente corrente, in
   `%LOCALAPPDATA%\Programs\Traduttore dal vivo`, con il collegamento nel menu Start e, se vuoi, sul desktop. Il
   runtime .NET è incluso: non serve installare altro.
2. **Al primo avvio incolla la chiave API di Claude** nella finestra che compare. Puoi cambiarla o toglierla in
   seguito dal menu › *Chiave API di Claude…*. La chiave resta sul PC, cifrata con l'account di Windows.

Per **aggiornare** esegui l'installer della versione nuova: le impostazioni e la chiave restano. Per
**disinstallare** usa Impostazioni › App › App installate › *Traduttore dal vivo*. La disinstallazione toglie anche
i modelli scaricati e la chiave API; lascia le impostazioni e, in `Documenti`, le trascrizioni delle riunioni.

### Avvisi di Windows sugli altri PC

L'installer non è firmato con un certificato di firma del codice, quindi Windows non lo conosce:

- se il file arriva **da internet** (download, allegato email, Teams), compare la schermata blu *«PC protetto da
  Windows»*: clicca **Ulteriori informazioni › Esegui comunque**. Copiato da una chiavetta USB o da una cartella di
  rete, di solito l'avviso non compare;
- se sul PC è attivo **Smart App Control** (Sicurezza di Windows › Controllo app e browser), Windows può bloccare i
  file non firmati senza possibilità di eseguirli comunque. Sul PC di sviluppo, con Smart App Control attivo,
  installazione e programma sono partiti, ma il programma di disinstallazione è stato bloccato. Chi ha Smart App
  Control attivo può disattivarlo dalla stessa pagina; sugli aggiornamenti recenti di Windows 11 si può riattivare in
  seguito senza reinstallare Windows;
- sui **PC aziendali** l'installazione di programmi non firmati può essere vietata dalle regole dell'azienda:
  chiedi al reparto IT.

La soluzione definitiva è **firmare** installer e programma con un certificato rilasciato da un'autorità
riconosciuta. Con il servizio [Artifact Signing di Microsoft Azure](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options)
(circa 10 $ al mese) le persone fisiche devono risiedere negli Stati Uniti o in Canada, mentre le organizzazioni
anche nell'Unione europea. In alternativa serve un certificato OV da un'autorità di certificazione tradizionale.
Un certificato creato da te non basta. Con il certificato, lo script firma tutto da solo: vedi
[Firmare l'installer](#firmare-linstaller).

### Chiave API con la variabile d'ambiente

In alternativa alla finestra del programma, la chiave si può dare con la variabile d'ambiente `ANTHROPIC_API_KEY`.
La chiave inserita nel programma ha la precedenza. Attenzione: impostata per tutto l'utente (con `setx`), la
variabile la vedono anche gli altri programmi che usano Claude; Claude Code, per esempio, può usarla al posto del
tuo abbonamento. Per questo è meglio la finestra del programma.

## Creare l'installer

Serve sul PC che crea l'installer, non su quelli dove lo installi:

- **[.NET SDK 10](https://dotnet.microsoft.com/download)**;
- **[Inno Setup 6](https://jrsoftware.org/isinfo.php)**:
  `winget install --id JRSoftware.InnoSetup -e --scope user`. È gratuito; per l'uso commerciale gli autori chiedono
  di [acquistare una licenza](https://jrsoftware.org/isorder.php).

Poi, dalla cartella `traduttore-live`:

```powershell
powershell -ExecutionPolicy Bypass -File installer\crea-installer.ps1
```

Il risultato è `installer\out\TraduttoreLive-Setup-<versione>.exe`, circa 53 MB: è l'unico file da copiare sugli
altri PC. Per una versione nuova cambia `<Version>` in [TraduttoreLive.csproj](TraduttoreLive.csproj), oppure passala
allo script con `-Versione 1.1.0`. Lo script dell'installer è [installer/TraduttoreLive.iss](installer/TraduttoreLive.iss).

### Firmare l'installer

Senza parametri l'installer **non è firmato** (vedi [Avvisi di Windows sugli altri PC](#avvisi-di-windows-sugli-altri-pc)).
Con un certificato di firma del codice, lo script firma con SHA-256 e marca temporale il programma, le librerie
che non hanno già una firma valida (anche quelle di terze parti, che Smart App Control altrimenti bloccherebbe),
l'installer e il programma di disinstallazione. Gli strumenti di firma di Microsoft (signtool e, per Azure, il client
di Artifact Signing) li scarica da NuGet la prima volta e li usa solo se hanno una firma valida di Microsoft.

- **Certificato sul PC** (certificato OV su chiavetta o in cloud, se il fornitore lo rende visibile in Windows):
  trova l'impronta e passala allo script.

  ```powershell
  Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Format-List Subject, NotAfter, Thumbprint
  powershell -ExecutionPolicy Bypass -File installer\crea-installer.ps1 -Certificato <impronta>
  ```

- **Azure Artifact Signing**: crea un file, per esempio `C:\firma\metadata.json`, con i dati dell'account
  (l'endpoint è quello della regione dell'account), accedi ad Azure con `az login` e passalo allo script.

  ```json
  {
    "Endpoint": "https://weu.codesigning.azure.net/",
    "CodeSigningAccountName": "nome-account",
    "CertificateProfileName": "nome-profilo"
  }
  ```

  ```powershell
  powershell -ExecutionPolicy Bypass -File installer\crea-installer.ps1 -ArtifactSigning C:\firma\metadata.json
  ```

La marca temporale fa restare valida la firma anche dopo la scadenza del certificato. Il server predefinito è
quello di DigiCert con `-Certificato` e quello di Microsoft con `-ArtifactSigning`; per usarne un altro c'è
`-MarcaTemporale <indirizzo>`. Un certificato nuovo non ha ancora «reputazione»: nelle prime settimane SmartScreen
può mostrare l'avviso anche sui file firmati, finché non si accumulano abbastanza installazioni.

Per provare il programma durante lo sviluppo basta `dotnet run --project traduttore-live`.

## Come si usa

Avvia il programma prima o durante la riunione. La finestra compare in basso al centro dello schermo:

- **trascinala** dove vuoi tenendo premuto il pulsante sinistro, **ridimensionala** dall'angolo in basso a destra;
- **Ctrl + rotellina** ingrandisce o rimpicciolisce il testo;
- **⏸** mette in pausa l'ascolto, **⋯** (o il clic destro sulla finestra) apre il menu, **✕** chiude.

La riga piccola in grigio è la frase originale con la sua lingua (`EN`, `DE`…), quella grande in bianco è la
traduzione. Gli avvisi del programma compaiono in giallo. La riga in alto riassume cosa sta facendo: cosa ascolta,
le lingue, il modello di trascrizione e se usa la scheda grafica o il processore.

### Il menu

| Voce | A cosa serve |
|---|---|
| **Lingua dei sottotitoli** | La lingua in cui tradurre (predefinita: italiano). |
| **Lingua parlata nella riunione** | «Riconosci automaticamente» va bene quasi sempre. Se sai che si parla una sola lingua, sceglierla rende la trascrizione più precisa, soprattutto con le frasi brevi. |
| **Cosa ascoltare** | **Tutto l'audio del PC** (predefinito) oppure **solo l'audio di un programma**: l'elenco mostra i programmi che stanno usando l'audio. Scegliere il programma della riunione (es. *Microsoft Teams*) evita di tradurre notifiche, video e musica. Se il programma viene chiuso e riaperto, il traduttore lo ritrova da solo. |
| **Precisione della trascrizione** | Il modello di Whisper: più grande = più preciso ma più lento (vedi sotto). |
| **Mostra il testo originale** | Mostra o nasconde la riga piccola con la frase originale. |
| **Salva la trascrizione della riunione** | Attiva o disattiva il salvataggio in `Documenti\Traduttore dal vivo`. |
| **Chiave API di Claude…** | Inserisce, cambia o toglie la chiave API usata per tradurre. |

### Quale modello di trascrizione

| Modello | Dimensione | Quando usarlo |
|---|---|---|
| tiny | 32 MB | PC molto lenti; tanti errori. |
| base | 60 MB | Veloce; va bene con audio pulito e inglese standard. |
| **small** (predefinito) | 190 MB | Il miglior compromesso: regge accenti e lingue diverse dall'inglese. |
| medium | 540 MB | Più preciso, ma senza scheda grafica è troppo lento per il tempo reale. |
| large-v3-turbo | 575 MB | Il più preciso; solo con una scheda grafica veloce. |

Il programma prova a usare la scheda grafica (Vulkan, anche quelle integrate AMD e Intel) e, se non riesce, usa il
processore. Se il PC non riesce a stare al passo, compare un avviso giallo che suggerisce un modello più piccolo, e
intanto le frasi in attesa vengono unite per recuperare il ritardo.

Il sottotitolo arriva **qualche secondo dopo la fine della frase**: il programma aspetta una breve pausa (circa
mezzo secondo) per capire che la frase è finita, poi trascrive e traduce. Il tempo di trascrizione dipende dal PC e
dal modello: su un Ryzen 7 7735U senza scheda grafica, «base» impiega circa 2 secondi a frase. Chi parla a lungo
senza pause viene diviso in pezzi di al massimo 9 secondi.

## Impostazioni avanzate

Le impostazioni sono in `%APPDATA%\TraduttoreLive\impostazioni.json` (menu › *Apri la cartella delle
impostazioni*). Modificale a programma chiuso.

| Impostazione | Predefinito | Descrizione |
|---|---|---|
| `ModelloClaude` | `claude-haiku-5-5` | Il modello che traduce. `claude-opus-5-5` traduce meglio le frasi difficili (gergo, frasi spezzate, giochi di parole), ma costa molto di più ed è un po' più lento. |
| `ParoleChiave` | vuoto | Nomi di persone, aziende, prodotti e sigle che Whisper deve riconoscere bene, separati da virgole: `Marco Rossi, Contoso, SAP S/4HANA, KPI`. |
| `FileGlossario` | vuoto | Percorso di un file (Markdown o testo) con le traduzioni obbligate dei termini, per esempio lo stesso `GLOSSARIO.md` usato dall'agente traduttore. |
| `PausaFineFraseMs` | `550` | Silenzio, in millisecondi, che chiude una frase. Più basso = sottotitoli più rapidi ma frasi più spezzate. |
| `DurataMassimaFraseS` | `9` | Lunghezza massima di un pezzo di parlato, in secondi. |
| `UsaSchedaGrafica` | `true` | `false` per usare solo il processore (anche dal menu; vale dal riavvio successivo). |

Il modello di trascrizione e il rilevatore di voce si trovano in `%LOCALAPPDATA%\TraduttoreLive\modelli`: puoi
cancellare i modelli che non usi.

### Quanto costa la traduzione

La trascrizione è gratuita (gira sul PC). La traduzione si paga a consumo all'API di Claude: ogni frase costa circa
700 token in ingresso (istruzioni, contesto e frase) e un centinaio in uscita. Per un'ora di riunione con parlato
continuo, circa 600 frasi, la stima indicativa è:

| Modello | Costo indicativo per ora |
|---|---|
| `claude-haiku-5-5` (predefinito) | circa 0,10 $ |
| `claude-opus-5-5` | 3-4 $ |

I prezzi aggiornati sono nella [pagina dei prezzi dell'API](https://platform.claude.com/docs/en/about-claude/pricing).

## Riservatezza

- L'**audio** non lascia mai il PC: la trascrizione avviene in locale.
- Il **testo trascritto** viene inviato all'API di Claude per la traduzione: valuta se è compatibile con le regole
  della tua azienda sulle informazioni riservate.
- Trascrivere e salvare una riunione può richiedere il **consenso dei partecipanti**: informali, come faresti per
  una registrazione. Se non vuoi tenere traccia, disattiva *Salva la trascrizione della riunione*.

## Se qualcosa non va

| Problema | Cosa fare |
|---|---|
| Non compare nessun sottotitolo | Controlla in alto cosa sta ascoltando. Se ascolti «tutto l'audio», verifica che Teams usi le stesse casse o cuffie predefinite di Windows; altrimenti scegli dal menu il dispositivo giusto o direttamente il programma. |
| «In attesa che … sia avviato» | Il programma scelto in *Cosa ascoltare* non è aperto. Si collega da solo appena lo avvii. |
| Sottotitoli in ritardo | Scegli un modello più piccolo (*base*) o chiudi i programmi pesanti. |
| La lingua viene riconosciuta male | Imposta la *Lingua parlata nella riunione*. |
| Nomi e sigle trascritti male | Aggiungili in `ParoleChiave`. |
| «Manca la chiave API di Claude» | Inseriscila dal menu › *Chiave API di Claude…*. |
| «Claude rifiuta la chiave API» | La chiave è sbagliata o è stata revocata: creane una nuova su platform.claude.com e inseriscila dal menu. |
| Il programma o l'installer non parte | Vedi [Avvisi di Windows sugli altri PC](#avvisi-di-windows-sugli-altri-pc). |

## Limiti

- Traduce solo le voci degli altri: la tua voce passa dal microfono e non viene ascoltata.
- Mostra i sottotitoli, non legge la traduzione ad alta voce: una voce sintetica si sovrapporrebbe a quella
  originale e verrebbe ricatturata dal programma stesso.
- Non distingue chi sta parlando.
- Funziona solo su Windows.

## Come è fatto

| File | Contenuto |
|---|---|
| [Audio/CatturaAudio.cs](Audio/CatturaAudio.cs) | Ascolto dell'audio in uscita (WASAPI loopback) di un dispositivo o di un solo programma. |
| [Audio/ConvertitoreAudio.cs](Audio/ConvertitoreAudio.cs) | Conversione in mono a 16 kHz, il formato di Whisper. |
| [Audio/SegmentatoreVoce.cs](Audio/SegmentatoreVoce.cs) | Divisione del parlato in frasi in base alle pause. |
| [Audio/Programmi.cs](Audio/Programmi.cs) | Elenco dei programmi che usano l'audio. |
| [Trascrizione/Trascrittore.cs](Trascrizione/Trascrittore.cs) | Whisper e rilevatore di voce, scaricamento dei modelli, filtri contro le frasi inventate. |
| [Traduzione/TraduttoreClaude.cs](Traduzione/TraduttoreClaude.cs) | Istruzioni e chiamata a Claude (SDK ufficiale Anthropic, con streaming). |
| [Traduzione/ChiaveApi.cs](Traduzione/ChiaveApi.cs) | Salvataggio cifrato della chiave API (DPAPI di Windows). |
| [Motore.cs](Motore.cs) | Collegamento delle varie parti e gestione degli errori. |
| [MainWindow.xaml](MainWindow.xaml) | La finestra dei sottotitoli e il menu. |
| [FinestraChiaveApi.xaml](FinestraChiaveApi.xaml) | La finestra per inserire la chiave API. |
| [installer/](installer/) | Script di Inno Setup e script che crea l'installer. |

Librerie usate: [NAudio](https://github.com/naudio/NAudio) per l'audio, [Whisper.net](https://github.com/sandrohanea/whisper.net)
per la trascrizione, l'[SDK Anthropic per C#](https://github.com/anthropics/anthropic-sdk-csharp) per Claude.
