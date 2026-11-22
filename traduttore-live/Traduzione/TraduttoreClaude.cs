using System.IO;
using System.Text;
using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Traduce le frasi trascritte con Claude. A Claude arriva solo il testo, mai l'audio.
/// Le frasi precedenti vengono passate come contesto, così pronomi, termini e frasi spezzate
/// si traducono in modo coerente.
/// </summary>
public sealed class TraduttoreClaude
{
    const int FrasiDiContesto = 4;

    readonly AnthropicClient _client;
    readonly string _modello;
    readonly string _istruzioni;
    readonly Queue<(string Originale, string Traduzione)> _contesto = new();

    public string LinguaArrivo { get; }

    public TraduttoreClaude(string modello, string linguaArrivo, string fileGlossario)
    {
        // Prima la chiave inserita nel programma; se non c'è, l'SDK legge ANTHROPIC_API_KEY
        // (o il profilo di `ant auth login`).
        _client = ChiaveApi.Leggi() is { } chiave ? new AnthropicClient { ApiKey = chiave } : new AnthropicClient();
        _modello = modello;
        LinguaArrivo = linguaArrivo;
        _istruzioni = Istruzioni(Lingue.NomeInglese(linguaArrivo), LeggiGlossario(fileGlossario));
    }

    /// <summary>
    /// Traduce una frase. <paramref name="parziale"/> riceve il testo man mano che arriva.
    /// Restituisce null se Claude indica che non c'è niente da tradurre.
    /// </summary>
    public async Task<string?> TraduciAsync(string testo, string linguaOriginale, Action<string> parziale, CancellationToken ct)
    {
        var messaggio = new StringBuilder();
        if (_contesto.Count > 0)
        {
            messaggio.AppendLine("<contesto>");
            foreach (var (o, t) in _contesto)
                messaggio.AppendLine($"{o}\n→ {t}");
            messaggio.AppendLine("</contesto>");
        }
        string lingua = string.IsNullOrEmpty(linguaOriginale) ? "?" : linguaOriginale;
        messaggio.Append($"<da_tradurre lingua=\"{lingua}\">{testo}</da_tradurre>");

        var parametri = new MessageCreateParams
        {
            Model = _modello,
            MaxTokens = 4000,
            // Tradurre una frase non richiede ragionamenti lunghi: impegno basso = risposta più rapida.
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = new List<BetaTextBlockParam>
            {
                new() { Text = _istruzioni, CacheControl = new BetaCacheControlEphemeral() },
            },
            Messages = [new BetaUserMessageParam { Content = messaggio.ToString() }],
        };
        // Se il modello rifiuta una richiesta per le sue regole di sicurezza, il server la ripete su un
        // modello alternativo scelto da Anthropic invece di restituire un rifiuto. Haiku non lo supporta.
        if (!_modello.Contains("haiku", StringComparison.OrdinalIgnoreCase))
            parametri = parametri with { Betas = ["server-side-fallback-2026-07-01"], Fallbacks = new Default() };

        var risposta = new StringBuilder();
        string? motivoFine = null;
        await foreach (var evento in _client.Beta.Messages.CreateStreaming(parametri, ct))
        {
            if (evento.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var pezzo))
            {
                risposta.Append(pezzo.Text);
                parziale(risposta.ToString().Trim());
            }
            else if (evento.TryPickDelta(out var fine))
            {
                motivoFine = fine.Delta.StopReason?.ToString();
            }
        }

        if (motivoFine is not null && motivoFine.Contains("refusal", StringComparison.OrdinalIgnoreCase))
            return "[traduzione non disponibile per questa frase]";

        string traduzione = risposta.ToString().Trim();
        if (traduzione.Length == 0 || traduzione == "-") return null;

        _contesto.Enqueue((testo, traduzione));
        while (_contesto.Count > FrasiDiContesto) _contesto.Dequeue();
        return traduzione;
    }

    static string Istruzioni(string lingua, string glossario)
    {
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

    static string LeggiGlossario(string percorso)
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
