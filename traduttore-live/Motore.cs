using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using Anthropic.Exceptions;
using NAudio.CoreAudioApi;
using TraduttoreLive.Audio;
using TraduttoreLive.Trascrizione;
using TraduttoreLive.Traduzione;

namespace TraduttoreLive;

/// <summary>
/// Collega i pezzi: audio del PC → frasi → trascrizione (Whisper, sul PC) → traduzione (Claude).
/// Trascrizione e traduzione lavorano in parallelo: mentre Claude traduce una frase, Whisper
/// trascrive già la successiva. Gli eventi arrivano da thread in background.
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
    volatile TraduttoreClaude? _traduttore;
    bool _whisperTraduceInInglese;
    MMDeviceEnumerator? _enumeratore;
    MMDeviceNotificationClient? _notifiche;
    Timer? _sorveglianza;
    Task? _lavoroTrascrizione;
    Task? _lavoroTraduzione;

    volatile bool _inPausa;
    bool _eraInPausa;
    string _sorgente = "";
    string _notaTraduzione = "";
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
        PreparaTraduttore();

        var avanzamento = new Avanzamento(m => StatoCambiato?.Invoke(m));
        _trascrittore = await Trascrittore.CreaAsync(_imp.ModelloWhisper, _imp.LinguaParlata, _whisperTraduceInInglese,
            _imp.ParoleChiave, _imp.DurataMassimaFraseS, _imp.UsaSchedaGrafica, avanzamento, ct);

        _lavoroTrascrizione = Task.Run(() => CicloTrascrizioneAsync(ct), ct);
        _lavoroTraduzione = Task.Run(() => CicloTraduzioneAsync(ct), ct);

        await AvviaCatturaAsync();

        if (_imp.Sorgente == "dispositivo" && _imp.DispositivoId is null)
        {
            // Se cambia il dispositivo predefinito (cuffie collegate, altoparlanti Bluetooth...) lo si segue.
            _enumeratore = new MMDeviceEnumerator();
            _notifiche = _enumeratore.CreateNotificationClient(false);
            _notifiche.DefaultDeviceChanged += (_, e) =>
            {
                if (e.Flow == DataFlow.Render && e.Role == Role.Multimedia)
                    _ = Task.Run(AvviaCatturaAsync);
            };
        }
        // Controllo periodico: il programma da ascoltare è stato avviato o chiuso? La cattura si è fermata?
        _sorveglianza = new Timer(_ => _ = Sorveglia(), null, 3000, 3000);
    }

    void PreparaTraduttore()
    {
        _traduttore = null;
        _notaTraduzione = "";
        if (!ChiaveApi.CredenzialiPresenti())
        {
            _notaTraduzione = " · senza traduzione";
            Avviso(_imp.LinguaArrivo == "en"
                ? "Manca la chiave API di Claude: traduco in inglese con Whisper, sul PC. Per tradurre con Claude inseriscila dal menu › Chiave API di Claude."
                : "Manca la chiave API di Claude: mostro solo la trascrizione. Per tradurre inseriscila dal menu › Chiave API di Claude.");
        }
        else
        {
            try
            {
                _traduttore = new TraduttoreClaude(_imp.ModelloClaude, _imp.LinguaArrivo, _imp.FileGlossario);
            }
            catch (Exception ex)
            {
                _notaTraduzione = " · senza traduzione";
                Avviso($"Non riesco a preparare la traduzione con Claude: {ex.Message}");
            }
        }
        // Whisper sa tradurre da solo, ma solo verso l'inglese.
        _whisperTraduceInInglese = _traduttore is null && _imp.LinguaArrivo == "en";
        if (_whisperTraduceInInglese) _notaTraduzione = " · traduzione Whisper";
    }

    // ---- Cattura ----

    async Task AvviaCatturaAsync()
    {
        await _semaforoCattura.WaitAsync();
        try
        {
            if (_cts.IsCancellationRequested) return;
            _cattura?.Dispose();
            _cattura = null;
            _pidProgramma = null;

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
        if (_cts.IsCancellationRequested || _imp.Sorgente != "programma") return;
        bool daRiavviare = _cattura is null;
        if (_pidProgramma is int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                daRiavviare |= p.HasExited;
            }
            catch (ArgumentException)
            {
                daRiavviare = true; // processo chiuso
            }
        }
        if (daRiavviare) await AvviaCatturaAsync();
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

                var traduttore = _traduttore;
                string lingua = _whisperTraduceInInglese ? "en" : risultato.Lingua;
                bool daTradurre = traduttore is not null && !string.Equals(lingua, traduttore.LinguaArrivo, StringComparison.OrdinalIgnoreCase);
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
                var traduttore = _traduttore;
                if (traduttore is null)
                {
                    riga.InTraduzione = false;
                    continue;
                }
                try
                {
                    riga.Traduzione = await traduttore.TraduciAsync(riga.Originale, riga.Lingua,
                        parziale => riga.Traduzione = parziale, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (AnthropicUnauthorizedException)
                {
                    FermaTraduzione("Claude rifiuta la chiave API: controllala dal menu › Chiave API di Claude.");
                }
                catch (AnthropicForbiddenException)
                {
                    FermaTraduzione("La chiave API di Claude non ha accesso a questo modello o all'organizzazione.");
                }
                catch (AnthropicNotFoundException)
                {
                    FermaTraduzione($"Il modello di Claude «{_imp.ModelloClaude}» non esiste: correggi «ModelloClaude» nelle impostazioni.");
                }
                catch (AnthropicRateLimitException)
                {
                    Avviso("Claude: troppe richieste in poco tempo, questa frase resta senza traduzione.");
                }
                catch (AnthropicIOException)
                {
                    Avviso("Claude non è raggiungibile: controlla la connessione a Internet.");
                }
                catch (AnthropicApiException ex)
                {
                    Avviso($"Claude ha restituito un errore: {ex.Message}");
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

    void FermaTraduzione(string messaggio)
    {
        _traduttore = null;
        _notaTraduzione = " · traduzione ferma";
        Avviso(messaggio);
        AggiornaStato();
    }

    // ---- Stato ----

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
        string calcolo = Trascrittore.MotoreDiCalcolo.Contains("vulkan", StringComparison.OrdinalIgnoreCase) ? "scheda grafica" : "processore";
        StatoCambiato?.Invoke($"{(_inPausa ? "In pausa" : "In ascolto")} · {_sorgente} · {parlata} → {arrivo} · Whisper {_imp.ModelloWhisper} ({calcolo}){_notaTraduzione}");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _sorveglianza?.Dispose();
        _notifiche?.Dispose();
        _enumeratore?.Dispose();
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
