namespace TraduttoreLive.Audio;

/// <summary>Un pezzo di parlato pronto da trascrivere (float mono, 16 kHz).</summary>
public sealed record SegmentoAudio(float[] Campioni, DateTime Inizio)
{
    public TimeSpan Durata => TimeSpan.FromSeconds(Campioni.Length / (double)ConvertitoreAudio.FrequenzaUscita);
}

/// <summary>
/// Divide il flusso audio in frasi in base al volume: una frase inizia quando il segnale supera il
/// rumore di fondo e finisce dopo una pausa. Se qualcuno parla a lungo senza pause, la frase viene
/// tagliata nel punto più silenzioso degli ultimi secondi, così i sottotitoli non restano indietro.
/// La verifica che si tratti davvero di voce (e non di musica o rumore) la fa poi il Trascrittore.
/// </summary>
public sealed class SegmentatoreVoce
{
    const int CampioniPerFrame = ConvertitoreAudio.FrequenzaUscita / 50; // 20 ms
    const int FramePreparlato = 15;     // 300 ms tenuti prima dell'inizio della voce
    const int FrameAvvio = 2;           // 40 ms di voce consecutivi per aprire una frase
    const int FrameMinimiDiVoce = 12;   // meno di 240 ms di voce: colpo di tosse, clic... si scarta
    const int FrameCodaSilenzio = 10;   // 200 ms di silenzio lasciati alla fine della frase

    readonly int _framePausa;
    readonly int _frameMassimi;

    readonly float[] _accumulo = new float[CampioniPerFrame];
    int _nAccumulo;

    readonly Queue<float[]> _preparlato = new();
    readonly List<float[]> _frame = [];
    readonly List<double> _energie = [];
    bool _inFrase;
    int _votiConsecutivi;
    int _silenzioConsecutivo;
    int _frameDiVoce;
    DateTime _inizio;
    double _rumore = 0.001;

    public event Action<SegmentoAudio>? Segmento;

    public SegmentatoreVoce(int pausaFineFraseMs, int durataMassimaS)
    {
        _framePausa = Math.Max(5, pausaFineFraseMs / 20);
        _frameMassimi = Math.Max(100, durataMassimaS * 50);
    }

    public void Aggiungi(ReadOnlySpan<float> campioni)
    {
        while (campioni.Length > 0)
        {
            int n = Math.Min(CampioniPerFrame - _nAccumulo, campioni.Length);
            campioni[..n].CopyTo(_accumulo.AsSpan(_nAccumulo));
            _nAccumulo += n;
            campioni = campioni[n..];
            if (_nAccumulo == CampioniPerFrame)
            {
                ElaboraFrame((float[])_accumulo.Clone());
                _nAccumulo = 0;
            }
        }
    }

    /// <summary>Butta via la frase in corso senza trascriverla (per esempio quando si mette in pausa).</summary>
    public void Scarta()
    {
        _nAccumulo = 0;
        Azzera();
    }

    void ElaboraFrame(float[] frame)
    {
        double somma = 0;
        foreach (float c in frame) somma += c * c;
        double rms = Math.Sqrt(somma / frame.Length);

        // Rumore di fondo: scende in fretta, sale lentamente (circa 30 secondi), così la voce non lo alza.
        _rumore = rms < _rumore ? 0.9 * _rumore + 0.1 * rms : 0.9993 * _rumore + 0.0007 * rms;
        _rumore = Math.Max(_rumore, 0.0002);
        double soglia = Math.Max(0.004, _rumore * 3.0);
        bool voce = rms > soglia;

        if (!_inFrase)
        {
            _preparlato.Enqueue(frame);
            if (_preparlato.Count > FramePreparlato) _preparlato.Dequeue();
            _votiConsecutivi = voce ? _votiConsecutivi + 1 : 0;
            if (_votiConsecutivi < FrameAvvio) return;

            _inFrase = true;
            _inizio = DateTime.Now - TimeSpan.FromMilliseconds(_preparlato.Count * 20);
            foreach (var f in _preparlato)
            {
                _frame.Add(f);
                _energie.Add(0);
            }
            _preparlato.Clear();
            _frameDiVoce = FrameAvvio;
            _silenzioConsecutivo = 0;
            return;
        }

        _frame.Add(frame);
        _energie.Add(rms);
        if (voce)
        {
            _frameDiVoce++;
            _silenzioConsecutivo = 0;
        }
        else
        {
            _silenzioConsecutivo++;
        }

        if (_silenzioConsecutivo >= _framePausa)
        {
            int fine = _frame.Count - _silenzioConsecutivo + FrameCodaSilenzio;
            Emetti(Math.Min(fine, _frame.Count));
            Azzera();
        }
        else if (_frame.Count >= _frameMassimi)
        {
            TagliaFraseLunga();
        }
    }

    /// <summary>Frase troppo lunga: si taglia nel punto più silenzioso dell'ultimo 40% e si continua.</summary>
    void TagliaFraseLunga()
    {
        int da = (int)(_frame.Count * 0.6);
        int taglio = da;
        double minimo = double.MaxValue;
        for (int i = da; i < _frame.Count - 5; i++)
        {
            // energia media su 100 ms, per non tagliare su un singolo frame tra due sillabe
            double e = 0;
            for (int k = i; k < i + 5; k++) e += _energie[k];
            if (e < minimo)
            {
                minimo = e;
                taglio = i + 2;
            }
        }

        Emetti(taglio);
        var restanti = _frame.GetRange(taglio, _frame.Count - taglio);
        var energieRestanti = _energie.GetRange(taglio, _energie.Count - taglio);
        _frame.Clear();
        _energie.Clear();
        _frame.AddRange(restanti);
        _energie.AddRange(energieRestanti);
        _inizio = DateTime.Now - TimeSpan.FromMilliseconds(_frame.Count * 20);
        _frameDiVoce = _frame.Count; // stima prudente: il resto contiene voce
    }

    void Emetti(int numeroFrame)
    {
        if (_frameDiVoce < FrameMinimiDiVoce || numeroFrame <= 0) return;
        var campioni = new float[numeroFrame * CampioniPerFrame];
        for (int i = 0; i < numeroFrame; i++)
            _frame[i].CopyTo(campioni, i * CampioniPerFrame);
        Segmento?.Invoke(new SegmentoAudio(campioni, _inizio));
    }

    void Azzera()
    {
        _inFrase = false;
        _frame.Clear();
        _energie.Clear();
        _preparlato.Clear();
        _votiConsecutivi = 0;
        _silenzioConsecutivo = 0;
        _frameDiVoce = 0;
    }
}
