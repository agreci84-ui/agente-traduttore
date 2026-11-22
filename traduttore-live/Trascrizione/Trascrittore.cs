using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace TraduttoreLive.Trascrizione;

/// <summary>Testo riconosciuto in un pezzo di parlato.</summary>
public sealed record Trascrizione(string Testo, string Lingua);

/// <summary>
/// Trascrive il parlato con Whisper, sul PC: l'audio non esce mai dal computer.
/// Prima di trascrivere controlla con Silero VAD che nel pezzo ci sia davvero voce, perché Whisper
/// sul silenzio o sulla musica tende a "inventare" frasi.
/// </summary>
public sealed partial class Trascrittore : IDisposable
{
    public static string CartellaModelli { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraduttoreLive", "modelli");

    /// <summary>Modelli proposti nel menu: più grande = più preciso ma più lento.</summary>
    public static readonly (string Nome, string Descrizione)[] Modelli =
    [
        ("tiny", "tiny – velocissimo, poco preciso (32 MB)"),
        ("base", "base – veloce (60 MB)"),
        ("small", "small – equilibrato, consigliato (190 MB)"),
        ("medium", "medium – preciso, lento senza scheda grafica (540 MB)"),
        ("large-v3-turbo", "large-v3-turbo – il più preciso, lento senza scheda grafica (575 MB)"),
    ];

    readonly WhisperFactory _fabbrica;
    readonly WhisperProcessor _processore;
    readonly WhisperVadFactory? _fabbricaVad;
    readonly WhisperVadProcessor? _vad;

    public string Modello { get; }

    /// <summary>Motore di calcolo in uso: "Vulkan" (scheda grafica) o "Cpu".</summary>
    public static string MotoreDiCalcolo => RuntimeOptions.LoadedLibrary?.ToString() ?? "?";

    Trascrittore(string modello, WhisperFactory fabbrica, WhisperProcessor processore,
                 WhisperVadFactory? fabbricaVad, WhisperVadProcessor? vad)
    {
        Modello = modello;
        _fabbrica = fabbrica;
        _processore = processore;
        _fabbricaVad = fabbricaVad;
        _vad = vad;
    }

    /// <param name="modello">tiny, base, small, medium, large-v3-turbo.</param>
    /// <param name="lingua">Codice della lingua parlata, o "auto".</param>
    /// <param name="traduciInInglese">Usa la traduzione interna di Whisper (solo verso l'inglese).</param>
    /// <param name="paroleChiave">Nomi e termini da riconoscere bene, separati da virgole.</param>
    /// <param name="secondiMassimi">Durata massima di un pezzo di parlato.</param>
    /// <param name="usaSchedaGrafica">Usa la scheda grafica (Vulkan) se disponibile; altrimenti solo il processore.</param>
    public static async Task<Trascrittore> CreaAsync(string modello, string lingua, bool traduciInInglese,
        string paroleChiave, double secondiMassimi, bool usaSchedaGrafica, IProgress<string> avanzamento, CancellationToken ct)
    {
        // Va deciso prima di caricare la libreria nativa: dopo il primo caricamento non si cambia più.
        if (!usaSchedaGrafica && RuntimeOptions.LoadedLibrary is null)
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];

        string percorso = await ScaricaSeMancaAsync(NomeFile(modello), Dimensione(modello),
            c => WhisperGgmlDownloader.Default.GetGgmlModelAsync(TipoGgml(modello), Quantizzazione(modello), c),
            $"modello di trascrizione «{modello}»", avanzamento, ct);

        avanzamento.Report($"Carico il modello «{modello}»…");
        return await Task.Run(() =>
        {
            var fabbrica = WhisperFactory.FromPath(percorso);
            var builder = fabbrica.CreateBuilder()
                .WithLanguage(string.IsNullOrWhiteSpace(lingua) ? "auto" : lingua)
                .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 2, 8))
                .WithNoContext(); // ogni frase è indipendente: evita che un errore si ripeta nelle frasi seguenti

            // Whisper elabora sempre una finestra di 30 secondi. Le nostre frasi durano al massimo
            // secondiMassimi: limitare il contesto a quella durata riduce il lavoro di circa tre volte.
            // 1500 "posizioni" = 30 s, quindi 50 per secondo, arrotondate a multipli di 64.
            int contesto = Math.Min(1500, (int)Math.Ceiling((secondiMassimi + 1) * 50 / 64.0) * 64);
            if (contesto < 1500) builder = builder.WithAudioContextSize(contesto);

            if (traduciInInglese) builder = builder.WithTranslate();
            if (!string.IsNullOrWhiteSpace(paroleChiave)) builder = builder.WithPrompt(paroleChiave.Trim());
            var processore = builder.Build();

            WhisperVadFactory? fabbricaVad = null;
            WhisperVadProcessor? vad = null;
            try
            {
                string percorsoVad = ScaricaSeMancaAsync("ggml-silero-v6.2.0.bin", 1,
                    c => WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync(SileroVadType.V6_2_0, c),
                    "rilevatore di voce", avanzamento, ct).GetAwaiter().GetResult();
                fabbricaVad = WhisperVadFactory.FromPath(percorsoVad);
                vad = fabbricaVad.CreateBuilder()
                    .WithThreshold(0.5f)
                    .WithMinSpeechDuration(TimeSpan.FromMilliseconds(200))
                    .WithThreads(2)
                    .Build();
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Senza il rilevatore di voce si trascrive comunque: restano attivi i filtri sul testo.
                vad?.Dispose();
                fabbricaVad?.Dispose();
                vad = null;
                fabbricaVad = null;
            }
            return new Trascrittore(modello, fabbrica, processore, fabbricaVad, vad);
        }, ct);
    }

    /// <summary>Trascrive un pezzo di parlato. Restituisce null se non contiene parole utili.</summary>
    public async Task<Trascrizione?> TrascriviAsync(float[] campioni, CancellationToken ct)
    {
        if (_vad is not null)
        {
            double secondiDiVoce = 0;
            foreach (var s in _vad.DetectSpeech(campioni))
                secondiDiVoce += (s.End - s.Start).TotalSeconds;
            if (secondiDiVoce < 0.25) return null;
        }

        var testo = new StringBuilder();
        string lingua = "";
        await foreach (var pezzo in _processore.ProcessAsync(campioni, ct))
        {
            testo.Append(pezzo.Text);
            if (lingua.Length == 0 && !string.IsNullOrEmpty(pezzo.Language)) lingua = pezzo.Language;
        }

        string pulito = Pulisci(testo.ToString());
        return pulito.Length == 0 ? null : new Trascrizione(pulito, lingua);
    }

    // ---- Filtri sul testo ----

    // Frasi che Whisper "inventa" sul silenzio o sulla musica: vengono dai sottotitoli dei video su cui è stato addestrato.
    static readonly string[] Allucinazioni =
    [
        "thanks for watching", "thank you for watching", "please subscribe", "like and subscribe",
        "subtitles by", "captions by", "transcribed by", "amara.org", "sottotitoli creati dalla comunità",
        "sottotitoli a cura di", "sottotitoli e revisione", "grazie per la visione", "iscriviti al canale",
        "untertitel der", "untertitel im auftrag", "sous-titres réalisés", "sous-titrage", "subtítulos realizados",
        "subtítulos por", "legendas pela comunidade", "редактор субтитров", "продолжение следует", "ご視聴ありがとうございました",
    ];

    static string Pulisci(string testo)
    {
        string t = Annotazioni().Replace(testo, " "); // [Musica], (applausi), ♪ ...
        t = Spazi().Replace(t, " ").Trim();
        if (t.Length == 0) return "";
        if (!t.Any(char.IsLetterOrDigit)) return "";

        string minuscolo = t.ToLower(CultureInfo.InvariantCulture);
        if (Allucinazioni.Any(minuscolo.Contains)) return "";
        return t;
    }

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*|[♪♫]+")]
    private static partial Regex Annotazioni();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spazi();

    // ---- Modelli ----

    static GgmlType TipoGgml(string modello) => modello switch
    {
        "tiny" => GgmlType.Tiny,
        "base" => GgmlType.Base,
        "medium" => GgmlType.Medium,
        "large-v3-turbo" => GgmlType.LargeV3Turbo,
        _ => GgmlType.Small,
    };

    static QuantizationType Quantizzazione(string modello) =>
        modello is "medium" or "large-v3-turbo" ? QuantizationType.Q5_0 : QuantizationType.Q5_1;

    static string NomeFile(string modello) =>
        $"ggml-{modello}-{Quantizzazione(modello).ToString().ToLowerInvariant()}.bin";

    static long Dimensione(string modello) => modello switch
    {
        "tiny" => 32_152_673,
        "base" => 59_707_625,
        "medium" => 540_000_000,
        "large-v3-turbo" => 574_041_195,
        _ => 190_085_487,
    };

    static async Task<string> ScaricaSeMancaAsync(string nomeFile, long dimensioneAttesa,
        Func<CancellationToken, Task<Stream>> apri, string descrizione, IProgress<string> avanzamento, CancellationToken ct)
    {
        Directory.CreateDirectory(CartellaModelli);
        string percorso = Path.Combine(CartellaModelli, nomeFile);
        if (File.Exists(percorso)) return percorso;

        string temporaneo = percorso + ".download";
        avanzamento.Report($"Scarico il {descrizione} (solo la prima volta)…");
        await using (var sorgente = await apri(ct))
        await using (var destinazione = File.Create(temporaneo))
        {
            var buffer = new byte[1 << 16];
            long letti = 0, ultimoAvviso = 0;
            int n;
            while ((n = await sorgente.ReadAsync(buffer, ct)) > 0)
            {
                await destinazione.WriteAsync(buffer.AsMemory(0, n), ct);
                letti += n;
                if (letti - ultimoAvviso > 5_000_000 && dimensioneAttesa > 1_000_000)
                {
                    ultimoAvviso = letti;
                    int perc = (int)Math.Min(99, letti * 100 / dimensioneAttesa);
                    avanzamento.Report($"Scarico il {descrizione}: {letti / 1_000_000} MB ({perc}%)…");
                }
            }
        }
        File.Move(temporaneo, percorso, overwrite: true);
        return percorso;
    }

    public void Dispose()
    {
        _processore.Dispose();
        _fabbrica.Dispose();
        _vad?.Dispose();
        _fabbricaVad?.Dispose();
    }
}
