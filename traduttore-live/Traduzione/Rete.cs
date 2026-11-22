using System.Net.Http;

namespace TraduttoreLive.Traduzione;

/// <summary>Il client HTTP condiviso dai servizi online e da Ollama.</summary>
static class Rete
{
    public static readonly HttpClient Http = CreaClient();

    static HttpClient CreaClient()
    {
        // Il tempo massimo si decide richiesta per richiesta (vedi InviaAsync).
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TraduttoreDalVivo/1.1");
        return client;
    }

    /// <summary>
    /// Invia una richiesta con un tempo massimo per ricevere la risposta. Gli errori di rete diventano
    /// <see cref="ServizioNonDisponibileException"/>, così si passa al servizio successivo.
    /// </summary>
    public static async Task<HttpResponseMessage> InviaAsync(HttpRequestMessage richiesta, string servizio, TimeSpan limite,
        CancellationToken ct, HttpCompletionOption opzione = HttpCompletionOption.ResponseContentRead,
        string? seNonRaggiungibile = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(limite);
        try
        {
            return await Http.SendAsync(richiesta, opzione, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ServizioNonDisponibileException($"{servizio} non risponde.", TimeSpan.FromMinutes(1));
        }
        catch (HttpRequestException ex)
        {
            throw new ServizioNonDisponibileException(seNonRaggiungibile ?? $"{servizio} non è raggiungibile ({ex.Message}).",
                TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>Il testo di una risposta di errore, accorciato per mostrarlo in un avviso.</summary>
    public static async Task<string> TestoErroreAsync(HttpResponseMessage risposta, CancellationToken ct)
    {
        try
        {
            string testo = (await risposta.Content.ReadAsStringAsync(ct)).Trim();
            if (testo.Length > 200) testo = testo[..200] + "…";
            return testo.Length > 0 ? testo : $"{(int)risposta.StatusCode} {risposta.ReasonPhrase}";
        }
        catch (Exception)
        {
            return $"{(int)risposta.StatusCode} {risposta.ReasonPhrase}";
        }
    }
}
