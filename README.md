# Agente traduttore e Traduttore dal vivo

Due strumenti di traduzione open source, con licenza MIT:

- **[Traduttore dal vivo](traduttore-live/README.md)**: un programma per Windows che **traduce in tempo reale le voci
  di una riunione online** (Teams, Zoom, Google Meet, Webex…) e le mostra come sottotitoli. La trascrizione avviene
  sul PC con Whisper, la traduzione è gratuita. **[Scarica l'ultima versione](https://github.com/agreci84-ui/agente-traduttore/releases/latest)**.
- **Agente traduttore**: un agente per [Claude Code](https://claude.com/claude-code) che traduce testi, file e
  documentazione dei progetti tra qualsiasi coppia di lingue, senza rompere formattazione, codice e segnaposto.
  È definito in [.claude/agents/traduttore.md](.claude/agents/traduttore.md) ed è descritto qui sotto.

Un agente di Claude Code lavora su testi e file e non può ascoltare l'audio: per le riunioni dal vivo c'è il
programma a parte.

## Cosa sa fare

- Traduce **testo incollato** nella conversazione, **singoli file** o **intere cartelle**.
- Riconosce la lingua di partenza; chiede la lingua di arrivo solo se non è chiara.
- Gestisce Markdown, testo semplice, file di localizzazione (`.json`, `.resx`, `.po`, `.strings`, `.properties`, `.yaml`, `.xlf`), HTML e XAML, sottotitoli (`.srt`, `.vtt`). Le stringhe dentro il codice sorgente le traduce solo se glielo chiedi.
- Lascia intatti codice, percorsi, URL, chiavi, segnaposto (`{0}`, `%s`, `$variabile`…) e la struttura del documento.
- Usa sempre gli stessi termini e segue il glossario del progetto, se c'è (`GLOSSARIO.md`).
- Per impostazione predefinita **crea un file nuovo** accanto all'originale (`README.md` → `README.en.md`): sovrascrive o modifica file esistenti solo se lo chiedi.
- Alla fine riassume i file creati, le scelte di terminologia e i dubbi.

## Come usarlo

L'agente funziona nei progetti in cui si trova il file `traduttore.md`.

**In un solo progetto**: copia il file nella cartella `.claude\agents\` di quel progetto.

```powershell
New-Item -ItemType Directory -Force "C:\percorso\del\progetto\.claude\agents" | Out-Null
Copy-Item ".claude\agents\traduttore.md" "C:\percorso\del\progetto\.claude\agents\"
```

**In tutti i progetti**: copialo nella cartella degli agenti del tuo utente.

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.claude\agents" | Out-Null
Copy-Item ".claude\agents\traduttore.md" "$env:USERPROFILE\.claude\agents\"
```

Claude Code carica gli agenti all'avvio della sessione: dopo la copia, apri una nuova conversazione.

Poi basta chiederlo, per esempio:
- «Usa il traduttore per creare la versione inglese del README»
- «Traduci in tedesco tutti i file `.md` della cartella `docs`»
- «Con il traduttore, rivedi la traduzione francese in `locales/fr.json`»

## Glossario

Per avere sempre la stessa traduzione dei termini di un progetto, aggiungi al progetto un file `GLOSSARIO.md`, per esempio:

```markdown
| Italiano | Inglese | Note |
|---|---|---|
| Centro sicurezza | Security Center | nome del prodotto |
| copia non protetta | unprotected copy | |
| dashboard | dashboard | non si traduce |
```

L'agente lo legge prima di iniziare e le sue scelte prevalgono su quelle dell'agente.

## Traduzione dal vivo delle riunioni

Il programma [Traduttore dal vivo](traduttore-live/README.md) ascolta l'audio del PC durante una riunione online e
mostra la traduzione delle voci degli altri partecipanti come sottotitoli, in una finestra sempre in primo piano.
La trascrizione avviene sul PC con Whisper. La traduzione è gratuita: il programma usa uno dopo l'altro
TranslateGemma sul PC, DeepL, Microsoft Translator e MyMemory, passando al successivo quando uno non è disponibile;
Claude, a pagamento, si può scegliere dal menu.

Le trascrizioni che salva (`Documenti\Traduttore dal vivo`) sono normali file di testo: dopo la riunione puoi
chiedere all'agente di tradurle in un'altra lingua o di trarne un riassunto.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

L'installer di Traduttore dal vivo viene compilato da GitHub Actions a partire dal codice di questo repository
([workflow](.github/workflows/build.yml)) e ogni release viene firmata solo dopo l'approvazione manuale di un
responsabile. Si firmano solo i file del progetto; le librerie di terze parti sono incluse così come le distribuiscono
i loro autori ([elenco](THIRD-PARTY-NOTICES.md)). La firma è attiva dopo l'approvazione del progetto da parte di
SignPath Foundation: le release precedenti non sono firmate.

*The Traduttore dal vivo installer is built by GitHub Actions from the source code in this repository, and each
release is signed only after manual approval. Only the project's own files are signed; third-party libraries are
included as distributed by their authors.*

Team roles:
- Committers and reviewers: [agreci84-ui](https://github.com/agreci84-ui)
- Approvers: [agreci84-ui](https://github.com/agreci84-ui)

Le modifiche proposte da altri (pull request) vengono riviste da un membro del team prima di essere accettate.
*Changes proposed by others (pull requests) are reviewed by a team member before being merged.*

Privacy policy: this program will not transfer any information to other networked systems unless specifically
requested by the user or the person installing or operating it. The online translation services are used only if
the user accepts them during installation, at first start or from the menu, and can be turned off at any time.
Details: [PRIVACY.md](PRIVACY.md).

## Privacy

L'audio delle riunioni non lascia mai il PC. Il testo trascritto va ai servizi di traduzione online solo se li
accetti, durante l'installazione, al primo avvio o dal menu; con TranslateGemma resta sul PC. Tutti i dettagli
sono nell'[informativa sulla privacy](PRIVACY.md).

## Licenza

[MIT](LICENSE). I componenti di terze parti e le loro licenze sono elencati in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
