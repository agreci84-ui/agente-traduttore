using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
#if WINDOWS
using NAudio.CoreAudioApi;
#endif
using TraduttoreLive.Audio;
using TraduttoreLive.Trascrizione;
using TraduttoreLive.Traduzione;

namespace TraduttoreLive;

/// <summary>
/// Collega i pezzi: audio del PC → frasi → trascrizione (Whisper, sul PC) → traduzione (uno o più servizi,
/// vedi <see cref="CatenaTraduzione"/>). Trascrizione e traduzione lavorano in parallelo: mentre una frase
/// viene tradotta, Whisper trascrive già la successiva. Gli eventi arrivano da thread in background.
/// </summary>
public sealed class Motore : IAsyncDisposable
{
    public event Action<string>? StatoCambiato;
    public event Action<RigaSottotitolo>? NuovaRiga;

    const int Hz = ConvertitoreAudio.FrequenzaUscita;

    readonly Impostazioni _imp;
    readonly RegistroRiunione _registro;
    readonly CancellationTokenSource _cts = new();
    readonly Channel<SegmentoAudio> _codaAudio = Channel.CreateUnbounded<SegmentoAudio>(new() { SingleReader = true });
    readonly Channel<RigaSottotitolo> _codaTraduzione = Channel.CreateUnbounded<RigaSottotitolo>(new() { SingleReader = true });
    readonly SegmentatoreVoce _segmentatore;
    readonly SemaphoreSlim _semaforoCattura = new(1, 1);

    CatturaAudio? _cattura;
    int? _pidProgramma;
    Trascrittore? _trascrittore;
    volatile CatenaTraduzione? _traduzione;
    bool _whisperTraduceInInglese;
#if WINDOWS
    MMDeviceEnumerator? _enumeratore;
    MMDeviceNotificationClient? _notifiche;
#endif
    Timer? _sorveglianza;
    int _sorveglianzaInCorso;
    DateTime _ultimoAvvioCattura;
    Task? _lavoroTrascrizione;
    Task? _lavoroTraduzione;

    volatile bool _inPausa;
    bool _eraInPausa;
    string _sorgente = "";
    string _notaTraduzione = "";
    bool _conAIIntegrata;
    bool _aiIntegrataScaricata;
    string? _attesa;
    double _caricoMedio;
    bool _avvisatoLentezza;

    public Motore(Impostazioni imp, RegistroRiunione registro)
    {
        _imp = imp;
        _registro = registro;
        _segmentatore = new SegmentatoreVoce(imp.PausaFineFraseMs, imp.DurataMassimaFraseS);
        _segmentatore.Segmento += s => _codaAudio.Writer.TryWrite(s);
    }

    public bool InPausa
    {
        get => _inPausa;
        set
        {
            _inPausa = value;
            AggiornaStato();
        }
    }

    public async Task AvviaAsync()
    {
        var ct = _cts.Token;
        await PreparaTraduzioneAsync(ct);

        var avanzamento = new Avanzamento(m => StatoCambiato?.Invoke(m));
        _trascrittore = await Trascrittore.CreaAsync(_imp.ModelloWhisper, _imp.LinguaParlata, _whisperTraduceInInglese,
            _imp.ParoleChiave, _imp.DurataMassimaFraseS, _imp.UsaSchedaGrafica, avanzamento, ct);

        _lavoroTrascrizione = Task.Run(() => CicloTrascrizioneAsync(ct), ct);
        _lavoroTraduzione = Task.Run(() => CicloTraduzioneAsync(ct), ct);

        await AvviaCatturaAsync();

#if WINDOWS
        if (_imp.Sorgente == "dispositivo" && _imp.DispositivoId is null)
        {
            // Se cambia il dispositivo predefinito (cuffie collegate, altoparlanti Bluetooth...) lo si segue.
            // Sul Mac se ne accorge il programma che ascolta l'audio, che si ferma e viene riavviato.
            _enumeratore = new MMDeviceEnumerator();
            _notifiche = _enumeratore.CreateNotificationClient(false);
            _notifiche.DefaultDeviceChanged += (_, e) =>
            {
                if (e.Flow == DataFlow.Render && e.Role == Role.Multimedia)
                    _ = Task.Run(() => AvviaCatturaAsync());
            };
        }
#endif
        // Controllo periodico: il programma da ascoltare è stato avviato o chiuso? La cattura si è fermata?
        _sorveglianza = new Timer(_ => _ = Sorveglia(), null, 3000, 3000);
    }

    async Task PreparaTraduzioneAsync(CancellationToken ct)
    {
        _traduzione = null;
        var servizi = await Servizi.PreparaAsync(_imp, Avviso, ct);
        if (servizi.Count > 0)
        {
            var catena = new CatenaTraduzione(servizi, _imp.LinguaArrivo);
            catena.Avviso += Avviso;
            catena.ServizioCambiato += nome =>
            {
                _notaTraduzione = nome is null ? " · nessun servizio di traduzione" : $" · traduce {nome}";
                AggiornaStato();
            };
            _traduzione = catena;
            _notaTraduzione = $" · traduce {servizi[0].Nome}";
            if (servizi[0] is ServizioIntegrato)
            {
                _conAIIntegrata = true;
                AIIntegrata.Cambiato += SuAIIntegrataCambiata;
                // Finché l'AI integrata non è pronta traduce il servizio successivo, se c'è.
                if (AIIntegrata.Stato != AIIntegrata.Fase.Pronta)
                    _notaTraduzione = servizi.Count > 1 ? $" · traduce {servizi[1].Nome}" : " · nessun servizio di traduzione";
            }
        }
        else
        {
            _notaTraduzione = " · senza traduzione";
        }
        // Whisper sa tradurre da solo, ma solo verso l'inglese.
        _whisperTraduceInInglese = _traduzione is null && _imp.LinguaArrivo == "en";
        if (_whisperTraduceInInglese) _notaTraduzione = " · traduzione Whisper";
    }

    // ---- Cattura ----

    /// <param name="soloSeFerma">Non riavvia una cattura che sta già funzionando (controlli periodici).</param>
    async Task AvviaCatturaAsync(bool soloSeFerma = false)
    {
        await _semaforoCattura.WaitAsync();
        try
        {
            if (_cts.IsCancellationRequested || (soloSeFerma && _cattura is not null)) return;
            _ultimoAvvioCattura = DateTime.UtcNow;
            _cattura?.Dispose();
            _cattura = null;
            _pidProgramma = null;
            if (OperatingSystem.IsMacOS())
            {
                _attesa = "Avvio l'ascolto dell'audio (la prima volta macOS chiede il permesso di registrare l'audio di sistema)…";
                AggiornaStato();
            }

            CatturaAudio nuova;
            if (_imp.Sorgente == "programma" && !string.IsNullOrWhiteSpace(_imp.NomeProgramma))
            {
                int? pid = Programmi.TrovaRadice(_imp.NomeProgramma);
                if (pid is null)
                {
                    _attesa = $"In attesa che «{_imp.NomeProgramma}» sia avviato…";
                    AggiornaStato();
                    return;
                }
                nuova = await CatturaAudio.AvviaProgrammaAsync(pid.Value, _imp.NomeProgramma);
                _pidProgramma = pid;
                _sorgente = $"solo {_imp.NomeProgramma}";
            }
            else
            {
                nuova = CatturaAudio.AvviaDispositivo(_imp.DispositivoId);
                _sorgente = nuova.Descrizione;
            }

            nuova.Campioni += SuCampioni;
            nuova.Interrotta += motivo =>
            {
                _attesa = $"Audio interrotto ({motivo}): riprovo…";
                AggiornaStato();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    await AvviaCatturaAsync();
                });
            };
            _cattura = nuova;
            _attesa = null;
            AggiornaStato();
        }
        catch (Exception ex)
        {
            _attesa = $"Non riesco ad ascoltare l'audio: {ex.Message}";
            AggiornaStato();
        }
        finally
        {
            _semaforoCattura.Release();
        }
    }

    void SuCampioni(float[] campioni)
    {
        if (_inPausa)
        {
            if (!_eraInPausa) _segmentatore.Scarta();
            _eraInPausa = true;
            return;
        }
        _eraInPausa = false;
        _segmentatore.Aggiungi(campioni);
    }

    async Task Sorveglia()
    {
        if (_cts.IsCancellationRequested || Interlocked.Exchange(ref _sorveglianzaInCorso, 1) == 1) return;
        try
        {
            if (_imp.Sorgente != "programma")
            {
                // La cattura non è partita (per esempio il Mac aspettava il permesso di registrare l'audio): si riprova.
                if (_cattura is null && DateTime.UtcNow - _ultimoAvvioCattura > TimeSpan.FromSeconds(30))
                    await AvviaCatturaAsync(soloSeFerma: true);
                return;
            }
            bool chiuso = false;
            if (_pidProgramma is int pid)
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    chiuso = p.HasExited;
                }
                catch (ArgumentException)
                {
                    chiuso = true; // processo chiuso
                }
            }
            if (chiuso) await AvviaCatturaAsync();
            else if (_cattura is null) await AvviaCatturaAsync(soloSeFerma: true);
        }
        finally
        {
            Volatile.Write(ref _sorveglianzaInCorso, 0);
        }
    }

    // ---- Trascrizione ----

    async Task CicloTrascrizioneAsync(CancellationToken ct)
    {
        var lettore = _codaAudio.Reader;
        double massimo = _imp.DurataMassimaFraseS + 0.5;
        try
        {
            while (await lettore.WaitToReadAsync(ct))
            {
                if (!lettore.TryRead(out var segmento)) continue;
                if (_inPausa) continue;

                // Se Whisper è rimasto indietro, si uniscono le frasi in attesa (finché stanno nella sua
                // finestra): una chiamata sola costa molto meno di più chiamate.
                var campioni = segmento.Campioni;
                while (lettore.TryPeek(out var prossimo)
                       && (campioni.Length + Hz / 5 + prossimo.Campioni.Length) / (double)Hz <= massimo)
                {
                    lettore.TryRead(out prossimo);
                    campioni = [.. campioni, .. new float[Hz / 5], .. prossimo!.Campioni];
                }

                var cronometro = Stopwatch.StartNew();
                Trascrizione.Trascrizione? risultato;
                try
                {
                    risultato = await _trascrittore!.TrascriviAsync(campioni, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Avviso($"Errore nella trascrizione: {ex.Message}");
                    continue;
                }
                ControllaVelocita(cronometro.Elapsed.TotalSeconds, campioni.Length / (double)Hz);
                if (risultato is null || _inPausa) continue;

                var traduzione = _traduzione;
                string lingua = _whisperTraduceInInglese ? "en" : risultato.Lingua;
                bool daTradurre = traduzione is not null && !string.Equals(lingua, traduzione.LinguaArrivo, StringComparison.OrdinalIgnoreCase);
                var riga = new RigaSottotitolo(segmento.Inizio, lingua, risultato.Testo, daTradurre)
                {
                    MostraOriginale = _imp.MostraOriginale,
                };
                NuovaRiga?.Invoke(riga);

                if (daTradurre)
                    _codaTraduzione.Writer.TryWrite(riga);
                else if (_imp.SalvaTrascrizione)
                    _registro.Scrivi(riga.Ora, riga.Lingua, riga.Originale, null, _imp.LinguaArrivo);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    void ControllaVelocita(double secondiDiCalcolo, double secondiDiAudio)
    {
        // Le frasi brevi costano quasi quanto quelle lunghe: si confronta con almeno 2 secondi di audio.
        double carico = secondiDiCalcolo / Math.Max(secondiDiAudio, 2.0);
        _caricoMedio = _caricoMedio == 0 ? carico : 0.75 * _caricoMedio + 0.25 * carico;
        if (_caricoMedio > 0.9 && !_avvisatoLentezza && _imp.ModelloWhisper is not ("tiny" or "base"))
        {
            _avvisatoLentezza = true;
            Avviso($"Il PC fatica a trascrivere in tempo reale con il modello «{_imp.ModelloWhisper}»: " +
                   "i sottotitoli possono arrivare in ritardo. Dal menu scegli un modello più piccolo (base).");
        }
    }

    // ---- Traduzione ----

    async Task CicloTraduzioneAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var riga in _codaTraduzione.Reader.ReadAllAsync(ct))
            {
                var traduzione = _traduzione;
                if (traduzione is null)
                {
                    riga.InTraduzione = false;
                    continue;
                }
                try
                {
                    // La catena prova i servizi uno dopo l'altro: gli avvisi sui servizi non disponibili li manda lei.
                    riga.Traduzione = await traduzione.TraduciAsync(riga.Originale, riga.Lingua,
                        parziale => riga.Traduzione = parziale, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Avviso($"Errore nella traduzione: {ex.Message}");
                }
                finally
                {
                    riga.InTraduzione = false;
                }

                if (_imp.SalvaTrascrizione)
                    _registro.Scrivi(riga.Ora, riga.Lingua, riga.Originale, riga.Traduzione, _imp.LinguaArrivo);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // ---- Stato ----

    void SuAIIntegrataCambiata()
    {
        if (_cts.IsCancellationRequested) return;
        if (AIIntegrata.Stato == AIIntegrata.Fase.Scaricamento) _aiIntegrataScaricata = true;
        if (AIIntegrata.Stato == AIIntegrata.Fase.Pronta && _aiIntegrataScaricata)
        {
            _aiIntegrataScaricata = false;
            Avviso("L'AI di traduzione integrata è pronta: da adesso traduce sul computer, gratis e senza limiti.");
        }
        AggiornaStato();
    }

    void Avviso(string testo) =>
        NuovaRiga?.Invoke(new RigaSottotitolo(DateTime.Now, "", testo, false) { Avviso = true });

    void AggiornaStato()
    {
        if (_attesa is not null)
        {
            StatoCambiato?.Invoke(_attesa);
            return;
        }
        string parlata = _imp.LinguaParlata == "auto" ? "lingua automatica" : Lingue.Nome(_imp.LinguaParlata).ToLowerInvariant();
        string arrivo = Lingue.Nome(_imp.LinguaArrivo).ToLowerInvariant();
        string calcolo = Trascrittore.SuSchedaGrafica ? "scheda grafica" : "processore";
        string ai = _conAIIntegrata && AIIntegrata.Stato is AIIntegrata.Fase.Scaricamento or AIIntegrata.Fase.Verifica or AIIntegrata.Fase.Caricamento
            ? $" · AI integrata: {AIIntegrata.Descrizione}"
            : "";
        StatoCambiato?.Invoke($"{(_inPausa ? "In pausa" : "In ascolto")} · {_sorgente} · {parlata} → {arrivo} · Whisper {_imp.ModelloWhisper} ({calcolo}){_notaTraduzione}{ai}");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_conAIIntegrata) AIIntegrata.Cambiato -= SuAIIntegrataCambiata;
        _sorveglianza?.Dispose();
#if WINDOWS
        _notifiche?.Dispose();
        _enumeratore?.Dispose();
#endif
        await _semaforoCattura.WaitAsync();
        try
        {
            _cattura?.Dispose();
            _cattura = null;
        }
        finally
        {
            _semaforoCattura.Release();
        }
        _codaAudio.Writer.TryComplete();
        _codaTraduzione.Writer.TryComplete();
        foreach (var lavoro in new[] { _lavoroTrascrizione, _lavoroTraduzione })
        {
            if (lavoro is null) continue;
            try { await lavoro; } catch (Exception) { }
        }
        // Il modello si libera solo dopo che la trascrizione in corso è finita.
        _trascrittore?.Dispose();
        _cts.Dispose();
    }

    sealed class Avanzamento(Action<string> azione) : IProgress<string>
    {
        public void Report(string value) => azione(value);
    }
}
