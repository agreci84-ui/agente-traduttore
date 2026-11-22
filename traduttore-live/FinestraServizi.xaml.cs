using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using TraduttoreLive.Traduzione;

namespace TraduttoreLive;

/// <summary>
/// Configura i servizi di traduzione: chiavi ed email (salvate cifrate), Ollama e il suo modello.
/// DialogResult = true se qualcosa è stato salvato.
/// </summary>
public partial class FinestraServizi : Window
{
    static readonly Brush Verde = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));
    static readonly Brush Rosso = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    static readonly Brush Grigio = new SolidColorBrush(Color.FromRgb(0x5F, 0x63, 0x68));

    readonly Impostazioni _imp;
    readonly CancellationTokenSource _cts = new();
    bool _scaricamentoInCorso;

    public FinestraServizi(Impostazioni imp)
    {
        InitializeComponent();
        _imp = imp;
        OllamaModello.ItemsSource = new[] { "translategemma:4b", "translategemma:12b" };
        OllamaModello.Text = imp.OllamaModello;
        OllamaIndirizzo.Text = imp.OllamaIndirizzo;
        ChiaveDeepL.Password = Credenziali.Leggi(Credenziali.DeepL) ?? "";
        ChiaveMicrosoft.Password = Credenziali.Leggi(Credenziali.Microsoft) ?? "";
        RegioneMicrosoft.Text = imp.MicrosoftRegione;
        EmailMyMemory.Text = Credenziali.Leggi(Credenziali.MyMemory) ?? "";
        ChiaveClaude.Password = Credenziali.Leggi(Credenziali.Claude) ?? "";
        Loaded += async (_, _) => await ControllaOllamaAsync();
    }

    void ApriPagina(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    static void Mostra(TextBlock esito, string testo, Brush? colore = null)
    {
        esito.Text = testo;
        esito.Foreground = colore ?? Grigio;
    }

    // ---- Ollama ----

    string? IndirizzoOllama()
    {
        string testo = OllamaIndirizzo.Text.Trim();
        if (Uri.TryCreate(testo, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return testo;
        Mostra(EsitoOllama, "✗ L'indirizzo di Ollama non è valido (quello predefinito è http://localhost:11434).", Rosso);
        return null;
    }

    async void SuControllaOllama(object sender, RoutedEventArgs e) => await ControllaOllamaAsync();

    async Task ControllaOllamaAsync()
    {
        if (_scaricamentoInCorso || IndirizzoOllama() is not { } indirizzo) return;
        Mostra(EsitoOllama, "Controllo Ollama…");
        try
        {
            var stato = await ServizioOllama.ControllaAsync(indirizzo, OllamaModello.Text, _cts.Token);
            switch (stato)
            {
                case ServizioOllama.Stato.Pronto:
                    Mostra(EsitoOllama, "✓ Ollama è avviato e il modello è pronto.", Verde);
                    break;
                case ServizioOllama.Stato.ModelloMancante:
                    Mostra(EsitoOllama, "Ollama è avviato, ma il modello non è ancora scaricato: usa «Scarica il modello».");
                    break;
                default:
                    Mostra(EsitoOllama, "Ollama non risponde: installalo e avvialo (dopo l'installazione parte da solo con Windows).");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    async void SuScaricaModello(object sender, RoutedEventArgs e)
    {
        if (_scaricamentoInCorso || IndirizzoOllama() is not { } indirizzo) return;
        string modello = OllamaModello.Text.Trim();
        if (modello.Length == 0)
        {
            Mostra(EsitoOllama, "✗ Indica il modello da scaricare.", Rosso);
            return;
        }

        _scaricamentoInCorso = true;
        PulsanteScarica.IsEnabled = false;
        Avanzamento.Visibility = Visibility.Visible;
        Avanzamento.IsIndeterminate = true;
        Mostra(EsitoOllama, "Preparo lo scaricamento… Lascia aperta questa finestra fino alla fine.");
        var avanzamento = new Progress<(string Stato, double? Percentuale)>(p =>
        {
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
            Mostra(EsitoOllama, $"✗ {ex.Message}", Rosso);
        }
        finally
        {
            _scaricamentoInCorso = false;
            if (IsLoaded)
            {
                PulsanteScarica.IsEnabled = true;
                Avanzamento.Visibility = Visibility.Collapsed;
            }
        }
    }

    // ---- Prova ----

    async void SuProva(object sender, RoutedEventArgs e)
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
            {
                string modello = OllamaModello.Text.Trim();
                prove.Add(ProvaOllamaAsync(indirizzo, modello, richiesta, glossario));
            }
            prove.Add(ChiaveDeepL.Password.Trim() is { Length: > 0 } deepl
                ? ProvaAsync(EsitoDeepL, new ServizioDeepL(deepl), richiesta)
                : Nessuna(EsitoDeepL, "Nessuna chiave: DeepL non viene usato."));
            prove.Add(ChiaveMicrosoft.Password.Trim() is { Length: > 0 } microsoft
                ? ProvaAsync(EsitoMicrosoft, new ServizioMicrosoft(microsoft, RegioneMicrosoft.Text), richiesta)
                : Nessuna(EsitoMicrosoft, "Nessuna chiave: Microsoft Translator non viene usato."));
            prove.Add(ProvaAsync(EsitoMyMemory, new ServizioMyMemory(EmailMyMemory.Text), richiesta));
            prove.Add(ChiaveClaude.Password.Trim() is { Length: > 0 } claude
                ? ProvaAsync(EsitoClaude, new ServizioClaude(_imp.ModelloClaude, arrivo, glossario, claude), richiesta)
                : Nessuna(EsitoClaude, "Nessuna chiave."));
            await Task.WhenAll(prove);
        }
        finally
        {
            if (IsLoaded) PulsanteProva.IsEnabled = true;
        }
    }

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
        if (esito.Text.Length == 0 || !esito.Text.StartsWith("Provo", StringComparison.Ordinal)) Mostra(esito, "Provo…");
        var cronometro = Stopwatch.StartNew();
        try
        {
            string? traduzione = await servizio.TraduciAsync(richiesta, _ => { }, _cts.Token);
            Mostra(esito, $"✓ «{traduzione}»  ({cronometro.Elapsed.TotalSeconds:0.0} s)", Verde);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Mostra(esito, $"✗ {ex.Message}", Rosso);
        }
    }

    // ---- Salvataggio ----

    void SuSalva(object sender, RoutedEventArgs e)
    {
        string? problema = null;
        string claude = ChiaveClaude.Password.Trim();
        string email = EmailMyMemory.Text.Trim();
        if (IndirizzoOllama() is null) problema = "L'indirizzo di Ollama non è valido.";
        else if (OllamaModello.Text.Trim().Length == 0) problema = "Indica il modello di Ollama (translategemma:4b).";
        else if (claude.Length > 0 && !claude.StartsWith("sk-ant-", StringComparison.Ordinal))
            problema = "La chiave di Claude non sembra valida: deve iniziare con sk-ant-.";
        else if (email.Length > 0 && (!email.Contains('@') || email.Any(char.IsWhiteSpace)))
            problema = "L'indirizzo email per MyMemory non sembra valido.";
        if (problema is not null)
        {
            Errore.Text = problema;
            Errore.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            Credenziali.Salva(Credenziali.DeepL, ChiaveDeepL.Password);
            Credenziali.Salva(Credenziali.Microsoft, ChiaveMicrosoft.Password);
            Credenziali.Salva(Credenziali.MyMemory, email);
            Credenziali.Salva(Credenziali.Claude, claude);
        }
        catch (Exception ex)
        {
            Errore.Text = $"Non riesco a salvare le chiavi: {ex.Message}";
            Errore.Visibility = Visibility.Visible;
            return;
        }
        _imp.OllamaIndirizzo = OllamaIndirizzo.Text.Trim();
        _imp.OllamaModello = OllamaModello.Text.Trim();
        _imp.MicrosoftRegione = RegioneMicrosoft.Text.Trim();
        _imp.Salva();
        DialogResult = true;
    }

    void SuChiusura(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_scaricamentoInCorso && DialogResult is null
            && MessageBox.Show(this, "Lo scaricamento del modello è in corso: chiudendo la finestra si interrompe. Chiudere?",
                   Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _cts.Cancel();
    }
}
