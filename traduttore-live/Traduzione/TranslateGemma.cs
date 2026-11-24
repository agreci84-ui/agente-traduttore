namespace TraduttoreLive.Traduzione;

/// <summary>
/// La richiesta nel formato indicato da Google per TranslateGemma: un solo messaggio, niente istruzioni di sistema,
/// il testo da tradurre in fondo. La usano l'AI integrata e TranslateGemma in Ollama.
/// </summary>
public static class TranslateGemma
{
    /// <summary>Tutto quello che viene prima del testo da tradurre (cambia solo con la coppia di lingue).</summary>
    public static string Istruzioni(string linguaOriginale, string linguaArrivo)
    {
        string a = NomeLingua(linguaArrivo), codA = CodiceLingua(linguaArrivo);
        if (string.IsNullOrWhiteSpace(linguaOriginale))
            return $"You are a professional translator into {a} ({codA}). Your goal is to accurately convey the meaning " +
                   $"and nuances of the original text while adhering to {a} grammar, vocabulary, and cultural sensitivities.\n" +
                   $"Produce only the {a} translation, without any additional explanations or commentary. " +
                   $"Please translate the following text into {a}:\n\n\n";
        string da = NomeLingua(linguaOriginale), codDa = CodiceLingua(linguaOriginale);
        return $"You are a professional {da} ({codDa}) to {a} ({codA}) translator. Your goal is to accurately convey the " +
               $"meaning and nuances of the original {da} text while adhering to {a} grammar, vocabulary, and cultural sensitivities.\n" +
               $"Produce only the {a} translation, without any additional explanations or commentary. " +
               $"Please translate the following {da} text into {a}:\n\n\n";
    }

    static string NomeLingua(string codice) => codice == "zh" ? "Chinese" : Lingue.NomeInglese(codice);
    static string CodiceLingua(string codice) => codice == "zh" ? "zh-Hans" : codice;
}
