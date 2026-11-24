using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TraduttoreLive.Traduzione;

namespace TraduttoreLive;

/// <summary>
/// Configura i servizi di traduzione: chiavi ed email (salvate nel Portachiavi), Ollama e il suo modello.
/// Restituisce true (ShowDialog&lt;bool&gt;) se qualcosa è stato salvato.
/// </summary>
public partial class FinestraServizi : Window
{
    static readonly IBrush Verde = new SolidColorBrush(Color.FromRgb(0x2D, 0xA4, 0x4E));
    static readonly IBrush Rosso = new SolidColorBrush(Color.FromRgb(0xD1, 0x24, 0x2F));

    readonly Impostazioni _imp;
    readonly CancellationTokenSource _cts = new();
    bool _scaricamentoInCorso;
    bool _chiusa;
    bool _chiusuraConfermata;

    public FinestraServizi() : this(new Impostazioni())
    {
    }

    public FinestraServizi(Impostazioni imp)
    {
        InitializeComponent();
        _imp = imp;
        OllamaModello.ItemsSource = new[] { "translategemma:4b", "translategemma:12b" };
        OllamaModello.Text = imp.OllamaModello;
        OllamaIndirizzo.Text = imp.OllamaIndirizzo;
        ChiaveGemini.Text = Credenziali.Leggi(Credenziali.Gemini) ?? "";
        ChiaveGroq.Text = Credenziali.Leggi(Credenziali.Groq) ?? "";
        ChiaveDeepL.Text = Credenziali.Leggi(Credenziali.DeepL) ?? "";
        ChiaveMicrosoft.Text = Credenziali.Leggi(Credenziali.Microsoft) ?? "";
        RegioneMicrosoft.Text = imp.MicrosoftRegione;
        EmailMyMemory.Text = Credenziali.Leggi(Credenziali.MyMemory) ?? "";
        ChiaveClaude.Text = Credenziali.Leggi(Credenziali.Claude) ?? "";
        UsaIntegrata.IsChecked = imp.UsaAIIntegrata;
        AggiornaIntegrata();
        AIIntegrata.Cambiato += SuAIIntegrataCambiata;
        Opened += async (_, _) => await ControllaOllamaAsync();
        Closing += SuChiusura;
        Closed += (_, _) =>
        {
            AIIntegrata.Cambiato -= SuAIIntegrataCambiata;
            _chiusa = true;
            _cts.Cancel();
        };
    }

    static string Testo(TextBox casella) => (casella.Text ?? "").Trim();

    static void Mostra(TextBlock esito, string testo, IBrush? colore = null)
    {
        esito.Text = testo;
        esito.IsVisible = testo.Length > 0;
        if (colore is null) esito.ClearValue(TextBlock.ForegroundProperty);
        else esito.Foreground = colore;
        esito.Opacity = colore is null ? 0.8 : 1;
    }

    // ---- AI integrata ----

    void SuAIIntegrataCambiata() => Dispatcher.UIThread.Post(AggiornaIntegrata);

    void AggiornaIntegrata()
    {
        if (_chiusa) return;
        var fase = AIIntegrata.Stato;
        AvanzamentoIntegrata.IsVisible = fase is AIIntegrata.Fase.Scaricamento or AIIntegrata.Fase.Verifica or AIIntegrata.Fase.Caricamento;
        AvanzamentoIntegrata.IsIndeterminate = fase != AIIntegrata.Fase.Scaricamento;
        AvanzamentoIntegrata.Value = AIIntegrata.Percentuale;
        string testo = fase == AIIntegrata.Fase.NonScaricata
            ? "Stato: non ancora scaricata. Si scarica da sola appena è attiva."
            : $"Stato: {AIIntegrata.Descrizione}.";
        Mostra(EsitoIntegrata, testo, fase == AIIntegrata.Fase.Pronta ? Verde : fase == AIIntegrata.Fase.Errore ? Rosso : null);
    }

    Task ProvaIntegrataAsync(RichiestaTraduzione richiesta) =>
        AIIntegrata.Stato == AIIntegrata.Fase.Pronta
            ? ProvaAsync(EsitoIntegrata, new ServizioIntegrato(_imp.UsaSchedaGrafica), richiesta)
            : Task.CompletedTask; // lo stato è già scritto sotto la casella

    // ---- Ollama ----

    string? IndirizzoOllama()
    {
        string testo = Testo(OllamaIndirizzo);
        if (Uri.TryCreate(testo, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return testo;
        Mostra(EsitoOllama, "✗ L'indirizzo di Ollama non è valido (quello predefinito è http://localhost:11434).", Rosso);
        return null;
    }

    string ModelloOllama() => (OllamaModello.Text ?? "").Trim();

    async void SuControllaOllama(object? sender, RoutedEventArgs e) => await ControllaOllamaAsync();

    async Task ControllaOllamaAsync()
    {
        if (_scaricamentoInCorso || IndirizzoOllama() is not { } indirizzo) return;
        Mostra(EsitoOllama, "Controllo Ollama…");
        try
        {
            var stato = await ServizioOllama.ControllaAsync(indirizzo, ModelloOllama(), _cts.Token);
            switch (stato)
            {
                case ServizioOllama.Stato.Pronto:
                    Mostra(EsitoOllama, "✓ Ollama è avviato e il modello è pronto.", Verde);
                    break;
                case ServizioOllama.Stato.ModelloMancante:
                    Mostra(EsitoOllama, "Ollama è avviato, ma il modello non è ancora scaricato: usa «Scarica il modello».");
                    break;
                default:
                    Mostra(EsitoOllama, "Ollama non risponde: installalo e avvialo (dopo l'installazione resta attivo nella barra dei menu).");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    async void SuScaricaModello(object? sender, RoutedEventArgs e)
    {
        if (_scaricamentoInCorso || IndirizzoOllama() is not { } indirizzo) return;
        string modello = ModelloOllama();
        if (modello.Length == 0)
        {
            Mostra(EsitoOllama, "✗ Indica il modello da scaricare.", Rosso);
            return;
        }

        _scaricamentoInCorso = true;
        PulsanteScarica.IsEnabled = false;
        Avanzamento.IsVisible = true;
        Avanzamento.IsIndeterminate = true;
        Mostra(EsitoOllama, "Preparo lo scaricamento… Lascia aperta questa finestra fino alla fine.");
        var avanzamento = new Progress<(string Stato, double? Percentuale)>(p =>
        {
            if (_chiusa) return;
            if (p.Percentuale is double percento && p.Stato.StartsWith("pulling", StringComparison.Ordinal))
            {
                Avanzamento.IsIndeterminate = false;
                Avanzamento.Value = percento;
                Mostra(EsitoOllama, $"Scarico {modello}: {percento:0}%. Lascia aperta questa finestra fino alla fine.");
            }
            else
            {
                Avanzamento.IsIndeterminate = true;
                if (p.Stato.StartsWith("verifying", StringComparison.Ordinal)) Mostra(EsitoOllama, "Controllo il modello scaricato…");
                else if (p.Stato.StartsWith("writing", StringComparison.Ordinal)) Mostra(EsitoOllama, "Quasi fatto…");
            }
        });
        try
        {
            await ServizioOllama.ScaricaModelloAsync(indirizzo, modello, avanzamento, _cts.Token);
            Mostra(EsitoOllama, $"✓ Modello {modello} scaricato e pronto.", Verde);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_chiusa) Mostra(EsitoOllama, $"✗ {ex.Message}", Rosso);
        }
        finally
        {
            _scaricamentoInCorso = false;
            if (!_chiusa)
            {
                PulsanteScarica.IsEnabled = true;
                Avanzamento.IsVisible = false;
            }
        }
    }

    // ---- Prova ----

    async void SuProva(object? sender, RoutedEventArgs e)
    {
        PulsanteProva.IsEnabled = false;
        try
        {
            string arrivo = _imp.LinguaArrivo;
            var richiesta = arrivo == "en"
                ? new RichiestaTraduzione("Buongiorno a tutti, possiamo iniziare la riunione.", "it", arrivo, [])
                : new RichiestaTraduzione("Good morning everyone, we can start the meeting.", "en", arrivo, []);
            string glossario = Interprete.LeggiGlossario(_imp.FileGlossario);
            var prove = new List<Task>();

            if (IndirizzoOllama() is { } indirizzo && !_scaricamentoInCorso)
                prove.Add(ProvaOllamaAsync(indirizzo, ModelloOllama(), richiesta, glossario));
            prove.Add(ProvaIntegrataAsync(richiesta));
            prove.Add(ProvaAIAsync(EsitoGemini, ServizioAI.Gemini, Testo(ChiaveGemini), richiesta, glossario));
            prove.Add(ProvaAIAsync(EsitoGroq, ServizioAI.Groq, Testo(ChiaveGroq), richiesta, glossario));
            prove.Add(Testo(ChiaveDeepL) is { Length: > 0 } deepl
                ? ProvaAsync(EsitoDeepL, new ServizioDeepL(deepl), richiesta)
                : Nessuna(EsitoDeepL, "Nessuna chiave: DeepL non viene usato."));
            prove.Add(Testo(ChiaveMicrosoft) is { Length: > 0 } microsoft
                ? ProvaAsync(EsitoMicrosoft, new ServizioMicrosoft(microsoft, Testo(RegioneMicrosoft)), richiesta)
                : Nessuna(EsitoMicrosoft, "Nessuna chiave: Microsoft Translator non viene usato."));
            prove.Add(ProvaAsync(EsitoMyMemory, new ServizioMyMemory(Testo(EmailMyMemory)), richiesta));
            prove.Add(Testo(ChiaveClaude) is { Length: > 0 } claude
                ? ProvaAsync(EsitoClaude, new ServizioClaude(_imp.ModelloClaude, arrivo, glossario, claude), richiesta)
                : Nessuna(EsitoClaude, "Nessuna chiave."));
            await Task.WhenAll(prove);
        }
        finally
        {
            if (!_chiusa) PulsanteProva.IsEnabled = true;
        }
    }

    Task ProvaAIAsync(TextBlock esito, FornitoreAI fornitore, string chiave, RichiestaTraduzione richiesta, string glossario) =>
        chiave.Length > 0
            ? ProvaAsync(esito, new ServizioAI(fornitore, chiave, Servizi.ModelloAI(_imp, fornitore), richiesta.LinguaArrivo, glossario), richiesta)
            : Nessuna(esito, $"Nessuna chiave: {fornitore.Nome} non viene usato.");

    static Task Nessuna(TextBlock esito, string testo)
    {
        Mostra(esito, testo);
        return Task.CompletedTask;
    }

    async Task ProvaOllamaAsync(string indirizzo, string modello, RichiestaTraduzione richiesta, string glossario)
    {
        var stato = await ServizioOllama.ControllaAsync(indirizzo, modello, _cts.Token);
        if (stato != ServizioOllama.Stato.Pronto)
        {
            Mostra(EsitoOllama, stato == ServizioOllama.Stato.ModelloMancante
                ? "✗ Il modello non è scaricato: usa «Scarica il modello»."
                : "✗ Ollama non risponde: installalo e avvialo.", Rosso);
            return;
        }
        Mostra(EsitoOllama, "Provo… (la prima volta Ollama carica il modello in memoria: può volerci un minuto)");
        await ProvaAsync(EsitoOllama, new ServizioOllama(indirizzo, modello, richiesta.LinguaArrivo, glossario), richiesta);
    }

    async Task ProvaAsync(TextBlock esito, IServizioTraduzione servizio, RichiestaTraduzione richiesta)
    {
        if (!(esito.Text ?? "").StartsWith("Provo", StringComparison.Ordinal)) Mostra(esito, "Provo…");
        var cronometro = Stopwatch.StartNew();
        try
        {
            string? traduzione = await servizio.TraduciAsync(richiesta, _ => { }, _cts.Token);
            if (!_chiusa) Mostra(esito, $"✓ «{traduzione}»  ({cronometro.Elapsed.TotalSeconds:0.0} s)", Verde);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_chiusa) Mostra(esito, $"✗ {ex.Message}", Rosso);
        }
    }

    // ---- Salvataggio ----

    void SuSalva(object? sender, RoutedEventArgs e)
    {
        string? problema = null;
        string claude = Testo(ChiaveClaude);
        string email = Testo(EmailMyMemory);
        if (IndirizzoOllama() is null) problema = "L'indirizzo di Ollama non è valido.";
        else if (ModelloOllama().Length == 0) problema = "Indica il modello di Ollama (translategemma:4b).";
        else if (claude.Length > 0 && !claude.StartsWith("sk-ant-", StringComparison.Ordinal))
            problema = "La chiave di Claude non sembra valida: deve iniziare con sk-ant-.";
        else if (email.Length > 0 && (!email.Contains('@') || email.Any(char.IsWhiteSpace)))
            problema = "L'indirizzo email per MyMemory non sembra valido.";
        if (problema is not null)
        {
            Errore.Text = problema;
            Errore.IsVisible = true;
            return;
        }

        try
        {
            Credenziali.Salva(Credenziali.Gemini, Testo(ChiaveGemini));
            Credenziali.Salva(Credenziali.Groq, Testo(ChiaveGroq));
            Credenziali.Salva(Credenziali.DeepL, Testo(ChiaveDeepL));
            Credenziali.Salva(Credenziali.Microsoft, Testo(ChiaveMicrosoft));
            Credenziali.Salva(Credenziali.MyMemory, email);
            Credenziali.Salva(Credenziali.Claude, claude);
        }
        catch (Exception ex)
        {
            Errore.Text = $"Non riesco a salvare le chiavi: {ex.Message}";
            Errore.IsVisible = true;
            return;
        }
        _imp.UsaAIIntegrata = UsaIntegrata.IsChecked == true;
        _imp.OllamaIndirizzo = Testo(OllamaIndirizzo);
        _imp.OllamaModello = ModelloOllama();
        _imp.MicrosoftRegione = Testo(RegioneMicrosoft);
        _imp.Salva();
        _chiusuraConfermata = true;
        Close(true);
    }

    void SuAnnulla(object? sender, RoutedEventArgs e) => Close(false);

    async void SuChiusura(object? sender, WindowClosingEventArgs e)
    {
        if (!_scaricamentoInCorso || _chiusuraConfermata) return;
        e.Cancel = true;
        if (await Dialogo.ChiediAsync(this, Title ?? "Servizi di traduzione",
                "Lo scaricamento del modello è in corso: chiudendo la finestra si interrompe. Chiudere?"))
        {
            _chiusuraConfermata = true;
            Close(false);
        }
    }
}
