namespace TraduttoreLive.Traduzione;

/// <summary>Una frase da tradurre, con le frasi precedenti (già tradotte) come contesto.</summary>
public sealed record RichiestaTraduzione(
    string Testo,
    string LinguaOriginale,
    string LinguaArrivo,
    IReadOnlyList<(string Originale, string Traduzione)> Contesto);

/// <summary>Un servizio di traduzione, sul PC o online.</summary>
public interface IServizioTraduzione
{
    /// <summary>Nome mostrato all'utente.</summary>
    string Nome { get; }

    /// <summary>
    /// Traduce una frase. <paramref name="parziale"/> riceve il testo man mano che arriva, se il servizio lo
    /// manda a pezzi. Restituisce null se non c'è niente da tradurre. Lancia
    /// <see cref="ServizioNonDisponibileException"/> se il servizio non può tradurre adesso.
    /// </summary>
    Task<string?> TraduciAsync(RichiestaTraduzione richiesta, Action<string> parziale, CancellationToken ct);
}

/// <summary>Il servizio non può tradurre adesso (spento, quota finita, chiave sbagliata, rete): si passa al successivo.</summary>
public sealed class ServizioNonDisponibileException(string motivo, TimeSpan? pausa = null) : Exception(motivo)
{
    /// <summary>Per quanto tempo non riprovarlo; null = fino al prossimo avvio della traduzione.</summary>
    public TimeSpan? Pausa { get; } = pausa;
}
