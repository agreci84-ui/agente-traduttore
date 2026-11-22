using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// MyMemory (https://mymemory.translated.net): gratuito e senza registrazione, ma con un limite giornaliero
/// (circa 5.000 caratteri al giorno; 50.000 indicando un indirizzo email). Accetta al massimo 500 byte per
/// richiesta: le frasi più lunghe si dividono.
/// </summary>
public sealed class ServizioMyMemory(string? email) : IServizioTraduzione
{
    const int ByteMassimi = 450;

    public string Nome => "MyMemory";

    static string Codice(string lingua) => lingua switch
    {
        "" or "?" => "Autodetect",
        "zh" => "zh-CN",
        _ => lingua,
    };

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        string coppia = $"{Codice(r.LinguaOriginale)}|{Codice(r.LinguaArrivo)}";
        var risultato = new StringBuilder();
        foreach (string pezzo in Dividi(r.Testo.Trim()))
        {
            string indirizzo = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(pezzo)}" +
                               $"&langpair={Uri.EscapeDataString(coppia)}" +
                               (string.IsNullOrWhiteSpace(email) ? "" : $"&de={Uri.EscapeDataString(email.Trim())}");
            using var richiesta = new HttpRequestMessage(HttpMethod.Get, indirizzo);
            using var risposta = await Rete.InviaAsync(richiesta, "MyMemory", TimeSpan.FromSeconds(15), ct);
            if ((int)risposta.StatusCode >= 500)
                throw new ServizioNonDisponibileException("MyMemory ha un problema temporaneo.", TimeSpan.FromMinutes(1));

            using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync(ct));
            var radice = json.RootElement;
            int stato = radice.TryGetProperty("responseStatus", out var s)
                ? s.ValueKind == JsonValueKind.Number ? s.GetInt32() : int.TryParse(s.GetString(), out int n) ? n : 0
                : (int)risposta.StatusCode;
            bool quotaFinita = radice.TryGetProperty("quotaFinished", out var q) && q.ValueKind == JsonValueKind.True;
            string testo = radice.TryGetProperty("responseData", out var dati) && dati.ValueKind == JsonValueKind.Object
                           && dati.TryGetProperty("translatedText", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "" : "";

            if (quotaFinita || stato == 429 || testo.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
                throw new ServizioNonDisponibileException(string.IsNullOrWhiteSpace(email)
                    ? "MyMemory: le traduzioni gratuite di oggi sono finite (indicando un'email nei servizi il limite sale a 50.000 caratteri al giorno)."
                    : "MyMemory: le traduzioni gratuite di oggi sono finite.", TimeSpan.FromHours(1));
            if (stato != 200)
            {
                string dettagli = radice.TryGetProperty("responseDetails", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() ?? "" : "";
                throw new ServizioNonDisponibileException($"MyMemory: {(dettagli.Length > 0 ? dettagli : $"errore {stato}")}",
                    TimeSpan.FromMinutes(10));
            }

            if (risultato.Length > 0) risultato.Append(' ');
            risultato.Append(WebUtility.HtmlDecode(testo).Trim());
            parziale(risultato.ToString());
        }
        return risultato.Length > 0 ? risultato.ToString() : null;
    }

    /// <summary>Divide il testo in pezzi di al massimo <see cref="ByteMassimi"/> byte, dopo un punto o uno spazio.</summary>
    static IEnumerable<string> Dividi(string testo)
    {
        while (Encoding.UTF8.GetByteCount(testo) > ByteMassimi)
        {
            int limite = testo.Length;
            while (Encoding.UTF8.GetByteCount(testo.AsSpan(0, limite)) > ByteMassimi) limite--;
            int taglio = testo.LastIndexOfAny(['.', '!', '?', ';'], limite - 1, limite);
            if (taglio < limite / 2) taglio = testo.LastIndexOf(' ', limite - 1, limite);
            if (taglio <= 0) taglio = limite - 1;
            yield return testo[..(taglio + 1)].Trim();
            testo = testo[(taglio + 1)..].TrimStart();
        }
        if (testo.Length > 0) yield return testo;
    }
}
