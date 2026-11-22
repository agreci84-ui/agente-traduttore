# Componenti di terze parti / Third-party components

Traduttore dal vivo è distribuito con licenza MIT (vedi [LICENSE](LICENSE)). Usa questi componenti open source,
ciascuno con la propria licenza.

*Traduttore dal vivo is released under the MIT license (see [LICENSE](LICENSE)). It uses the following open source
components, each under its own license.*

## Inclusi nel programma e nell'installer / Included in the program and installer

| Componente | Licenza | Progetto |
|---|---|---|
| .NET runtime e Windows Presentation Foundation | MIT | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |
| NAudio (NAudio, NAudio.Core, NAudio.Wasapi, NAudio.WinMM, NAudio.Midi) | MIT | https://github.com/naudio/NAudio |
| Whisper.net e le sue librerie native (whisper.cpp, ggml) | MIT | https://github.com/sandrohanea/whisper.net, https://github.com/ggml-org/whisper.cpp |
| Anthropic C# SDK | Apache-2.0 | https://github.com/anthropics/anthropic-sdk-csharp |
| Inno Setup (programma di installazione e disinstallazione) | Inno Setup License | https://jrsoftware.org/isinfo.php |

## Scaricati durante l'uso / Downloaded at runtime

Non sono inclusi nell'installer: il programma li scarica quando servono.

*Not included in the installer: the program downloads them when needed.*

| Componente | Licenza | Da dove |
|---|---|---|
| Modelli di trascrizione Whisper (OpenAI), in formato ggml | MIT | https://huggingface.co/sandrohanea/whisper.net |
| Rilevatore di voce Silero VAD | MIT | https://huggingface.co/sandrohanea/whisper.net (modello originale: https://github.com/snakers4/silero-vad) |
| TranslateGemma (Google), solo se scegli di scaricarlo con Ollama | [Gemma Terms of Use](https://ai.google.dev/gemma/terms) | https://ollama.com/library/translategemma |

Ollama è un programma separato (licenza MIT, https://github.com/ollama/ollama): non è incluso, lo installi tu se
vuoi tradurre sul PC.

## Servizi online / Online services

Se li usi, valgono i termini e le informative di ciascun servizio:
[MyMemory](https://mymemory.translated.net/doc/terms.php),
[DeepL](https://www.deepl.com/privacy),
[Microsoft Translator](https://www.microsoft.com/privacy/privacystatement),
[Anthropic (Claude)](https://www.anthropic.com/legal/privacy).
