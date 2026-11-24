using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using TraduttoreLive.Trascrizione;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// L'AI di traduzione integrata: TranslateGemma 4B, il modello di traduzione aperto di Google, eseguito dentro il
/// programma con llama.cpp (LLamaSharp). Gratis, senza limiti, senza chiavi, e il testo non lascia il computer.
/// La prima volta il modello (2,5 GB) si scarica da solo, in background: intanto traducono gli altri servizi.
/// Resta caricato per tutta la vita del programma, anche quando la traduzione si riavvia per un cambio di impostazioni.
/// </summary>
public static class AIIntegrata
{
    public enum Fase { NonScaricata, Scaricata, Scaricamento, Verifica, Caricamento, Pronta, Errore }

    // Quantizzazione Q4_K_M di google/translategemma-4b-it (di mradermacher, ripubblicata identica da anilb).
    // Revisione fissata e impronta controllata: si usa solo quel file.
    public const string NomeFile = "translategemma-4b-it.Q4_K_M.gguf";
    public const long Dimensione = 2_489_909_760;
    const string Sha256 = "81200d03e843d2ec1ece6eeafe7d13cb6e5211e1fcd336ade55790b683a08330";
    static readonly string[] Indirizzi =
    [
        "https://huggingface.co/mradermacher/translategemma-4b-it-GGUF/resolve/d37868f759d2f817a43d28dd80cf9f982ffa62fd/" + NomeFile,
        "https://huggingface.co/anilb/translategemma-gguf/resolve/b4a4747a01c36cadf60238985cd31dc6ea66b760/" + NomeFile,
    ];

    const int DimensioneContesto = 1024;
    const int TokenMassimiRisposta = 256;

    static readonly object Blocco = new();
    static readonly SemaphoreSlim Uso = new(1, 1);
    static CancellationTokenSource? _scaricamento;
    static DateTime _ultimoTentativo = DateTime.MinValue;
    static bool _libreriaConfigurata;

    static LLamaWeights? _pesi;
    static LLamaContext? _contesto;
    static string? _istruzioniInMemoria;
    static int _tokenIstruzioni;

    public static string Percorso => Path.Combine(Trascrittore.CartellaModelli, NomeFile);

    public static Fase Stato { get; private set; } = File.Exists(Percorso) ? Fase.Scaricata : Fase.NonScaricata;

    /// <summary>Percentuale dello scaricamento (0-100).</summary>
    public static int Percentuale { get; private set; }

    public static string? UltimoErrore { get; private set; }

    /// <summary>Usa la scheda grafica: Metal sui Mac con Apple Silicon, Vulkan su Windows.</summary>
    public static bool SuSchedaGrafica { get; private set; }

    /// <summary>Cambiano la fase o la percentuale. Arriva da un thread in background.</summary>
    public static event Action? Cambiato;

    /// <summary>Descrizione breve per la riga di stato e la finestra dei servizi.</summary>
    public static string Descrizione => Stato switch
    {
        Fase.NonScaricata => "non ancora scaricata",
        Fase.Scaricata => "scaricata",
        Fase.Scaricamento => $"la sto scaricando: {Percentuale}% di 2,5 GB",
        Fase.Verifica => "controllo il file scaricato",
        Fase.Caricamento => "la sto avviando",
        Fase.Pronta => SuSchedaGrafica ? "pronta (scheda grafica)" : "pronta (processore)",
        _ => $"non disponibile: {UltimoErrore}",
    };

    /// <summary>
    /// Scarica (se manca) e carica il modello in background. Si può chiamare più volte: parte una volta sola, e dopo
    /// un errore riprova al massimo una volta al minuto.
    /// </summary>
    public static void Prepara(bool usaSchedaGrafica)
    {
        lock (Blocco)
        {
            if (Stato is Fase.Scaricamento or Fase.Verifica or Fase.Caricamento or Fase.Pronta) return;
            if (Stato == Fase.Errore && DateTime.UtcNow - _ultimoTentativo < TimeSpan.FromMinutes(1)) return;
            _ultimoTentativo = DateTime.UtcNow;
            _scaricamento = new CancellationTokenSource();
            var ct = _scaricamento.Token;
            Imposta(File.Exists(Percorso) ? Fase.Caricamento : Fase.Scaricamento);
            _ = Task.Run(() => PreparaAsync(usaSchedaGrafica, ct));
        }
    }

    /// <summary>Interrompe lo scaricamento in corso (l'AI integrata è stata disattivata). Riprende da lì la prossima volta.</summary>
    public static void FermaScaricamento()
    {
        lock (Blocco)
        {
            if (Stato != Fase.Scaricamento) return;
            _scaricamento?.Cancel();
        }
    }

    static async Task PreparaAsync(bool usaSchedaGrafica, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(Percorso)) await ScaricaAsync(ct);
            Imposta(Fase.Caricamento);
            await CaricaAsync(usaSchedaGrafica);
            Imposta(Fase.Pronta);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Imposta(File.Exists(Percorso) ? Fase.Scaricata : Fase.NonScaricata);
        }
        catch (Exception ex)
        {
            UltimoErrore = ex.Message;
            Registra(ex);
            Imposta(Fase.Errore);
        }
    }

    /// <summary>L'ultimo errore, con i dettagli tecnici, in ai-integrata.log nella cartella delle impostazioni (per l'assistenza).</summary>
    static void Registra(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Impostazioni.Cartella);
            File.WriteAllText(Path.Combine(Impostazioni.Cartella, "ai-integrata.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {Environment.OSVersion} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}\n{ex}\n");
        }
        catch (Exception)
        {
        }
    }

    static void Imposta(Fase fase)
    {
        Stato = fase;
        Cambiato?.Invoke();
    }

    // ---- Scaricamento ----

    static async Task ScaricaAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Trascrittore.CartellaModelli);
        string temporaneo = Percorso + ".download";
        long presenti = File.Exists(temporaneo) ? new FileInfo(temporaneo).Length : 0;
        long liberi = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(temporaneo))!).AvailableFreeSpace;
        if (liberi < Dimensione - presenti + 300_000_000)
            throw new IOException($"servono {(Dimensione - presenti) / 1e9 + 0.3:0.0} GB liberi sul disco per scaricarla.");

        Exception? ultimoErrore = null;
        foreach (string indirizzo in Indirizzi)
        {
            try
            {
                await ScaricaDaAsync(indirizzo, temporaneo, ct);
                ultimoErrore = null;
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested
                                       && ex is HttpRequestException or IOException or OperationCanceledException)
            {
                ultimoErrore = ex;
            }
        }
        if (ultimoErrore is not null)
            throw new IOException($"lo scaricamento non è riuscito ({ultimoErrore.Message}). Riprovo più tardi.", ultimoErrore);

        Imposta(Fase.Verifica);
        string impronta;
        await using (var file = new FileStream(temporaneo, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true))
            impronta = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, ct));
        if (impronta != Sha256)
        {
            File.Delete(temporaneo);
            throw new InvalidDataException("il file scaricato è rovinato: lo riscarico.");
        }
        File.Move(temporaneo, Percorso, overwrite: true);
    }

    static async Task ScaricaDaAsync(string indirizzo, string temporaneo, CancellationToken ct)
    {
        long inizio = File.Exists(temporaneo) ? new FileInfo(temporaneo).Length : 0;
        if (inizio >= Dimensione) return; // già tutto: lo controlla l'impronta

        using var richiesta = new HttpRequestMessage(HttpMethod.Get, indirizzo);
        if (inizio > 0) richiesta.Headers.Range = new RangeHeaderValue(inizio, null);
        // Se per un minuto non arriva niente, si considera interrotto (e si riprende con l'indirizzo successivo).
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromMinutes(1));
        using var risposta = await Rete.Http.SendAsync(richiesta, HttpCompletionOption.ResponseHeadersRead, limite.Token);
        risposta.EnsureSuccessStatusCode();
        bool riprende = risposta.StatusCode == HttpStatusCode.PartialContent;
        long scaricati = riprende ? inizio : 0;

        await using var sorgente = await risposta.Content.ReadAsStreamAsync(limite.Token);
        await using var destinazione = new FileStream(temporaneo, riprende ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 1 << 20, true);
        var buffer = new byte[1 << 20];
        int letti;
        while ((letti = await sorgente.ReadAsync(buffer, limite.Token)) > 0)
        {
            limite.CancelAfter(TimeSpan.FromMinutes(1));
            await destinazione.WriteAsync(buffer.AsMemory(0, letti), limite.Token);
            scaricati += letti;
            int percentuale = (int)Math.Min(99, scaricati * 100 / Dimensione);
            if (percentuale != Percentuale)
            {
                Percentuale = percentuale;
                Cambiato?.Invoke();
            }
        }
        if (scaricati < Dimensione) throw new IOException("la connessione si è interrotta");
    }

    // ---- Caricamento ----

    static async Task CaricaAsync(bool usaSchedaGrafica)
    {
        lock (Blocco)
        {
            // Va deciso prima di caricare la libreria nativa: dopo il primo caricamento non si cambia più.
            if (!_libreriaConfigurata)
            {
                _libreriaConfigurata = true;
                NativeLibraryConfig.All.WithLogCallback((_, _) => { });
                if (OperatingSystem.IsWindows()) NativeLibraryConfig.All.WithVulkan(usaSchedaGrafica);
            }
        }

        bool metal = OperatingSystem.IsMacOS()
                     && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
        bool schedaGrafica = usaSchedaGrafica && (metal || OperatingSystem.IsWindows());
        var parametri = new ModelParams(Percorso)
        {
            ContextSize = DimensioneContesto,
            GpuLayerCount = schedaGrafica ? 99 : 0,
            Threads = Math.Clamp(Environment.ProcessorCount / 2, 2, 8),
        };
        var pesi = await LLamaWeights.LoadFromFileAsync(parametri);
        var contesto = pesi.CreateContext(parametri);
        _pesi = pesi;
        _contesto = contesto;
        // Su Windows la scheda grafica si usa solo se la libreria Vulkan si è caricata.
        SuSchedaGrafica = schedaGrafica && (metal || NativeApi.GetLoadedNativeLibrary(NativeLibraryName.LLama)?.Metadata?.UseVulkan == true);
    }

    // ---- Traduzione ----

    /// <summary>
    /// Traduce una frase. Le istruzioni (uguali per tutte le frasi con la stessa coppia di lingue) restano elaborate
    /// in memoria: a ogni frase si elabora solo il testo nuovo, così il primo pezzo arriva molto prima.
    /// </summary>
    public static async Task<string?> TraduciAsync(string testo, string linguaOriginale, string linguaArrivo,
        Action<string> parziale, CancellationToken ct)
    {
        await Uso.WaitAsync(ct);
        try
        {
            var contesto = _contesto ?? throw new InvalidOperationException("l'AI integrata non è pronta.");
            var vocabolario = contesto.Vocab;
            var batch = new LLamaBatch();

            string istruzioni = "<start_of_turn>user\n" + TranslateGemma.Istruzioni(linguaOriginale, linguaArrivo);
            if (istruzioni != _istruzioniInMemoria)
            {
                _istruzioniInMemoria = null;
                contesto.NativeHandle.MemoryClear(true);
                var tokenIstruzioni = contesto.Tokenize(istruzioni, true, true);
                batch.AddRange(tokenIstruzioni, 0, LLamaSeqId.Zero, false);
                Controlla(await contesto.DecodeAsync(batch, ct));
                _tokenIstruzioni = tokenIstruzioni.Length;
                _istruzioniInMemoria = istruzioni;
            }
            else
            {
                contesto.NativeHandle.MemorySequenceRemove(LLamaSeqId.Zero, _tokenIstruzioni, -1);
            }

            var resto = contesto.Tokenize(testo.Trim() + "<end_of_turn>\n<start_of_turn>model\n", false, true);
            int spazio = DimensioneContesto - _tokenIstruzioni - TokenMassimiRisposta - 8;
            if (resto.Length > spazio) resto = resto[^spazio..]; // non succede con frasi di qualche secondo
            batch.Clear();
            for (int i = 0; i < resto.Length; i++)
                batch.Add(resto[i], _tokenIstruzioni + i, LLamaSeqId.Zero, i == resto.Length - 1);
            Controlla(await contesto.DecodeAsync(batch, ct));

            int posizione = _tokenIstruzioni + resto.Length;
            var campionatore = new GreedySamplingPipeline();
            var decodificatore = new StreamingTokenDecoder(contesto);
            var risposta = new StringBuilder();
            for (int n = 0; n < TokenMassimiRisposta; n++)
            {
                ct.ThrowIfCancellationRequested();
                var token = campionatore.Sample(contesto.NativeHandle, batch.TokenCount - 1);
                if (token.IsEndOfGeneration(vocabolario)) break;
                decodificatore.Add(token);
                risposta.Append(decodificatore.Read());
                parziale(risposta.ToString().Trim());
                batch.Clear();
                batch.Add(token, posizione++, LLamaSeqId.Zero, true);
                Controlla(await contesto.DecodeAsync(batch, ct));
            }
            string risultato = risposta.ToString().Trim();
            return risultato.Length == 0 ? null : risultato;
        }
        catch (Exception)
        {
            _istruzioniInMemoria = null; // la memoria del modello è a metà: alla prossima frase si riparte da zero
            throw;
        }
        finally
        {
            Uso.Release();
        }
    }

    static void Controlla(DecodeResult esito)
    {
        if (esito != DecodeResult.Ok) throw new InvalidOperationException($"errore dell'AI integrata ({esito}).");
    }
}

/// <summary>
/// L'AI integrata come servizio della catena di traduzione. Finché il modello non è pronto (si scarica la prima
/// volta) traducono gli altri servizi. Se il computer è troppo lento per usarla in diretta e ci sono altri servizi,
/// per il resto della sessione passa a quelli.
/// </summary>
public sealed class ServizioIntegrato : IServizioTraduzione
{
    /// <summary>Oltre questo tempo medio per frase i sottotitoli restano indietro: meglio un servizio online.</summary>
    const double SecondiTroppi = 8;

    readonly bool _usaSchedaGrafica;
    readonly List<double> _durate = [];
    bool _troppoLenta;

    public ServizioIntegrato(bool usaSchedaGrafica)
    {
        _usaSchedaGrafica = usaSchedaGrafica;
        AIIntegrata.Prepara(usaSchedaGrafica);
    }

    /// <summary>Ci sono altri servizi a cui passare se è troppo lenta.</summary>
    public bool PuoPassareAdAltri { get; set; }

    public string Nome => "AI integrata";

    public async Task<string?> TraduciAsync(RichiestaTraduzione r, Action<string> parziale, CancellationToken ct)
    {
        if (_troppoLenta)
            throw new ServizioNonDisponibileException("L'AI integrata è troppo lenta su questo computer per i sottotitoli in diretta: " +
                                                      "fino al riavvio uso gli altri servizi.");
        if (AIIntegrata.Stato != AIIntegrata.Fase.Pronta)
        {
            AIIntegrata.Prepara(_usaSchedaGrafica);
            throw AIIntegrata.Stato == AIIntegrata.Fase.Errore
                ? new ServizioNonDisponibileException($"Non riesco a preparare l'AI integrata: {AIIntegrata.UltimoErrore}",
                    TimeSpan.FromMinutes(2))
                : new ServizioNonDisponibileException("L'AI integrata si sta preparando (la prima volta la scarico: 2,5 GB).",
                    TimeSpan.FromSeconds(10));
        }

        var cronometro = Stopwatch.StartNew();
        string? traduzione;
        try
        {
            traduzione = await AIIntegrata.TraduciAsync(r.Testo, r.LinguaOriginale, r.LinguaArrivo, parziale, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ServizioNonDisponibileException($"AI integrata: {ex.Message}", TimeSpan.FromMinutes(1));
        }

        _durate.Add(cronometro.Elapsed.TotalSeconds);
        if (_durate.Count > 5) _durate.RemoveAt(0);
        if (PuoPassareAdAltri && _durate.Count >= 4 && _durate.Order().ElementAt(_durate.Count / 2) > SecondiTroppi)
            _troppoLenta = true; // da questa frase in poi
        return traduzione;
    }
}
