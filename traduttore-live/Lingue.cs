namespace TraduttoreLive;

/// <summary>Lingue proposte nei menu. Whisper ne riconosce molte di più: si possono impostare a mano nel file delle impostazioni.</summary>
public static class Lingue
{
    public static readonly (string Codice, string Nome)[] Elenco =
    [
        ("it", "Italiano"),
        ("en", "Inglese"),
        ("es", "Spagnolo"),
        ("fr", "Francese"),
        ("de", "Tedesco"),
        ("pt", "Portoghese"),
        ("nl", "Olandese"),
        ("pl", "Polacco"),
        ("ro", "Rumeno"),
        ("ru", "Russo"),
        ("uk", "Ucraino"),
        ("tr", "Turco"),
        ("ar", "Arabo"),
        ("zh", "Cinese"),
        ("ja", "Giapponese"),
        ("ko", "Coreano"),
        ("hi", "Hindi"),
    ];

    /// <summary>Nome italiano della lingua, o il codice stesso se non è nell'elenco.</summary>
    public static string Nome(string codice) =>
        Elenco.FirstOrDefault(l => l.Codice == codice).Nome ?? codice;

    /// <summary>Nome inglese, usato nelle istruzioni a Claude.</summary>
    public static string NomeInglese(string codice) => codice switch
    {
        "it" => "Italian",
        "en" => "English",
        "es" => "Spanish",
        "fr" => "French",
        "de" => "German",
        "pt" => "Portuguese",
        "nl" => "Dutch",
        "pl" => "Polish",
        "ro" => "Romanian",
        "ru" => "Russian",
        "uk" => "Ukrainian",
        "tr" => "Turkish",
        "ar" => "Arabic",
        "zh" => "Chinese (Simplified)",
        "ja" => "Japanese",
        "ko" => "Korean",
        "hi" => "Hindi",
        _ => NomeDaCultura(codice),
    };

    // Per le lingue fuori elenco riconosciute da Whisper (es. "sv" → "Swedish").
    static string NomeDaCultura(string codice)
    {
        if (string.IsNullOrWhiteSpace(codice)) return codice;
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(codice).EnglishName;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return codice;
        }
    }
}
