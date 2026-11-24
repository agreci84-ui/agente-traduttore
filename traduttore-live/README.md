# Traduttore dal vivo

Un programma per Windows che **traduce in tempo reale le voci di una riunione online** (Teams, Zoom, Google Meet,
Webex o qualsiasi altro programma) e le mostra come sottotitoli in una finestra sempre in primo piano.

Ascolta quello che esce dalle casse o dalle cuffie, quindi sente **gli altri partecipanti**, non il tuo microfono, e
funziona con qualsiasi programma senza bisogno di installare niente al suo interno.

```
audio del PC ──► frasi ──► trascrizione (Whisper, sul PC) ──► traduzione (servizi gratuiti) ──► sottotitoli
  (WASAPI)       (pause)    l'audio non esce mai dal PC        uno dopo l'altro                + file di trascrizione
```

- **Trascrizione sul PC** con [Whisper](https://github.com/ggml-org/whisper.cpp): l'audio della riunione non viene mai
  inviato a nessuno. Prima di trascrivere, un rilevatore di voce (Silero VAD) scarta musica, rumori e silenzi.
- **Traduzione con l'AI integrata**, gratis e senza limiti: TranslateGemma, il modello di traduzione aperto di
  Google, gira dentro il programma. Non servono account né chiavi, e il testo non lascia il PC: il programma lo
  scarica da solo la prima volta (2,5 GB). Finché non è pronto, o se il PC non sta al passo, il programma passa ai
  servizi online gratuiti, uno dopo l'altro (vedi [Servizi di traduzione](#servizi-di-traduzione)). Claude, a
  pagamento, resta disponibile se lo scegli.
- **Lingua riconosciuta frase per frase**: se in riunione si parlano più lingue, ogni frase viene tradotta dalla sua.
  Le frasi già nella lingua dei sottotitoli non vengono tradotte.
- **Trascrizione salvata** in `Documenti\Traduttore dal vivo\Riunione <data e ora>.txt`, con l'originale e la
  traduzione di ogni frase.

## Requisiti

- **Windows 10 (versione 1809 o successiva) o Windows 11, a 64 bit.** Per ascoltare *un solo programma* serve
  Windows 10 versione 2004 o successiva; con le versioni precedenti si può ascoltare tutto l'audio del PC.
- **8 GB di memoria** (RAM) e **3 GB liberi sul disco** per l'AI integrata. Con una scheda grafica (anche integrata,
  se supporta Vulkan) traduce più in fretta.
- **Internet** alla prima partenza, per scaricare il modello di trascrizione (190 MB con l'impostazione predefinita) e
  l'AI integrata (2,5 GB), e durante l'uso per i servizi di traduzione online. Con l'AI integrata pronta, la
  traduzione funziona anche senza Internet.

Non serve nessun abbonamento né nessuna configurazione: il programma scarica da solo l'AI integrata e intanto, se
accetti i servizi online, traduce con MyMemory.

## Servizi di traduzione

Nella modalità **Automatica** (predefinita) il programma usa l'**AI integrata** e, finché non è pronta o se il PC non
sta al passo, i servizi gratuiti che hai configurato, in quest'ordine. Se uno non può tradurre (spento, quota
gratuita finita, chiave sbagliata, rete assente), passa al successivo e lo riprova più tardi; un avviso giallo dice
cosa è successo e la riga in alto mostra quale servizio sta traducendo.

**L'AI integrata** è TranslateGemma 4B, il modello di traduzione aperto di Google, eseguito dentro il programma con
[llama.cpp](https://github.com/ggml-org/llama.cpp): gratis, senza limiti, senza account né chiavi, e il testo non
lascia il PC. La prima volta il programma la scarica da solo da Hugging Face (2,5 GB), in background: la riga in alto
mostra l'avanzamento, e un avviso giallo dice quando è pronta. Con una buona scheda grafica basta circa un secondo a
frase; con il solo processore qualche secondo (da 2 a 4 su un Mac con processore Intel), mentre il testo compare
man mano. Se il PC non sta al passo (più di
8 secondi a frase) e ci sono altri servizi, fino al riavvio il programma passa a quelli. Si disattiva dal menu ›
*Traduzione › Usa l'AI integrata*; dallo stesso menu si può scegliere *Solo l'AI integrata*, per non usare mai i
servizi online.

Gli altri servizi non servono per forza: si configurano nella sezione **Avanzate** della finestra dei servizi.

| Servizio | Gratis | Cosa serve | Il testo va a |
|---|---|---|---|
| **1. TranslateGemma sul PC** | senza limiti | [Ollama](https://ollama.com/download) installato e il modello TranslateGemma di Google (4b: 3,3 GB; 12b: 8,1 GB), che il programma scarica per te | nessuno: resta sul PC |
| **2. Google Gemini** (AI) | un limite di richieste al giorno deciso da Google (di solito circa un'ora di riunione) | un account Google e una [chiave API di Google AI Studio](https://aistudio.google.com/apikey), senza carta di credito | Google, che sul piano gratuito può usare i testi per migliorare i suoi prodotti |
| **3. Groq** (AI) | un limite di token al giorno (circa mezz'ora di riunione) | un account e una [chiave API di GroqCloud](https://console.groq.com/keys), senza carta di credito | Groq, che non usa i testi per addestrare i modelli |
| **4. DeepL** | 500.000 caratteri al mese (circa 10 ore di parlato) | un account [DeepL API Free](https://www.deepl.com/pro-api) e la sua chiave | DeepL |
| **5. Microsoft Translator** | 2 milioni di caratteri al mese (circa 40 ore) | un account Azure e una [risorsa Translator](https://learn.microsoft.com/azure/ai-services/translator/create-translator-resource) con piano F0 | Microsoft |
| **6. MyMemory** | circa 5.000 caratteri al giorno, 50.000 con la tua email (circa un'ora) | niente | MyMemory (Translated) |

Chiavi, email e modello si impostano dal menu › **Traduzione › Servizi e chiavi…**, nella sezione *Avanzate*. Lì c'è
anche **Prova i servizi**, che traduce una frase di prova con ciascuno e dice se funziona. Chiavi ed email restano sul
PC, cifrate con l'account di Windows.

Dal menu › *Traduzione › Un solo servizio* puoi anche scegliere **un solo servizio**: in quel caso il programma usa
solo quello.

**TranslateGemma in Ollama** è un'alternativa all'AI integrata per chi usa già [Ollama](https://ollama.com): lo
stesso modello, fatto girare da Ollama invece che dal programma. Per usarlo:
1. installa [Ollama](https://ollama.com/download), un programma gratuito che fa girare i modelli sul PC e parte da
   solo con Windows;
2. nel menu › *Traduzione › Servizi e chiavi…* premi **Scarica il modello** e aspetta la fine (3,3 GB).

Sui PC senza una scheda grafica dedicata ogni frase può richiedere qualche secondo, perché il processore lavora
anche per la trascrizione: se i sottotitoli restano indietro, usa un servizio online o scegli un modello di
trascrizione più piccolo. In `OllamaModello` puoi indicare anche un modello di chat generico (per esempio
`gemma3:4b`): a differenza di TranslateGemma usa le frasi precedenti come contesto e il glossario.

**Le AI gratuite (Google Gemini e Groq)** traducono molto meglio di DeepL, Microsoft e MyMemory: fanno da interprete
come Claude, seguono il senso della conversazione grazie alle frasi precedenti, rispettano il glossario e correggono
le parole trascritte male. Il prezzo è un limite giornaliero: quando finisce, il programma passa al servizio
successivo e le riprova dopo un'ora. Per crearne la chiave basta un account, senza carta di credito; poi incollala
nel menu › *Traduzione › Servizi e chiavi…*. Le quote dei piani gratuiti le decidono Google e Groq e cambiano spesso.
Attenzione: con il **piano gratuito di Gemini, Google può usare i testi** per migliorare i suoi prodotti, anche con
la revisione di persone, quindi non usarlo per le riunioni riservate; Groq usa i testi solo per rispondere. I
modelli si cambiano in `GeminiModello` e `GroqModello` (vedi [Impostazioni avanzate](#impostazioni-avanzate)).

**Claude**, a pagamento, traduce meglio le frasi difficili e usa contesto e glossario. Non viene mai usato in
automatico: per usarlo inserisci la chiave API nella stessa finestra e sceglilo dal menu › *Traduzione*. L'API di
Claude si paga a consumo, separatamente dagli abbonamenti a Claude.

Se nessun servizio è disponibile, il programma mostra solo la trascrizione; se i sottotitoli sono in inglese,
traduce da solo con Whisper, con una qualità inferiore.

Per il Mac c'è una versione a parte, con le stesse funzioni: vedi [Traduttore dal vivo per Mac](../traduttore-live-mac/README.md).

## Installazione

1. **Scarica `TraduttoreLive-Setup-<versione>.exe`** dalle
   [release su GitHub](https://github.com/agreci84-ui/agente-traduttore/releases/latest) (oppure crealo tu, vedi
   [Creare l'installer](#creare-linstaller)) ed eseguilo. Non servono i diritti di amministratore: il programma si
   installa solo per l'utente corrente, in `%LOCALAPPDATA%\Programs\Traduttore dal vivo`, con il collegamento nel
   menu Start e, se vuoi, sul desktop. Il runtime .NET è incluso: non serve installare altro.
2. L'installazione mostra l'**informativa sulla privacy** e chiede se usare i **servizi di traduzione online**, che
   ricevono il testo trascritto (non l'audio). Se togli la spunta, il programma traduce solo sul PC con
   TranslateGemma. Puoi cambiare idea in qualsiasi momento dal menu › *Traduzione*.
3. **Avvialo**: la prima volta scarica l'AI integrata (2,5 GB, qualche minuto) e intanto, con i servizi online,
   traduce con MyMemory. Non c'è niente da configurare.

Per **aggiornare** esegui l'installer della versione nuova: impostazioni e chiavi restano. Per **disinstallare** usa
Impostazioni › App › App installate › *Traduttore dal vivo*. La disinstallazione toglie anche i modelli di
trascrizione e l'AI integrata scaricati e le chiavi; lascia le impostazioni e, in `Documenti`, le trascrizioni delle riunioni. Il
modello TranslateGemma lo conserva Ollama: per toglierlo, `ollama rm translategemma:4b`, oppure disinstalla Ollama.

### Avvisi di Windows sugli altri PC

Le release sono firmate gratuitamente da [SignPath Foundation](https://signpath.org) dopo l'approvazione del
progetto (vedi la [code signing policy](../README.md#code-signing-policy)). Finché l'installer non è firmato, Windows
non lo conosce:

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

Anche con la firma, un certificato nuovo non ha ancora «reputazione»: nelle prime settimane SmartScreen può mostrare
l'avviso finché non si accumulano abbastanza installazioni. Con SignPath Foundation si firmano solo i file del
progetto: le librerie di terze parti (NAudio, Whisper.net, l'SDK di Anthropic) e il programma di disinstallazione di
Inno Setup restano senza firma, e Smart App Control potrebbe ancora bloccarli.

### Chiave di Claude con la variabile d'ambiente

In alternativa alla finestra del programma, la chiave di Claude si può dare con la variabile d'ambiente
`ANTHROPIC_API_KEY`. La chiave inserita nel programma ha la precedenza. Attenzione: impostata per tutto l'utente
(con `setx`), la variabile la vedono anche gli altri programmi che usano Claude; Claude Code, per esempio, può usarla
al posto del tuo abbonamento. Per questo è meglio la finestra del programma.

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

**Con SignPath Foundation (gratuito, per i progetti open source).** Le release ufficiali le compila e le firma
GitHub Actions ([.github/workflows/build.yml](../.github/workflows/build.yml)) quando si pubblica un tag `vX.Y.Z`
uguale alla `<Version>` del progetto:
1. compila il programma su un runner di GitHub;
2. fa firmare a SignPath i file del progetto (`TraduttoreLive.exe` e `TraduttoreLive.dll`);
3. crea l'installer con quei file e fa firmare anche l'installer;
4. pubblica la release con l'installer firmato.

Ogni firma va approvata a mano su SignPath. Le configurazioni degli artifact da creare su SignPath sono in
[.signpath/artifact-configurations](../.signpath/artifact-configurations). Nelle impostazioni del repository
(*Settings › Secrets and variables › Actions*) servono il segreto `SIGNPATH_API_TOKEN` e le variabili
`SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG` e `SIGNPATH_SIGNING_POLICY_SLUG`. Senza questi dati la release si
crea lo stesso, senza firma.

```powershell
# nuova versione: cambia <Version> in TraduttoreLive.csproj, fai il commit, poi
git tag v1.2.0
git push origin v1.2.0
```

**Con un tuo certificato, in locale.** Senza parametri lo script crea l'installer **non firmato** (vedi
[Avvisi di Windows sugli altri PC](#avvisi-di-windows-sugli-altri-pc)).
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
| **Traduzione** | **Automatica** (predefinita: l'AI integrata, poi i servizi gratuiti uno dopo l'altro), **Solo l'AI integrata** oppure *Un solo servizio*. *Usa l'AI integrata* la attiva o la disattiva; *Servizi e chiavi…* apre la finestra per configurare e provare i servizi. |
| **Salva la trascrizione della riunione** | Attiva o disattiva il salvataggio in `Documenti\Traduttore dal vivo`. |

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
| `ServizioTraduzione` | `automatico` | Anche dal menu › *Traduzione*: `automatico`, `integrata`, `ollama`, `gemini`, `groq`, `deepl`, `microsoft`, `mymemory` o `claude`. |
| `UsaAIIntegrata` | `true` | Usa l'AI integrata nella modalità automatica (anche dal menu). Con `false` non viene scaricata. |
| `OllamaModello` | `translategemma:4b` | Il modello di Ollama: `translategemma:12b` è più preciso ma vuole un PC potente; un modello di chat (es. `gemma3:4b`) usa anche contesto e glossario. |
| `OllamaIndirizzo` | `http://localhost:11434` | Dove risponde Ollama. |
| `GeminiModello` | `gemini-flash-lite-latest` | Il modello di Google Gemini. Con questo nome Google aggiorna da solo il modello Flash-Lite, quello con più traduzioni gratuite al giorno; `gemini-flash-latest` traduce un po' meglio, ma il piano gratuito ne concede molte meno. |
| `GroqModello` | `openai/gpt-oss-120b` | Il modello di Groq, tra quelli del suo piano gratuito (es. `openai/gpt-oss-20b`, più rapido). Ogni modello ha la sua quota giornaliera. |
| `MicrosoftRegione` | vuoto | La regione della risorsa Microsoft Translator (es. `westeurope`); vuota per le risorse globali. |
| `ModelloClaude` | `claude-haiku-5-5` | Il modello di Claude, se lo scegli. `claude-opus-5-5` traduce meglio le frasi difficili, ma costa molto di più ed è un po' più lento. |
| `ParoleChiave` | vuoto | Nomi di persone, aziende, prodotti e sigle che Whisper deve riconoscere bene, separati da virgole: `Marco Rossi, Contoso, SAP S/4HANA, KPI`. |
| `FileGlossario` | vuoto | Percorso di un file (Markdown o testo) con le traduzioni obbligate dei termini, per esempio lo stesso `GLOSSARIO.md` usato dall'agente traduttore. Lo usano Gemini, Groq, Claude e i modelli di chat in Ollama. |
| `PausaFineFraseMs` | `550` | Silenzio, in millisecondi, che chiude una frase. Più basso = sottotitoli più rapidi ma frasi più spezzate. |
| `DurataMassimaFraseS` | `9` | Lunghezza massima di un pezzo di parlato, in secondi. |
| `UsaSchedaGrafica` | `true` | `false` per usare solo il processore (anche dal menu; vale dal riavvio successivo). |

Il modello di trascrizione, il rilevatore di voce e l'AI integrata (`translategemma-4b-it.Q4_K_M.gguf`) si trovano in
`%LOCALAPPDATA%\TraduttoreLive\modelli`: puoi cancellare i modelli che non usi (l'AI integrata, se è attiva, viene
riscaricata).

### Quanto costa la traduzione

Niente, con i servizi della modalità automatica: trascrizione e AI integrata girano sul PC, e Gemini, Groq, DeepL,
Microsoft Translator e MyMemory si usano entro le loro quote gratuite. Un'ora di riunione con parlato continuo sono circa 600
frasi e 50.000 caratteri: da qui le stime in ore della tabella dei servizi. Quando un servizio finisce la quota, il
programma passa al successivo; Gemini, Groq, DeepL e Microsoft non addebitano niente sui piani gratuiti.

Solo **Claude**, se lo scegli, si paga a consumo: ogni frase costa circa 700 token in ingresso e un centinaio in
uscita, cioè circa 0,10 $ all'ora con `claude-haiku-5-5` e 3-4 $ con `claude-opus-5-5`. I prezzi aggiornati sono nella
[pagina dei prezzi dell'API](https://platform.claude.com/docs/en/about-claude/pricing).

## Riservatezza

- L'**audio** non lascia mai il PC: la trascrizione avviene in locale.
- Il **testo trascritto** resta sul PC con l'AI integrata e con TranslateGemma in Ollama; con i servizi online viene inviato al servizio che lo
  traduce (Gemini, Groq, DeepL, Microsoft, MyMemory o Claude). Con il piano gratuito di **Gemini**, Google può usare
  i testi per migliorare i suoi prodotti: per le riunioni riservate non usarlo. I servizi online si usano solo se li accetti (durante
  l'installazione, al primo avvio o dal menu › *Traduzione › Usa i servizi online*). Valuta se è compatibile con le
  regole della tua azienda sulle informazioni riservate: per le riunioni riservate disattiva i servizi online.
- Tutti i dettagli sono nell'[informativa sulla privacy](../PRIVACY.md).
- Trascrivere e salvare una riunione può richiedere il **consenso dei partecipanti**: informali, come faresti per
  una registrazione. Se non vuoi tenere traccia, disattiva *Salva la trascrizione della riunione*.

## Se qualcosa non va

| Problema | Cosa fare |
|---|---|
| Non compare nessun sottotitolo | Controlla in alto cosa sta ascoltando. Se ascolti «tutto l'audio», verifica che Teams usi le stesse casse o cuffie predefinite di Windows; altrimenti scegli dal menu il dispositivo giusto o direttamente il programma. |
| «In attesa che … sia avviato» | Il programma scelto in *Cosa ascoltare* non è aperto. Si collega da solo appena lo avvii. |
| Sottotitoli in ritardo | Scegli un modello di trascrizione più piccolo (*base*) o chiudi i programmi pesanti. Se a rallentare è l'AI integrata, il programma passa da solo ai servizi online (se li hai accettati). |
| «L'AI integrata è troppo lenta su questo computer…» | Il PC non riesce a tradurre in diretta con l'AI integrata: fino al riavvio il programma usa i servizi online. Con un modello di trascrizione più piccolo resta più processore per l'AI. |
| «Non riesco a preparare l'AI integrata: …» | Di solito manca spazio sul disco (servono 3 GB) o la connessione si è interrotta: il programma riprova da solo. I dettagli tecnici sono in `ai-integrata.log`, nella cartella delle impostazioni. |
| La lingua viene riconosciuta male | Imposta la *Lingua parlata nella riunione*. |
| Nomi e sigle trascritti male | Aggiungili in `ParoleChiave`. |
| «MyMemory: le traduzioni gratuite di oggi sono finite» | Indica la tua email per MyMemory o configura un altro servizio gratuito (menu › *Traduzione › Servizi e chiavi…*). |
| «Gemini: le traduzioni gratuite di oggi sono finite» (o Groq) | È finita la quota giornaliera dell'AI gratuita: il programma passa al servizio successivo e la riprova dopo un'ora. Per avere più traduzioni gratuite configura sia Gemini sia Groq, e DeepL. |
| «Gemini: il piano gratuito non è disponibile nel tuo paese» | Usa Groq o gli altri servizi. |
| «… il modello … non esiste» | Il modello indicato in `GeminiModello` o `GroqModello` non c'è più (i servizi gratuiti li cambiano spesso): cancella la riga dalle impostazioni per tornare a quello predefinito, o scegline uno dall'elenco del servizio. |
| «… rifiuta la chiave» | La chiave è sbagliata o è stata revocata: controllala con *Prova i servizi* nella finestra dei servizi. |
| «Ollama non è avviato o non è installato» | Avvia Ollama dal menu Start (di solito parte da solo con Windows) o installalo. |
| TranslateGemma è lento | Il PC non ha una scheda grafica adatta: usa la modalità automatica con un servizio online, oppure un modello di trascrizione più piccolo. |
| Il programma o l'installer non parte | Vedi [Avvisi di Windows sugli altri PC](#avvisi-di-windows-sugli-altri-pc). |

## Limiti

- Traduce solo le voci degli altri: la tua voce passa dal microfono e non viene ascoltata.
- Mostra i sottotitoli, non legge la traduzione ad alta voce: una voce sintetica si sovrapporrebbe a quella
  originale e verrebbe ricatturata dal programma stesso.
- Non distingue chi sta parlando.
- Questo programma funziona solo su Windows; per il Mac c'è la [versione per Mac](../traduttore-live-mac/README.md).

## Come è fatto

| File | Contenuto |
|---|---|
| [Audio/CatturaAudio.cs](Audio/CatturaAudio.cs) | Ascolto dell'audio in uscita (WASAPI loopback) di un dispositivo o di un solo programma. |
| [Audio/ConvertitoreAudio.cs](Audio/ConvertitoreAudio.cs) | Conversione in mono a 16 kHz, il formato di Whisper. |
| [Audio/SegmentatoreVoce.cs](Audio/SegmentatoreVoce.cs) | Divisione del parlato in frasi in base alle pause. |
| [Audio/Programmi.cs](Audio/Programmi.cs) | Elenco dei programmi che usano l'audio. |
| [Trascrizione/Trascrittore.cs](Trascrizione/Trascrittore.cs) | Whisper e rilevatore di voce, scaricamento dei modelli, filtri contro le frasi inventate. |
| [Traduzione/AIIntegrata.cs](Traduzione/AIIntegrata.cs) | L'AI integrata: scaricamento e controllo del modello, caricamento con llama.cpp, traduzione con le istruzioni tenute in memoria. |
| [Traduzione/TranslateGemma.cs](Traduzione/TranslateGemma.cs) | Le istruzioni nel formato di TranslateGemma, per l'AI integrata e per Ollama. |
| [Traduzione/CatenaTraduzione.cs](Traduzione/CatenaTraduzione.cs) | Usa i servizi uno dopo l'altro, li sospende quando non sono disponibili e tiene il contesto. |
| [Traduzione/Servizi.cs](Traduzione/Servizi.cs) | Sceglie i servizi da usare in base alle impostazioni. |
| [Traduzione/ServizioOllama.cs](Traduzione/ServizioOllama.cs) | TranslateGemma (o un altro modello) sul PC con Ollama; controllo e scaricamento del modello. |
| [Traduzione/ServizioDeepL.cs](Traduzione/ServizioDeepL.cs), [ServizioMicrosoft.cs](Traduzione/ServizioMicrosoft.cs), [ServizioMyMemory.cs](Traduzione/ServizioMyMemory.cs) | I servizi online gratuiti. |
| [Traduzione/ServizioAI.cs](Traduzione/ServizioAI.cs) | Le AI gratuite Google Gemini e Groq, con le istruzioni da interprete (API compatibile con OpenAI, con streaming). |
| [Traduzione/ServizioClaude.cs](Traduzione/ServizioClaude.cs) | Claude, a pagamento (SDK ufficiale Anthropic, con streaming). |
| [Traduzione/Interprete.cs](Traduzione/Interprete.cs) | Istruzioni da interprete per Claude, le AI gratuite e i modelli di chat. |
| [Traduzione/Credenziali.cs](Traduzione/Credenziali.cs) | Salvataggio cifrato di chiavi ed email (DPAPI di Windows). |
| [Motore.cs](Motore.cs) | Collegamento delle varie parti e gestione degli errori. |
| [MainWindow.xaml](MainWindow.xaml) | La finestra dei sottotitoli e il menu. |
| [FinestraServizi.xaml](FinestraServizi.xaml) | La finestra dei servizi di traduzione. |
| [installer/](installer/) | Script di Inno Setup e script che crea l'installer. |

Librerie usate: [NAudio](https://github.com/naudio/NAudio) per l'audio, [Whisper.net](https://github.com/sandrohanea/whisper.net)
per la trascrizione, [LLamaSharp](https://github.com/SciSharp/LLamaSharp) (llama.cpp) per l'AI integrata,
l'[SDK Anthropic per C#](https://github.com/anthropics/anthropic-sdk-csharp) per Claude.
TranslateGemma è distribuito da Google con i [termini d'uso di Gemma](https://ai.google.dev/gemma/terms).
