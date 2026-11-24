using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraduttoreLive.Traduzione;

/// <summary>Un servizio di AI online con un piano gratuito e un'API compatibile con quella di OpenAI.</summary>
/// <param name="Codice">Codice nelle impostazioni e nelle credenziali.</param>
/// <param name="Indirizzo">Indirizzo di base dell'API (con la barra finale).</param>
/// <param name="Ragionamento">
/// Valori di «reasoning_effort» da provare in ordine, dal più rapido: i modelli ne accettano di diversi; null = non
/// mandarlo. Tradurre una frase non richiede ragionamenti lunghi, e meno ragiona più in fretta risponde.
/// </param>
public sealed record FornitoreAI(string Codice, string Nome, string Indirizzo, string ModelloPredefinito,
    string?[] Ragionamento, string NomeImpostazioneModello);

/// <summary>
/// Traduzione con un'AI gratuita online (Google Gemini, Groq): un modello linguistico che fa da interprete come
/// Claude, usa le frasi precedenti come contesto e il glossario, ma con il piano gratuito del servizio, che ha un
/// limite di richieste al giorno. Quando lo finisce, la catena passa al servizio successivo e lo riprova più tardi.
/// </summary>
public sealed class ServizioAI : IServizioTraduzione
{
    /// <summary>Google Gemini, dal piano gratuito di Google AI Studio: la qualità migliore tra le AI gratuite.</summary>
    public static readonly FornitoreAI Gemini = new(Credenziali.Gemini, "Gemini",
        "https://generativelanguage.googleapis.com/v1beta/openai/", "gemini-flash-lite-latest",
        ["minimal", "none", "low", null], nameof(Impostazioni.GeminiModello));

    /// <summary>Groq: modelli aperti, molto veloci; non usa i testi per addestrare i modelli.</summary>
    public static readonly FornitoreAI Groq = new(Credenziali.Groq, "Groq",
        "https://api.groq.com/openai/v1/", "openai/gpt-oss-120b",
        ["low", null], nameof(Impostazioni.GroqModello));

    /// <summary>Nell'ordine in cui li usa la modalità automatica.</summary>
    public static readonly FornitoreAI[] Fornitori = [Gemini, Groq];

    static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    readonly FornitoreAI _fornitore;
    readonly string _chiave;
    readonly string _modello;
    readonly string _istruzioni;
    int _ragionamento; // indice in _fornitore.Ragionamento del primo valore accettato dal modello

    public ServizioAI(FornitoreAI fornitore, string chiave, string modello, string linguaArrivo, string glossario)
    {
        _fornitore = fornitore;
        _chiave = chiave.Trim();
        _modello = string.IsNullOrWhiteSpace(modello) ? fornitore.ModelloPredefinito : modello.Trim();
        _istruzioni = Interprete.Istruzioni(linguaArrivo, glossario);
    }

    public string Nome => _fornitore.Nome;

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        while (true)
        {
            var corpo = new
            {
                model = _modello,
                messages = new object[]
                {
                    new { role = "system", content = _istruzioni },
                    new { role = "user", content = Interprete.Messaggio(r) },
                },
                stream = true,
                max_tokens = 1024,
                reasoning_effort = _fornitore.Ragionamento[_ragionamento],
            };
            using var richiesta = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_fornitore.Indirizzo), "chat/completions"))
            {
                Content = JsonContent.Create(corpo, options: Json),
            };
            richiesta.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_chiave}");
            using var risposta = await Rete.InviaAsync(richiesta, Nome, TimeSpan.FromSeconds(20), ct,
                HttpCompletionOption.ResponseHeadersRead);

            if (!risposta.IsSuccessStatusCode)
            {
                string errore = await Rete.TestoErroreAsync(risposta, ct);
                // Il modello non accetta quel livello di ragionamento: si prova il successivo.
                if ((int)risposta.StatusCode == 400 && _ragionamento < _fornitore.Ragionamento.Length - 1
                    && (Contiene(errore, "reasoning") || Contiene(errore, "thinking") || Contiene(errore, "effort")))
                {
                    _ragionamento++;
                    continue;
                }
                throw Errore((int)risposta.StatusCode, errore, risposta.Headers.RetryAfter?.Delta);
            }
            return await LeggiRispostaAsync(risposta, parziale, ct);
        }
    }

    /// <summary>La risposta arriva a pezzi (server-sent events): «data: {json}» per ogni pezzo, poi «data: [DONE]».</summary>
    async Task<string?> LeggiRispostaAsync(HttpResponseMessage risposta, Action<string> parziale, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(40));
        var testo = new StringBuilder();
        try
        {
            using var lettore = new StreamReader(await risposta.Content.ReadAsStreamAsync(limite.Token));
            while (await lettore.ReadLineAsync(limite.Token) is { } riga)
            {
                if (!riga.StartsWith("data:", StringComparison.Ordinal)) continue;
                string dati = riga[5..].Trim();
                if (dati == "[DONE]") break;
                using var json = JsonDocument.Parse(dati);
                var radice = json.RootElement;
                if (radice.TryGetProperty("error", out var errore))
                    throw new ServizioNonDisponibileException($"{Nome}: {errore}", TimeSpan.FromMinutes(1));
                if (radice.TryGetProperty("choices", out var scelte) && scelte.GetArrayLength() > 0
                    && scelte[0].TryGetProperty("delta", out var delta)
                    && delta.TryGetProperty("content", out var pezzo) && pezzo.ValueKind == JsonValueKind.String)
                {
                    testo.Append(pezzo.GetString());
                    parziale(testo.ToString().Trim());
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ServizioNonDisponibileException($"{Nome} non risponde.", TimeSpan.FromMinutes(1));
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException)
        {
            throw new ServizioNonDisponibileException($"{Nome} ha interrotto la risposta.", TimeSpan.FromMinutes(1));
        }
        return Interprete.Risposta(testo.ToString());
    }

    ServizioNonDisponibileException Errore(int stato, string errore, TimeSpan? riprova)
    {
        string correggi = "dal menu › Traduzione › Servizi e chiavi";
        if (stato is 401 or 403 || Contiene(errore, "API key") || Contiene(errore, "API_KEY"))
            return new($"{Nome} rifiuta la chiave: controllala {correggi}.");
        if (Contiene(errore, "location is not supported") || Contiene(errore, "not available in your country"))
            return new($"{Nome}: il piano gratuito non è disponibile nel tuo paese.");
        if (stato == 404)
            return new($"{Nome}: il modello «{_modello}» non esiste o non è disponibile: correggi «{_fornitore.NomeImpostazioneModello}» " +
                       "nelle impostazioni.");
        if (stato == 429)
        {
            // Si distingue il limite al giorno (si riprova tra un'ora) da quello al minuto (tra pochi secondi).
            if (Contiene(errore, "per day") || Contiene(errore, "PerDay") || Contiene(errore, "daily")
                || Contiene(errore, "(TPD)") || Contiene(errore, "(RPD)"))
                return new($"{Nome}: le traduzioni gratuite di oggi sono finite.", TimeSpan.FromHours(1));
            var pausa = riprova is { } r && r > TimeSpan.Zero && r < TimeSpan.FromMinutes(10) ? r : TimeSpan.FromSeconds(30);
            return new($"{Nome}: troppe richieste in poco tempo.", pausa);
        }
        if (stato >= 500)
            return new($"{Nome} ha un problema temporaneo.", TimeSpan.FromMinutes(1));
        return new($"{Nome}: {errore}", TimeSpan.FromMinutes(10));
    }

    static bool Contiene(string testo, string parte) => testo.Contains(parte, StringComparison.OrdinalIgnoreCase);
}
