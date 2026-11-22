# Informativa sulla privacy — Traduttore dal vivo

*English version below.*

Traduttore dal vivo non ha un server, non raccoglie statistiche d'uso e non invia niente ai suoi autori.

## Cosa resta sul PC

- **L'audio** della riunione: viene trascritto sul PC con Whisper e non viene mai inviato a nessuno.
- **Le trascrizioni** salvate in `Documenti\Traduttore dal vivo` (si può disattivare dal menu).
- **Le impostazioni** e le **chiavi dei servizi**, queste ultime cifrate con l'account di Windows.
- Con **TranslateGemma** (tramite Ollama, sul PC) anche il testo da tradurre resta sul PC.

## Cosa passa in rete, e quando

| Cosa | A chi | Quando |
|---|---|---|
| Il **testo trascritto** di ogni frase (non l'audio), con le frasi precedenti come contesto per DeepL e Claude | Il servizio di traduzione in uso: MyMemory (Translated srl), DeepL, Microsoft Translator o Anthropic (Claude) | Solo se hai accettato i **servizi online** (durante l'installazione, al primo avvio o dal menu › Traduzione) |
| L'**indirizzo email** che indichi per MyMemory | MyMemory, insieme a ogni richiesta | Solo se lo inserisci |
| Una **frase di prova** fissa | I servizi configurati | Quando premi *Prova i servizi* |
| Le richieste di **scaricamento** dei modelli di trascrizione Whisper e del rilevatore di voce | Hugging Face | Al primo avvio e quando scegli un altro modello di trascrizione |
| La richiesta di **scaricamento** di TranslateGemma | Ollama, che lo scarica dal proprio archivio | Quando premi *Scarica il modello* |

Come per qualsiasi collegamento a Internet, questi servizi ricevono anche l'indirizzo IP del PC. Per come trattano i
dati, valgono le loro informative: [MyMemory](https://mymemory.translated.net/doc/terms.php),
[DeepL](https://www.deepl.com/privacy), [Microsoft](https://www.microsoft.com/privacy/privacystatement),
[Anthropic](https://www.anthropic.com/legal/privacy), [Hugging Face](https://huggingface.co/privacy).

## Come disattivare i servizi online

- **Durante l'installazione**: togli la spunta da *Usa i servizi di traduzione online gratuiti*.
- **In qualsiasi momento**: menu › *Traduzione* › togli la spunta da *Usa i servizi online*.

Con i servizi online disattivati il programma traduce solo con TranslateGemma sul PC, se è configurato; altrimenti
mostra solo la trascrizione.

Trascrivere e salvare una riunione può richiedere il consenso dei partecipanti: informali, come faresti per una
registrazione.

---

# Privacy policy — Traduttore dal vivo

Traduttore dal vivo has no server, collects no usage statistics and sends nothing to its authors.

## What stays on the PC

- **Meeting audio**: it is transcribed on the PC with Whisper and is never sent anywhere.
- **Transcripts** saved in `Documents\Traduttore dal vivo` (can be turned off from the menu).
- **Settings** and **service keys**; keys are encrypted with the Windows user account.
- With **TranslateGemma** (via Ollama, on the PC) the text to translate also stays on the PC.

## What goes over the network, and when

| What | To whom | When |
|---|---|---|
| The **transcribed text** of each sentence (not the audio), with previous sentences as context for DeepL and Claude | The translation service in use: MyMemory (Translated srl), DeepL, Microsoft Translator or Anthropic (Claude) | Only if you accepted the **online services** (during installation, at first start or from the Traduzione menu) |
| The **email address** you enter for MyMemory | MyMemory, with each request | Only if you enter it |
| A fixed **test sentence** | The configured services | When you press *Prova i servizi* (test the services) |
| **Download** requests for the Whisper transcription models and the voice activity detector | Hugging Face | At first start and when you choose another transcription model |
| The **download** request for TranslateGemma | Ollama, which downloads it from its registry | When you press *Scarica il modello* (download the model) |

As with any Internet connection, these services also receive the PC's IP address. Their own privacy policies apply:
[MyMemory](https://mymemory.translated.net/doc/terms.php), [DeepL](https://www.deepl.com/privacy),
[Microsoft](https://www.microsoft.com/privacy/privacystatement), [Anthropic](https://www.anthropic.com/legal/privacy),
[Hugging Face](https://huggingface.co/privacy).

## How to turn off the online services

- **During installation**: clear *Usa i servizi di traduzione online gratuiti* (use the free online translation
  services).
- **At any time**: menu › *Traduzione* › clear *Usa i servizi online*.

With the online services turned off, the program translates only with TranslateGemma on the PC, if it is set up;
otherwise it shows only the transcript.
