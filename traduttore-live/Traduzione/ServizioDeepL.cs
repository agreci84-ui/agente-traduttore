using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// DeepL API (https://www.deepl.com/pro-api). Con il piano gratuito, DeepL API Free, traduce fino a 500.000
/// caratteri al mese. Le frasi precedenti vanno nel parametro «context»: aiutano la traduzione e non si pagano.
/// </summary>
public sealed class ServizioDeepL(string chiave) : IServizioTraduzione
{
    // Lingue di partenza accettate da DeepL: per le altre la si lascia riconoscere a DeepL.
    static readonly HashSet<string> Sorgenti =
    [
        "ar", "bg", "cs", "da", "de", "el", "en", "es", "et", "fi", "fr", "hu", "id", "it", "ja", "ko",
        "lt", "lv", "nb", "nl", "pl", "pt", "ro", "ru", "sk", "sl", "sv", "tr", "uk", "zh",
    ];

    static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string Nome => "DeepL";

    // Le chiavi del piano gratuito finiscono con ":fx" e usano un indirizzo diverso.
    Uri Indirizzo => new(chiave.Trim().EndsWith(":fx", StringComparison.Ordinal)
        ? "https://api-free.deepl.com/v2/translate"
        : "https://api.deepl.com/v2/translate");

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        string arrivo = r.LinguaArrivo switch
        {
            "en" => "EN-GB",
            "pt" => "PT-PT",
            "zh" => "ZH-HANS",
            var c when Sorgenti.Contains(c) => c.ToUpperInvariant(),
            _ => throw new ServizioNonDisponibileException($"DeepL non traduce in {Lingue.Nome(r.LinguaArrivo).ToLowerInvariant()}."),
        };
        var corpo = new
        {
            text = new[] { r.Testo },
            target_lang = arrivo,
            source_lang = Sorgenti.Contains(r.LinguaOriginale) ? r.LinguaOriginale.ToUpperInvariant() : null,
            context = r.Contesto.Count > 0 ? string.Join("\n", r.Contesto.Select(c => c.Originale)) : null,
        };
        using var richiesta = new HttpRequestMessage(HttpMethod.Post, Indirizzo) { Content = JsonContent.Create(corpo, options: Json) };
        richiesta.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {chiave.Trim()}");
        using var risposta = await Rete.InviaAsync(richiesta, "DeepL", TimeSpan.FromSeconds(15), ct);

        switch ((int)risposta.StatusCode)
        {
            case 403 or 401:
                throw new ServizioNonDisponibileException("DeepL rifiuta la chiave: controllala dal menu › Traduzione › Servizi e chiavi.");
            case 456:
                throw new ServizioNonDisponibileException("DeepL: i caratteri gratuiti di questo mese sono finiti.");
            case 429:
                throw new ServizioNonDisponibileException("DeepL: troppe richieste in poco tempo.", TimeSpan.FromSeconds(30));
            case >= 400 and < 500:
                throw new ServizioNonDisponibileException($"DeepL: {await Rete.TestoErroreAsync(risposta, ct)}", TimeSpan.FromMinutes(10));
            case >= 500:
                throw new ServizioNonDisponibileException("DeepL ha un problema temporaneo.", TimeSpan.FromMinutes(1));
        }

        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync(ct));
        string? testo = json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
        return string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
    }
}
