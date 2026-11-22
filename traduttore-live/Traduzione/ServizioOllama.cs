using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Traduzione sul PC con Ollama (https://ollama.com), gratuita, senza limiti e senza inviare niente in rete.
/// Con TranslateGemma, il modello di traduzione aperto di Google, usa il formato di richiesta previsto da quel
/// modello; con un modello di chat generico (gemma3, qwen3...) usa le istruzioni da interprete, con contesto
/// e glossario.
/// </summary>
public sealed class ServizioOllama : IServizioTraduzione
{
    public enum Stato { NonRaggiungibile, ModelloMancante, Pronto }

    readonly Uri _base;
    readonly string _modello;
    readonly string _istruzioni;

    public ServizioOllama(string indirizzo, string modello, string linguaArrivo, string glossario)
    {
        _base = Indirizzo(indirizzo);
        _modello = modello.Trim();
        _istruzioni = UsaTranslateGemma(_modello) ? "" : Interprete.Istruzioni(linguaArrivo, glossario);
    }

    public string Nome => UsaTranslateGemma(_modello) ? "TranslateGemma sul PC" : $"{_modello} sul PC";

    static bool UsaTranslateGemma(string modello) => modello.Contains("translategemma", StringComparison.OrdinalIgnoreCase);

    static Uri Indirizzo(string indirizzo) => new(indirizzo.Trim().TrimEnd('/') + "/");

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        object[] messaggi = UsaTranslateGemma(_modello)
            ? [new { role = "user", content = RichiestaTranslateGemma(r) }]
            : [new { role = "system", content = _istruzioni }, new { role = "user", content = Interprete.Messaggio(r) }];
        var corpo = new
        {
            model = _modello,
            messages = messaggi,
            stream = true,
            keep_alive = "30m", // il modello resta in memoria tra una frase e l'altra
            options = new { temperature = 0 },
        };
        using var richiesta = new HttpRequestMessage(HttpMethod.Post, new Uri(_base, "api/chat")) { Content = JsonContent.Create(corpo) };
        // Alla prima frase Ollama carica il modello in memoria: può volerci un po'.
        using var risposta = await Rete.InviaAsync(richiesta, "Ollama", TimeSpan.FromMinutes(3), ct,
            HttpCompletionOption.ResponseHeadersRead, "Ollama non è avviato o non è installato.");
        if (risposta.StatusCode == HttpStatusCode.NotFound)
            throw new ServizioNonDisponibileException(
                $"In Ollama manca il modello «{_modello}»: scaricalo dal menu › Traduzione › Servizi e chiavi.");
        if (!risposta.IsSuccessStatusCode)
            throw new ServizioNonDisponibileException($"Ollama: {await Rete.TestoErroreAsync(risposta, ct)}", TimeSpan.FromMinutes(1));

        var testo = new StringBuilder();
        using var lettore = new StreamReader(await risposta.Content.ReadAsStreamAsync(ct));
        while (await lettore.ReadLineAsync(ct) is { } riga)
        {
            if (riga.Length == 0) continue;
            using var json = JsonDocument.Parse(riga);
            var radice = json.RootElement;
            if (radice.TryGetProperty("error", out var errore))
                throw new ServizioNonDisponibileException($"Ollama: {errore.GetString()}", TimeSpan.FromMinutes(1));
            if (radice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var pezzo))
            {
                testo.Append(pezzo.GetString());
                parziale(testo.ToString().Trim());
            }
            if (radice.TryGetProperty("done", out var fatto) && fatto.GetBoolean()) break;
        }
        return UsaTranslateGemma(_modello) ? NullSeVuoto(testo.ToString()) : Interprete.Risposta(testo.ToString());
    }

    static string? NullSeVuoto(string testo) => string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();

    /// <summary>Il messaggio nel formato indicato da Google per TranslateGemma (un solo messaggio, niente istruzioni di sistema).</summary>
    static string RichiestaTranslateGemma(RichiestaTraduzione r)
    {
        string a = NomeLingua(r.LinguaArrivo), codA = CodiceLingua(r.LinguaArrivo);
        if (string.IsNullOrWhiteSpace(r.LinguaOriginale))
            return $"You are a professional translator into {a} ({codA}). Your goal is to accurately convey the meaning " +
                   $"and nuances of the original text while adhering to {a} grammar, vocabulary, and cultural sensitivities.\n" +
                   $"Produce only the {a} translation, without any additional explanations or commentary. " +
                   $"Please translate the following text into {a}:\n\n\n{r.Testo.Trim()}";
        string da = NomeLingua(r.LinguaOriginale), codDa = CodiceLingua(r.LinguaOriginale);
        return $"You are a professional {da} ({codDa}) to {a} ({codA}) translator. Your goal is to accurately convey the " +
               $"meaning and nuances of the original {da} text while adhering to {a} grammar, vocabulary, and cultural sensitivities.\n" +
               $"Produce only the {a} translation, without any additional explanations or commentary. " +
               $"Please translate the following {da} text into {a}:\n\n\n{r.Testo.Trim()}";
    }

    static string NomeLingua(string codice) => codice == "zh" ? "Chinese" : Lingue.NomeInglese(codice);
    static string CodiceLingua(string codice) => codice == "zh" ? "zh-Hans" : codice;

    // ---- Controllo e scaricamento del modello (finestra dei servizi) ----

    /// <summary>Ollama è avviato? Il modello è già scaricato?</summary>
    public static async Task<Stato> ControllaAsync(string indirizzo, string modello, CancellationToken ct)
    {
        try
        {
            using var richiesta = new HttpRequestMessage(HttpMethod.Get, new Uri(Indirizzo(indirizzo), "api/tags"));
            using var risposta = await Rete.InviaAsync(richiesta, "Ollama", TimeSpan.FromSeconds(3), ct);
            if (!risposta.IsSuccessStatusCode) return Stato.NonRaggiungibile;
            using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync(ct));
            string cercato = modello.Trim().Contains(':') ? modello.Trim() : modello.Trim() + ":latest";
            foreach (var m in json.RootElement.GetProperty("models").EnumerateArray())
            {
                string? nome = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.Equals(nome, cercato, StringComparison.OrdinalIgnoreCase)) return Stato.Pronto;
            }
            return Stato.ModelloMancante;
        }
        catch (Exception ex) when (ex is ServizioNonDisponibileException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or UriFormatException)
        {
            return Stato.NonRaggiungibile;
        }
    }

    /// <summary>Fa scaricare il modello a Ollama. L'avanzamento riporta lo stato e, quando noto, la percentuale.</summary>
    public static async Task ScaricaModelloAsync(string indirizzo, string modello, IProgress<(string Stato, double? Percentuale)> avanzamento,
        CancellationToken ct)
    {
        using var richiesta = new HttpRequestMessage(HttpMethod.Post, new Uri(Indirizzo(indirizzo), "api/pull"))
        {
            Content = JsonContent.Create(new { model = modello.Trim(), stream = true }),
        };
        using var risposta = await Rete.InviaAsync(richiesta, "Ollama", TimeSpan.FromSeconds(30), ct,
            HttpCompletionOption.ResponseHeadersRead, "Ollama non è avviato o non è installato.");
        if (!risposta.IsSuccessStatusCode)
            throw new ServizioNonDisponibileException($"Ollama: {await Rete.TestoErroreAsync(risposta, ct)}");

        using var lettore = new StreamReader(await risposta.Content.ReadAsStreamAsync(ct));
        while (await lettore.ReadLineAsync(ct) is { } riga)
        {
            if (riga.Length == 0) continue;
            using var json = JsonDocument.Parse(riga);
            var radice = json.RootElement;
            if (radice.TryGetProperty("error", out var errore))
                throw new ServizioNonDisponibileException($"Ollama: {errore.GetString()}");
            string stato = radice.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            double? percentuale = null;
            if (radice.TryGetProperty("total", out var totale) && radice.TryGetProperty("completed", out var fatti)
                && totale.GetInt64() > 0)
                percentuale = 100.0 * fatti.GetInt64() / totale.GetInt64();
            avanzamento.Report((stato, percentuale));
            if (stato == "success") return;
        }
        throw new ServizioNonDisponibileException("Ollama ha interrotto lo scaricamento del modello.");
    }
}
