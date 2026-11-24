using System.Diagnostics;

namespace TraduttoreLive.Audio;

/// <summary>
/// Ascolta l'audio in uscita dal Mac: tutto l'audio, oppure solo quello di un'app. Il lavoro lo fa il programma
/// cattura-audio (Core Audio, macOS 14.2 o successivo), che manda qui i campioni float mono a 16 kHz. Non usa il
/// microfono: sente le voci degli altri partecipanti, non la tua. La prima volta macOS chiede il permesso di
/// registrare l'audio di sistema.
/// </summary>
public sealed class CatturaAudio : IDisposable
{
    /// <summary>Nuovi campioni mono a 16 kHz. Viene chiamato da un thread in background: chi lo gestisce deve essere veloce.</summary>
    public event Action<float[]>? Campioni;

    /// <summary>La cattura si è fermata da sola (cambio dell'uscita audio, errore...).</summary>
    public event Action<string>? Interrotta;

    public string Descrizione { get; }

    static string Programma => Path.Combine(AppContext.BaseDirectory, "cattura-audio");

    readonly Process _processo;
    readonly object _lock = new();
    readonly TaskCompletionSource _avvio = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Thread _lettoreErrori;
    string? _ultimoErrore;
    bool _fermata;

    CatturaAudio(Process processo, string descrizione)
    {
        _processo = processo;
        Descrizione = descrizione;
        _lettoreErrori = new Thread(LeggiMessaggi) { IsBackground = true, Name = "cattura-audio: messaggi" };
        _lettoreErrori.Start();
        new Thread(LeggiCampioni) { IsBackground = true, Name = "cattura-audio: campioni", Priority = ThreadPriority.AboveNormal }.Start();
    }

    /// <summary>Ascolta tutto l'audio del Mac. Sul Mac non si sceglie il dispositivo: si ascoltano tutte le app.</summary>
    public static CatturaAudio AvviaDispositivo(string? dispositivoId) => Avvia(["ascolta"], "tutto l'audio del Mac");

    /// <summary>Ascolta solo l'audio di un'app e dei suoi processi secondari.</summary>
    public static Task<CatturaAudio> AvviaProgrammaAsync(int pid, string nome)
    {
        string percorso = Programmi.PercorsoApp(pid)
            ?? throw new InvalidOperationException($"«{nome}» non è più in esecuzione.");
        return Task.Run(() => Avvia(["ascolta", "--programma", percorso], $"solo {nome}"));
    }

    static CatturaAudio Avvia(string[] argomenti, string descrizione)
    {
        if (!File.Exists(Programma))
            throw new FileNotFoundException("manca il programma cattura-audio accanto a Traduttore dal vivo.");
        var info = new ProcessStartInfo(Programma)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string a in argomenti) info.ArgumentList.Add(a);
        var processo = Process.Start(info) ?? throw new InvalidOperationException("non riesco ad avviare cattura-audio.");

        var cattura = new CatturaAudio(processo, descrizione);
        try
        {
            // La prima volta macOS chiede il permesso di registrare l'audio di sistema e l'avvio aspetta la risposta.
            if (!cattura._avvio.Task.Wait(TimeSpan.FromMinutes(2)))
                throw new TimeoutException("manca il permesso di registrare l'audio di sistema, oppure il programma che ascolta " +
                                           "l'audio non risponde. Riprovo tra poco.");
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            cattura.Dispose();
            throw ex.InnerException;
        }
        catch (Exception)
        {
            cattura.Dispose();
            throw;
        }
        return cattura;
    }

    /// <summary>Righe sullo standard error: «PRONTO …» quando l'ascolto è partito, «ERRORE …» prima di uscire.</summary>
    void LeggiMessaggi()
    {
        try
        {
            while (_processo.StandardError.ReadLine() is { } riga)
            {
                if (riga.StartsWith("PRONTO", StringComparison.Ordinal)) _avvio.TrySetResult();
                else if (riga.StartsWith("ERRORE ", StringComparison.Ordinal)) _ultimoErrore = riga["ERRORE ".Length..].Trim();
            }
        }
        catch (Exception)
        {
            // Processo chiuso.
        }
        _avvio.TrySetException(new InvalidOperationException(_ultimoErrore ?? "il programma che ascolta l'audio si è chiuso."));
    }

    void LeggiCampioni()
    {
        var flusso = _processo.StandardOutput.BaseStream;
        var buffer = new byte[16_000 * 4 / 10]; // 100 ms
        int resto = 0;
        try
        {
            int letti;
            while ((letti = flusso.Read(buffer, resto, buffer.Length - resto)) > 0)
            {
                int totale = resto + letti;
                int utili = totale - totale % 4;
                var campioni = new float[utili / 4];
                Buffer.BlockCopy(buffer, 0, campioni, 0, utili);
                resto = totale - utili;
                if (resto > 0) Array.Copy(buffer, utili, buffer, 0, resto);

                lock (_lock)
                {
                    if (_fermata) return;
                    if (campioni.Length > 0) Campioni?.Invoke(campioni);
                }
            }
        }
        catch (Exception)
        {
            // Flusso chiuso.
        }

        _lettoreErrori.Join(TimeSpan.FromSeconds(2));
        lock (_lock)
        {
            if (_fermata || !_avvio.Task.IsCompletedSuccessfully) return;
        }
        Interrotta?.Invoke(_ultimoErrore ?? "il programma che ascolta l'audio si è fermato");
    }

    public void Dispose()
    {
        lock (_lock) _fermata = true;
        try
        {
            // Chiudendo lo standard input cattura-audio si ferma da solo e libera il dispositivo di ascolto.
            _processo.StandardInput.Close();
            if (!_processo.WaitForExit(1500)) _processo.Kill();
        }
        catch (Exception)
        {
        }
        _processo.Dispose();
    }
}
