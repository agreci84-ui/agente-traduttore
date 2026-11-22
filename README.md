# Agente traduttore

Un agente per [Claude Code](https://claude.com/claude-code) che traduce testi, file e documentazione dei progetti tra qualsiasi coppia di lingue, senza rompere formattazione, codice e segnaposto.

L'agente è definito in [.claude/agents/traduttore.md](.claude/agents/traduttore.md).

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
