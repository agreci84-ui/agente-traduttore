using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TraduttoreLive.Audio;

/// <summary>
/// Ascolta l'audio in uscita dal PC (WASAPI loopback): tutto quello che esce da un dispositivo,
/// oppure solo l'audio di un programma. Non usa il microfono: sente le voci degli altri partecipanti,
/// non la tua. Produce campioni float mono a 16 kHz.
/// </summary>
public sealed class CatturaAudio : IDisposable
{
    /// <summary>Nuovi campioni mono a 16 kHz. Viene chiamato da un thread audio: chi lo gestisce deve essere veloce.</summary>
    public event Action<float[]>? Campioni;

    /// <summary>La cattura si è fermata da sola (dispositivo scollegato, errore del driver...).</summary>
    public event Action<string>? Interrotta;

    public string Descrizione { get; }

    readonly WasapiRecorder _registratore;
    readonly ConvertitoreAudio _convertitore;
    readonly object _lock = new();
    readonly Stopwatch _orologio = Stopwatch.StartNew();
    readonly Timer _riempiSilenzio;
    TimeSpan _ultimoPacchetto;
    bool _fermata;

    CatturaAudio(WasapiRecorder registratore, string descrizione)
    {
        _registratore = registratore;
        Descrizione = descrizione;
        _convertitore = new ConvertitoreAudio(registratore.WaveFormat);
        _registratore.DataAvailable += SuDati;
        _registratore.RecordingStopped += (_, e) =>
        {
            if (_fermata) return;
            Interrotta?.Invoke(e.Exception?.Message ?? "la cattura dell'audio si è fermata");
        };
        // In loopback Windows non manda pacchetti quando non suona niente: senza questo timer
        // l'ultima frase resterebbe "aperta" finché non riparte l'audio.
        _riempiSilenzio = new Timer(_ => RiempiSilenzio(), null, 100, 100);
    }

    /// <summary>Ascolta tutto ciò che esce da un dispositivo (null = dispositivo predefinito di Windows).</summary>
    public static CatturaAudio AvviaDispositivo(string? dispositivoId)
    {
        using var enumeratore = new MMDeviceEnumerator();
        MMDevice dispositivo = dispositivoId is null
            ? enumeratore.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : enumeratore.GetDevice(dispositivoId);
        string nome = dispositivo.FriendlyName;

        var registratore = new WasapiRecorderBuilder()
            .WithDevice(dispositivo)
            .WithLoopbackCapture()
            .Build();
        var cattura = new CatturaAudio(registratore, nome);
        registratore.StartRecording();
        return cattura;
    }

    /// <summary>Ascolta solo l'audio di un processo e dei suoi processi figli (richiede Windows 10 2004 o successivo).</summary>
    public static async Task<CatturaAudio> AvviaProgrammaAsync(int pid, string nome)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            throw new PlatformNotSupportedException(
                "per ascoltare un solo programma serve Windows 10 versione 2004 o successiva: scegli «Tutto l'audio del PC».");

        // Il dispositivo virtuale del "process loopback" non ha un formato suo: lo sceglie chi cattura,
        // e Windows converte. Si chiede direttamente quello che serve a Whisper.
        var registratore = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)pid, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(new WaveFormat(ConvertitoreAudio.FrequenzaUscita, 16, 1))
            .BuildAsync();
        var cattura = new CatturaAudio(registratore, nome);
        registratore.StartRecording();
        return cattura;
    }

    void SuDati(ReadOnlySpan<byte> dati, AudioClientBufferFlags flag, long posizioneDispositivo, long qpc)
    {
        lock (_lock)
        {
            if (_fermata) return;
            var campioni = _convertitore.Converti(dati, flag.HasFlag(AudioClientBufferFlags.Silent));
            _ultimoPacchetto = _orologio.Elapsed;
            if (campioni.Length > 0) Campioni?.Invoke(campioni);
        }
    }

    void RiempiSilenzio()
    {
        lock (_lock)
        {
            if (_fermata) return;
            var adesso = _orologio.Elapsed;
            var vuoto = adesso - _ultimoPacchetto;
            if (vuoto < TimeSpan.FromMilliseconds(150)) return;
            int n = (int)(vuoto.TotalSeconds * ConvertitoreAudio.FrequenzaUscita);
            _ultimoPacchetto = adesso;
            Campioni?.Invoke(new float[n]);
        }
    }

    public void Dispose()
    {
        lock (_lock) _fermata = true;
        _riempiSilenzio.Dispose();
        try { _registratore.StopRecording(); } catch (Exception) { }
        _registratore.Dispose();
    }
}
