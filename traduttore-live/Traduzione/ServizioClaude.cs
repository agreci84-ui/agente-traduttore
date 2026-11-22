using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Traduzione con Claude tramite l'API di Anthropic: a pagamento, si usa solo se la scegli dal menu
/// (mai nella modalità automatica, che usa solo servizi gratuiti).
/// </summary>
public sealed class ServizioClaude : IServizioTraduzione
{
    readonly AnthropicClient _client;
    readonly string _modello;
    readonly string _istruzioni;

    /// <param name="chiave">Chiave da usare; null = quella salvata nel programma.</param>
    public ServizioClaude(string modello, string linguaArrivo, string glossario, string? chiave = null)
    {
        // Prima la chiave inserita nel programma; se non c'è, l'SDK legge ANTHROPIC_API_KEY
        // (o il profilo di `ant auth login`).
        _client = (chiave ?? Credenziali.Leggi(Credenziali.Claude)) is { } k ? new AnthropicClient { ApiKey = k } : new AnthropicClient();
        _modello = modello;
        _istruzioni = Interprete.Istruzioni(linguaArrivo, glossario);
    }

    public string Nome => "Claude";

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
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
            Messages = [new BetaUserMessageParam { Content = Interprete.Messaggio(r) }],
        };
        // Se il modello rifiuta una richiesta per le sue regole di sicurezza, il server la ripete su un
        // modello alternativo scelto da Anthropic invece di restituire un rifiuto. Haiku non lo supporta.
        if (!_modello.Contains("haiku", StringComparison.OrdinalIgnoreCase))
            parametri = parametri with { Betas = ["server-side-fallback-2026-07-01"], Fallbacks = new Default() };

        var risposta = new StringBuilder();
        string? motivoFine = null;
        try
        {
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
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new ServizioNonDisponibileException("Claude rifiuta la chiave API: controllala dal menu › Traduzione › Servizi e chiavi.");
        }
        catch (AnthropicForbiddenException)
        {
            throw new ServizioNonDisponibileException("La chiave API di Claude non ha accesso a questo modello o all'organizzazione.");
        }
        catch (AnthropicNotFoundException)
        {
            throw new ServizioNonDisponibileException($"Il modello di Claude «{_modello}» non esiste: correggi «ModelloClaude» nelle impostazioni.");
        }
        catch (AnthropicRateLimitException)
        {
            throw new ServizioNonDisponibileException("Claude: troppe richieste in poco tempo.", TimeSpan.FromSeconds(30));
        }
        catch (AnthropicIOException)
        {
            throw new ServizioNonDisponibileException("Claude non è raggiungibile: controlla la connessione a Internet.", TimeSpan.FromMinutes(1));
        }
        catch (AnthropicApiException ex)
        {
            throw new ServizioNonDisponibileException($"Claude ha restituito un errore: {ex.Message}", TimeSpan.FromMinutes(1));
        }

        if (motivoFine is not null && motivoFine.Contains("refusal", StringComparison.OrdinalIgnoreCase))
            return "[traduzione non disponibile per questa frase]";
        return Interprete.Risposta(risposta.ToString());
    }
}
