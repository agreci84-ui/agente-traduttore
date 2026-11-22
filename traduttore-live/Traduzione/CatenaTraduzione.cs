namespace TraduttoreLive.Traduzione;

/// <summary>
/// Traduce con più servizi, in ordine di preferenza: se uno non può tradurre (spento, quota gratuita finita,
/// chiave sbagliata, rete assente) passa al successivo e lo riprova più tardi. Tiene le ultime frasi tradotte
/// come contesto per i servizi che lo usano. Viene chiamata da un solo thread alla volta.
/// </summary>
public sealed class CatenaTraduzione
{
    const int FrasiDiContesto = 4;

    sealed class Voce(IServizioTraduzione servizio)
    {
        public IServizioTraduzione Servizio { get; } = servizio;
        public DateTime SospesoFinoA { get; set; } = DateTime.MinValue;
        public string? UltimoAvviso { get; set; }
        public bool Attivo => SospesoFinoA <= DateTime.UtcNow;
    }

    readonly List<Voce> _voci;
    readonly Queue<(string Originale, string Traduzione)> _contesto = new();
    bool _avvisatoNessuno;

    public CatenaTraduzione(IEnumerable<IServizioTraduzione> servizi, string linguaArrivo)
    {
        _voci = servizi.Select(s => new Voce(s)).ToList();
        LinguaArrivo = linguaArrivo;
    }

    public string LinguaArrivo { get; }

    /// <summary>Nome del servizio che ha tradotto l'ultima frase (null se nessuno ci è riuscito).</summary>
    public string? ServizioAttuale { get; private set; }

    /// <summary>Messaggi per l'utente: un servizio non è disponibile, si passa al successivo...</summary>
    public event Action<string>? Avviso;

    /// <summary>Cambia il servizio che sta traducendo (null = nessuno).</summary>
    public event Action<string?>? ServizioCambiato;

    public async Task<string?> TraduciAsync(string testo, string linguaOriginale, Action<string> parziale, CancellationToken ct)
    {
        var richiesta = new RichiestaTraduzione(testo, linguaOriginale, LinguaArrivo, _contesto.ToArray());
        for (int i = 0; i < _voci.Count; i++)
        {
            var voce = _voci[i];
            if (!voce.Attivo) continue;
            try
            {
                string? traduzione = await voce.Servizio.TraduciAsync(richiesta, parziale, ct);
                voce.UltimoAvviso = null;
                _avvisatoNessuno = false;
                ImpostaServizioAttuale(voce.Servizio.Nome);
                if (!string.IsNullOrWhiteSpace(traduzione))
                {
                    _contesto.Enqueue((testo, traduzione));
                    while (_contesto.Count > FrasiDiContesto) _contesto.Dequeue();
                }
                return traduzione;
            }
            catch (ServizioNonDisponibileException ex)
            {
                voce.SospesoFinoA = ex.Pausa is { } pausa ? DateTime.UtcNow + pausa : DateTime.MaxValue;
                parziale("");
                // Lo stesso problema (es. rete assente) si segnala una volta sola, non a ogni frase.
                if (ex.Message != voce.UltimoAvviso)
                {
                    voce.UltimoAvviso = ex.Message;
                    var prossimo = _voci.Skip(i + 1).FirstOrDefault(v => v.Attivo);
                    Avviso?.Invoke(prossimo is null ? ex.Message : $"{ex.Message} Passo a {prossimo.Servizio.Nome}.");
                }
            }
        }

        ImpostaServizioAttuale(null);
        if (!_avvisatoNessuno)
        {
            _avvisatoNessuno = true;
            Avviso?.Invoke("Nessun servizio di traduzione disponibile in questo momento: mostro solo la trascrizione " +
                           "e riprovo con le prossime frasi.");
        }
        return null;
    }

    void ImpostaServizioAttuale(string? nome)
    {
        if (nome == ServizioAttuale) return;
        ServizioAttuale = nome;
        ServizioCambiato?.Invoke(nome);
    }
}
