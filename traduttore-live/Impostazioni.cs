using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraduttoreLive;

/// <summary>
/// Impostazioni del programma, salvate in %APPDATA%\TraduttoreLive\impostazioni.json.
/// Si cambiano dal menu della finestra (clic destro); il file si può anche modificare a mano
/// a programma chiuso.
/// </summary>
public sealed class Impostazioni
{
    /// <summary>Lingua in cui mostrare i sottotitoli (codice ISO 639-1: "it", "en", "de"...).</summary>
    public string LinguaArrivo { get; set; } = "it";

    /// <summary>Lingua parlata nella riunione: "auto" per riconoscerla frase per frase.</summary>
    public string LinguaParlata { get; set; } = "auto";

    /// <summary>
    /// Cosa ascoltare: "dispositivo" = tutto ciò che esce dalle casse o dalle cuffie;
    /// "programma" = solo l'audio di un programma (es. Teams), indicato in <see cref="NomeProgramma"/>.
    /// </summary>
    public string Sorgente { get; set; } = "dispositivo";

    /// <summary>Id del dispositivo di uscita da ascoltare; null = dispositivo predefinito di Windows.</summary>
    public string? DispositivoId { get; set; }

    /// <summary>Nome del processo da ascoltare quando <see cref="Sorgente"/> è "programma" (es. "ms-teams").</summary>
    public string? NomeProgramma { get; set; }

    /// <summary>Modello di trascrizione Whisper: tiny, base, small, medium, large-v3-turbo.</summary>
    public string ModelloWhisper { get; set; } = "small";

    /// <summary>Usa la scheda grafica (Vulkan) per la trascrizione, se disponibile. Cambia al riavvio del programma.</summary>
    public bool UsaSchedaGrafica { get; set; } = true;

    /// <summary>Modello di Claude usato per tradurre.</summary>
    public string ModelloClaude { get; set; } = "claude-haiku-5-5";

    /// <summary>La chiave API è già stata chiesta al primo avvio: non la si chiede più da sola.</summary>
    public bool ChiaveApiChiesta { get; set; }

    /// <summary>
    /// Parole che Whisper deve riconoscere bene (nomi di persone, prodotti, sigle), separate da virgole.
    /// </summary>
    public string ParoleChiave { get; set; } = "";

    /// <summary>
    /// File con il glossario (Markdown o testo) da rispettare nelle traduzioni. Vuoto = nessuno.
    /// </summary>
    public string FileGlossario { get; set; } = "";

    /// <summary>Silenzio (in millisecondi) dopo il quale una frase si considera finita.</summary>
    public int PausaFineFraseMs { get; set; } = 550;

    /// <summary>Durata massima di un pezzo di parlato prima di tradurlo comunque (in secondi).</summary>
    public int DurataMassimaFraseS { get; set; } = 9;

    public bool MostraOriginale { get; set; } = true;
    public bool SalvaTrascrizione { get; set; } = true;
    public double DimensioneTesto { get; set; } = 24;

    // Posizione e dimensione della finestra dei sottotitoli.
    public double? FinestraSinistra { get; set; }
    public double? FinestraAlto { get; set; }
    public double FinestraLarghezza { get; set; } = 900;
    public double FinestraAltezza { get; set; } = 240;

    [JsonIgnore]
    public static string Cartella { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TraduttoreLive");

    [JsonIgnore]
    static string Percorso => Path.Combine(Cartella, "impostazioni.json");

    static readonly JsonSerializerOptions OpzioniJson = new() { WriteIndented = true };

    public static Impostazioni Carica()
    {
        try
        {
            if (File.Exists(Percorso))
                return JsonSerializer.Deserialize<Impostazioni>(File.ReadAllText(Percorso)) ?? new();
        }
        catch (Exception)
        {
            // File rovinato o scritto a mano in modo errato: si riparte dalle impostazioni predefinite.
        }
        return new();
    }

    public void Salva()
    {
        try
        {
            Directory.CreateDirectory(Cartella);
            File.WriteAllText(Percorso, JsonSerializer.Serialize(this, OpzioniJson));
        }
        catch (Exception)
        {
            // Non poter salvare le preferenze non deve fermare la traduzione.
        }
    }
}
