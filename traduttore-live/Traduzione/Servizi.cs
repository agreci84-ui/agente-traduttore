namespace TraduttoreLive.Traduzione;

/// <summary>I servizi di traduzione disponibili e la scelta di quelli da usare.</summary>
public static class Servizi
{
    public const string Automatico = "automatico";
    public const string Ollama = "ollama";

    /// <summary>Le scelte del menu, con il codice salvato nelle impostazioni.</summary>
    public static readonly (string Codice, string Nome)[] Elenco =
    [
        (Automatico, "Automatica: servizi gratuiti, uno dopo l'altro"),
        (Ollama, "TranslateGemma sul PC (Ollama), gratuito"),
        (Credenziali.DeepL, "DeepL, gratuito fino a 500.000 caratteri al mese"),
        (Credenziali.Microsoft, "Microsoft Translator, gratuito fino a 2 milioni di caratteri al mese"),
        (Credenziali.MyMemory, "MyMemory, gratuito con limite giornaliero"),
        (Credenziali.Claude, "Claude (a pagamento)"),
    ];

    static bool _suggerimentoMostrato;

    /// <summary>
    /// I servizi da usare, in ordine. In automatico: TranslateGemma sul PC (se Ollama è avviato e il modello c'è),
    /// DeepL e Microsoft (se c'è la chiave), MyMemory (sempre). Claude solo se scelto, perché è a pagamento.
    /// I problemi (chiave mancante, Ollama spento...) arrivano come avvisi.
    /// </summary>
    public static async Task<List<IServizioTraduzione>> PreparaAsync(Impostazioni imp, Action<string> avviso, CancellationToken ct)
    {
        string scelta = imp.ServizioTraduzione;
        bool automatico = scelta == Automatico || Elenco.All(e => e.Codice != scelta);
        string glossario = Interprete.LeggiGlossario(imp.FileGlossario);
        var servizi = new List<IServizioTraduzione>();

        // I servizi online ricevono il testo trascritto: si usano solo se l'utente lo ha accettato.
        bool online = imp.ServiziOnline == true;
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
            if (servizi.Count == 0)
                avviso("Traduco solo sul PC (i servizi online sono disattivati), ma TranslateGemma non è pronto: mostro solo " +
                       "la trascrizione. Dal menu › Traduzione puoi configurare TranslateGemma o attivare i servizi online.");
            return servizi;
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
            if (automatico && servizi.Count == 1 && email is null && !_suggerimentoMostrato)
            {
                _suggerimentoMostrato = true;
                avviso("Traduco con MyMemory, gratuito ma limitato a circa 5.000 caratteri al giorno (pochi minuti di parlato). " +
                       "Per tradurre di più, gratis: menu › Traduzione › Servizi e chiavi.");
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
}
