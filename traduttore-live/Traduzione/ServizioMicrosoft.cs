using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Microsoft Translator (Azure AI Translator). Con il piano gratuito F0 traduce fino a 2 milioni di caratteri
/// al mese. Serve una risorsa Translator creata nel portale di Azure: chiave e, per le risorse regionali, regione.
/// </summary>
public sealed class ServizioMicrosoft(string chiave, string regione) : IServizioTraduzione
{
    public string Nome => "Microsoft Translator";

    static string Codice(string lingua) => lingua == "zh" ? "zh-Hans" : lingua;

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        // La lingua di partenza si indica solo se è tra quelle note; altrimenti la riconosce Microsoft.
        bool partenzaNota = Lingue.Elenco.Any(l => l.Codice == r.LinguaOriginale);
        string indirizzo = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0" +
                           $"&to={Uri.EscapeDataString(Codice(r.LinguaArrivo))}" +
                           (partenzaNota ? $"&from={Uri.EscapeDataString(Codice(r.LinguaOriginale))}" : "");
        using var richiesta = new HttpRequestMessage(HttpMethod.Post, indirizzo)
        {
            Content = JsonContent.Create(new[] { new { Text = r.Testo } }),
        };
        richiesta.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", chiave.Trim());
        if (!string.IsNullOrWhiteSpace(regione))
            richiesta.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", regione.Trim());
        using var risposta = await Rete.InviaAsync(richiesta, "Microsoft Translator", TimeSpan.FromSeconds(15), ct);

        switch ((int)risposta.StatusCode)
        {
            case 401:
                throw new ServizioNonDisponibileException(
                    "Microsoft Translator rifiuta la chiave o la regione: controllale dal menu › Traduzione › Servizi e chiavi.");
            case 403:
                throw new ServizioNonDisponibileException("Microsoft Translator: i caratteri gratuiti di questo mese sono finiti.");
            case 429:
                throw new ServizioNonDisponibileException("Microsoft Translator: troppe richieste in poco tempo.", TimeSpan.FromMinutes(1));
            case >= 400 and < 500:
                throw new ServizioNonDisponibileException($"Microsoft Translator: {await Rete.TestoErroreAsync(risposta, ct)}",
                    TimeSpan.FromMinutes(10));
            case >= 500:
                throw new ServizioNonDisponibileException("Microsoft Translator ha un problema temporaneo.", TimeSpan.FromMinutes(1));
        }

        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync(ct));
        string? testo = json.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString();
        return string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
    }
}
