using System.IO;
using System.Text;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Istruzioni e messaggio per i servizi che sono modelli linguistici generici (Claude, o un modello di chat in
/// Ollama): fanno da interprete, usano le frasi precedenti come contesto e rispettano il glossario.
/// </summary>
public static class Interprete
{
    public static string Istruzioni(string linguaArrivo, string glossario)
    {
        string lingua = Lingue.NomeInglese(linguaArrivo);
        var s = new StringBuilder($"""
            Sei l'interprete di una riunione online. Ricevi frasi trascritte automaticamente dal parlato
            e le traduci in {lingua} per i sottotitoli che un partecipante legge in diretta.
            Le trascrizioni possono contenere parole riconosciute male, frasi spezzate a metà,
            ripetizioni ed esitazioni.

            Regole:
            - Rispondi solo con la traduzione del testo dentro <da_tradurre>: niente virgolette, note,
              spiegazioni o alternative.
            - <contesto> contiene le frasi precedenti con la loro traduzione: serve solo a capire il senso
              (pronomi, termini, frasi spezzate) e a usare sempre gli stessi termini. Non tradurlo di nuovo.
            - Traduci il significato in modo naturale e scorrevole, come parlerebbe un madrelingua.
              Togli esitazioni e ripetizioni ("ehm", "you know", parole ripetute).
            - Se una parola sembra riconosciuta male, usa l'interpretazione più probabile nel contesto della riunione.
            - Lascia invariati nomi di persone, aziende e prodotti, sigle, numeri e codici.
            - Se la frase è incompleta, traduci la parte che c'è senza completarla.
            - Se il testo è già in {lingua}, riscrivilo così com'è, correggendo solo errori evidenti di trascrizione.
            - Se non c'è niente da tradurre (solo rumore o parole senza senso), rispondi con un solo trattino: -
            """);
        if (glossario.Length > 0)
        {
            s.AppendLine();
            s.AppendLine();
            s.AppendLine("Glossario della riunione: usa sempre queste traduzioni, prevalgono sulle tue scelte.");
            s.AppendLine("<glossario>");
            s.AppendLine(glossario);
            s.Append("</glossario>");
        }
        return s.ToString();
    }

    /// <summary>Il messaggio con il contesto e la frase da tradurre.</summary>
    public static string Messaggio(RichiestaTraduzione r)
    {
        var messaggio = new StringBuilder();
        if (r.Contesto.Count > 0)
        {
            messaggio.AppendLine("<contesto>");
            foreach (var (o, t) in r.Contesto)
                messaggio.AppendLine($"{o}\n→ {t}");
            messaggio.AppendLine("</contesto>");
        }
        string lingua = string.IsNullOrEmpty(r.LinguaOriginale) ? "?" : r.LinguaOriginale;
        messaggio.Append($"<da_tradurre lingua=\"{lingua}\">{r.Testo}</da_tradurre>");
        return messaggio.ToString();
    }

    /// <summary>La risposta del modello: null se ha indicato che non c'è niente da tradurre.</summary>
    public static string? Risposta(string testo)
    {
        string t = testo.Trim();
        return t.Length == 0 || t == "-" ? null : t;
    }

    public static string LeggiGlossario(string percorso)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(percorso) && File.Exists(percorso))
            {
                string testo = File.ReadAllText(percorso).Trim();
                return testo.Length > 20_000 ? testo[..20_000] : testo;
            }
        }
        catch (Exception)
        {
            // Glossario illeggibile: si traduce senza.
        }
        return "";
    }
}
