namespace TraduttoreLive.Traduzione;

/// <summary>I servizi di traduzione disponibili e la scelta di quelli da usare.</summary>
public static class Servizi
{
    public const string Automatico = "automatico";
    public const string Integrata = "integrata";
    public const string Ollama = "ollama";

    /// <summary>Le scelte del menu, con il codice salvato nelle impostazioni.</summary>
    public static readonly (string Codice, string Nome)[] Elenco =
    [
        (Automatico, "Automatica: AI integrata, poi i servizi gratuiti"),
        (Integrata, "Solo l'AI integrata (il testo non lascia il computer)"),
        (Ollama, "TranslateGemma sul PC (Ollama), gratuito"),
        (Credenziali.Gemini, "Google Gemini, AI gratuita con limite giornaliero"),
        (Credenziali.Groq, "Groq, AI gratuita con limite giornaliero"),
        (Credenziali.DeepL, "DeepL, gratuito fino a 500.000 caratteri al mese"),
        (Credenziali.Microsoft, "Microsoft Translator, gratuito fino a 2 milioni di caratteri al mese"),
        (Credenziali.MyMemory, "MyMemory, gratuito con limite giornaliero"),
        (Credenziali.Claude, "Claude (a pagamento)"),
    ];

    static bool _suggerimentoMostrato;

    /// <summary>
    /// I servizi da usare, in ordine. In automatico: l'AI integrata (se non è disattivata; la prima volta si scarica da
    /// sola), TranslateGemma in Ollama (se è avviato e il modello c'è), le AI gratuite Gemini e Groq, poi DeepL e
    /// Microsoft (se c'è la chiave), MyMemory (sempre). Claude solo se scelto, perché è a pagamento.
    /// I problemi (chiave mancante, Ollama spento...) arrivano come avvisi.
    /// </summary>
    public static async Task<List<IServizioTraduzione>> PreparaAsync(Impostazioni imp, Action<string> avviso, CancellationToken ct)
    {
        string scelta = imp.ServizioTraduzione;
        bool automatico = scelta == Automatico || Elenco.All(e => e.Codice != scelta);
        ServizioIntegrato? integrato = null;
        if ((automatico && imp.UsaAIIntegrata) || scelta == Integrata)
            integrato = new ServizioIntegrato(imp.UsaSchedaGrafica);
        else
            AIIntegrata.FermaScaricamento();

        var servizi = await AltriServiziAsync(imp, automatico, integrato is not null, avviso, ct);
        if (integrato is not null)
        {
            // Se il computer è troppo lento per usarla in diretta, passa agli altri servizi (se ce ne sono).
            integrato.PuoPassareAdAltri = servizi.Count > 0;
            servizi.Insert(0, integrato);
        }
        return servizi;
    }

    static async Task<List<IServizioTraduzione>> AltriServiziAsync(Impostazioni imp, bool automatico, bool conIntegrata,
        Action<string> avviso, CancellationToken ct)
    {
        string scelta = imp.ServizioTraduzione;
        string glossario = Interprete.LeggiGlossario(imp.FileGlossario);
        var servizi = new List<IServizioTraduzione>();

        // I servizi online ricevono il testo trascritto: si usano solo se l'utente lo ha accettato.
        bool online = imp.ServiziOnline == true;
        if (scelta == Integrata) return servizi;
        if (!online && !automatico && scelta != Ollama)
        {
            avviso("I servizi di traduzione online sono disattivati: per usarli attivali dal menu › Traduzione › " +
                   "Usa i servizi online.");
            return servizi;
        }

        if (automatico || scelta == Ollama)
        {
            var stato = await ServizioOllama.ControllaAsync(imp.OllamaIndirizzo, imp.OllamaModello, ct);
            if (stato == ServizioOllama.Stato.Pronto)
                servizi.Add(new ServizioOllama(imp.OllamaIndirizzo, imp.OllamaModello, imp.LinguaArrivo, glossario));
            else if (stato == ServizioOllama.Stato.ModelloMancante)
                avviso($"Ollama è avviato, ma manca il modello «{imp.OllamaModello}»: scaricalo dal menu › Traduzione › Servizi e chiavi.");
            else if (!automatico)
                avviso("Ollama non è avviato o non è installato: avvialo, oppure scegli un altro servizio dal menu › Traduzione.");
        }

        if (automatico && !online)
        {
            if (servizi.Count == 0 && !conIntegrata)
                avviso("Traduco solo sul computer (i servizi online sono disattivati), ma l'AI integrata è disattivata e " +
                       "TranslateGemma in Ollama non è pronto: mostro solo la trascrizione. Dal menu › Traduzione puoi attivare " +
                       "l'AI integrata o i servizi online.");
            return servizi;
        }

        foreach (var fornitore in ServizioAI.Fornitori)
        {
            if (!automatico && scelta != fornitore.Codice) continue;
            if (Credenziali.Leggi(fornitore.Codice) is { } chiave)
                servizi.Add(new ServizioAI(fornitore, chiave, ModelloAI(imp, fornitore), imp.LinguaArrivo, glossario));
            else if (!automatico)
                avviso($"Manca la chiave di {fornitore.Nome}: inseriscila dal menu › Traduzione › Servizi e chiavi.");
        }

        if (automatico || scelta == Credenziali.DeepL)
        {
            if (Credenziali.Leggi(Credenziali.DeepL) is { } chiave) servizi.Add(new ServizioDeepL(chiave));
            else if (!automatico) avviso("Manca la chiave di DeepL: inseriscila dal menu › Traduzione › Servizi e chiavi.");
        }

        if (automatico || scelta == Credenziali.Microsoft)
        {
            if (Credenziali.Leggi(Credenziali.Microsoft) is { } chiave) servizi.Add(new ServizioMicrosoft(chiave, imp.MicrosoftRegione));
            else if (!automatico) avviso("Manca la chiave di Microsoft Translator: inseriscila dal menu › Traduzione › Servizi e chiavi.");
        }

        if (automatico || scelta == Credenziali.MyMemory)
        {
            string? email = Credenziali.Leggi(Credenziali.MyMemory);
            servizi.Add(new ServizioMyMemory(email));
            if (automatico && !conIntegrata && servizi.Count == 1 && email is null && !_suggerimentoMostrato)
            {
                _suggerimentoMostrato = true;
                avviso("Traduco con MyMemory, gratuito ma limitato a circa 5.000 caratteri al giorno (pochi minuti di parlato). " +
                       "Per tradurre di più e meglio, gratis, attiva l'AI integrata dal menu › Traduzione.");
            }
        }

        if (scelta == Credenziali.Claude)
        {
            if (!Credenziali.ClaudeDisponibile())
                avviso("Manca la chiave API di Claude: inseriscila dal menu › Traduzione › Servizi e chiavi.");
            else
            {
                try
                {
                    servizi.Add(new ServizioClaude(imp.ModelloClaude, imp.LinguaArrivo, glossario));
                }
                catch (Exception ex)
                {
                    avviso($"Non riesco a preparare la traduzione con Claude: {ex.Message}");
                }
            }
        }
        return servizi;
    }

    /// <summary>Il modello scelto nelle impostazioni per un'AI gratuita.</summary>
    public static string ModelloAI(Impostazioni imp, FornitoreAI fornitore) =>
        fornitore.Codice == Credenziali.Groq ? imp.GroqModello : imp.GeminiModello;
}
