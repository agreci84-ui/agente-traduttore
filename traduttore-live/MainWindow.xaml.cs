using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using TraduttoreLive.Audio;
using TraduttoreLive.Traduzione;
using TraduttoreLive.Trascrizione;

namespace TraduttoreLive;

/// <summary>Finestra dei sottotitoli: sempre in primo piano, trascinabile, ridimensionabile.</summary>
public partial class MainWindow : Window
{
    const int RigheMassime = 200;

    readonly Impostazioni _imp = Impostazioni.Carica();
    readonly RegistroRiunione _registro = new();
    readonly ObservableCollection<RigaSottotitolo> _righe = [];
    readonly SemaphoreSlim _riavvio = new(1, 1);
    Motore? _motore;
    bool _seguiUltimaRiga = true;

    public MainWindow()
    {
        InitializeComponent();
        Elenco.ItemsSource = _righe;
        Scorrimento.ScrollChanged += SuScorrimento;
        Cornice.MouseRightButtonUp += (_, _) => ApriMenu(null);
        ApplicaDimensioneTesto();
        Posiziona();
    }

    async void SuCaricata(object sender, RoutedEventArgs e)
    {
        ApplicaSceltaInstallazione();
        if (_imp.ServiziOnline is null)
        {
            _imp.ServiziOnline = ChiediServiziOnline();
            _imp.Salva();
        }
        await RiavviaMotoreAsync();
    }

    // ---- Servizi online: si usano solo se l'utente li accetta ----

    const string ChiaveRegistro = @"Software\TraduttoreLive";

    /// <summary>
    /// L'installer registra la scelta fatta durante l'installazione («Usa i servizi di traduzione online»):
    /// la si applica una volta e poi la si toglie, così da lì in avanti vale quella del menu.
    /// </summary>
    void ApplicaSceltaInstallazione()
    {
        try
        {
            using var chiave = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ChiaveRegistro, writable: true);
            if (chiave?.GetValue("ServiziOnline") is int valore)
            {
                _imp.ServiziOnline = valore != 0;
                _imp.Salva();
                chiave.DeleteValue("ServiziOnline", throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // Registro non leggibile: si chiede all'utente.
        }
    }

    bool ChiediServiziOnline() => MessageBox.Show(this,
        "Traduttore dal vivo trascrive e traduce sul PC con la sua AI integrata: l'audio e il testo non lasciano " +
        "il computer. La prima volta l'AI si scarica da sola (2,5 GB): ci vuole qualche minuto.\n\n" +
        "Mentre si scarica, o se il PC è troppo lento per usarla, il programma può tradurre con servizi online " +
        "gratuiti (MyMemory e, se li configuri, Google Gemini, Groq, DeepL o Microsoft Translator), che ricevono il " +
        "testo trascritto, mai l'audio.\n\n" +
        "Vuoi usare anche i servizi online?\n\n" +
        "• Sì: traduce subito, anche mentre l'AI integrata si scarica.\n" +
        "• No: traduce solo sul PC, appena l'AI integrata è pronta.\n\n" +
        "Puoi cambiare idea in qualsiasi momento dal menu › Traduzione.",
        "Servizi di traduzione online", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Mostra la finestra dei servizi di traduzione. Restituisce true se qualcosa è stato salvato.</summary>
    bool ApriServizi() => new FinestraServizi(_imp) { Owner = this }.ShowDialog() == true;

    // ---- Motore ----

    async Task RiavviaMotoreAsync()
    {
        await _riavvio.WaitAsync();
        try
        {
            if (_motore is not null)
            {
                TestoStato.Text = "Riavvio…";
                var vecchio = _motore;
                _motore = null;
                await Task.Run(() => vecchio.DisposeAsync().AsTask());
            }

            var motore = new Motore(_imp, _registro);
            motore.StatoCambiato += s => Dispatcher.BeginInvoke(() =>
            {
                if (_motore == motore) TestoStato.Text = s;
            });
            motore.NuovaRiga += r => Dispatcher.BeginInvoke(() =>
            {
                if (_motore == motore) AggiungiRiga(r);
            });
            _motore = motore;
            AggiornaPulsantePausa();

            try
            {
                await Task.Run(motore.AvviaAsync);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TestoStato.Text = "Non avviato";
                AggiungiRiga(new RigaSottotitolo(DateTime.Now, "", $"Non riesco ad avviare la traduzione: {ex.Message}", false) { Avviso = true });
            }
        }
        finally
        {
            _riavvio.Release();
        }
    }

    void AggiungiRiga(RigaSottotitolo riga)
    {
        _righe.Add(riga);
        while (_righe.Count > RigheMassime) _righe.RemoveAt(0);
    }

    // Scorre da solo verso l'ultima frase, a meno che tu non sia risalito a rileggere.
    void SuScorrimento(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0)
            _seguiUltimaRiga = Scorrimento.VerticalOffset >= Scorrimento.ScrollableHeight - 4;
        else if (_seguiUltimaRiga)
            Scorrimento.ScrollToEnd();
    }

    // ---- Pulsanti ----

    void SuPausa(object sender, RoutedEventArgs e)
    {
        if (_motore is null) return;
        _motore.InPausa = !_motore.InPausa;
        AggiornaPulsantePausa();
    }

    void AggiornaPulsantePausa()
    {
        bool inPausa = _motore?.InPausa ?? false;
        PulsantePausa.Content = inPausa ? "" : "";
        PulsantePausa.ToolTip = inPausa ? "Riprendi" : "Metti in pausa";
    }

    void SuMenu(object sender, RoutedEventArgs e) => ApriMenu((Button)sender);

    void SuChiudi(object sender, RoutedEventArgs e) => Close();

    void Trascina(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void SuRotellina(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        CambiaDimensioneTesto(e.Delta > 0 ? 2 : -2);
        e.Handled = true;
    }

    // ---- Menu ----

    void ApriMenu(Button? origine)
    {
        var menu = new ContextMenu();

        var arrivo = Sottomenu(menu, "Lingua dei sottotitoli");
        foreach (var (codice, nome) in Lingue.Elenco)
            Voce(arrivo, nome, _imp.LinguaArrivo == codice, () => Cambia(() => _imp.LinguaArrivo = codice));

        var parlata = Sottomenu(menu, "Lingua parlata nella riunione");
        Voce(parlata, "Riconosci automaticamente", _imp.LinguaParlata == "auto", () => Cambia(() => _imp.LinguaParlata = "auto"));
        parlata.Items.Add(new Separator());
        foreach (var (codice, nome) in Lingue.Elenco)
            Voce(parlata, nome, _imp.LinguaParlata == codice, () => Cambia(() => _imp.LinguaParlata = codice));

        var ascolta = Sottomenu(menu, "Cosa ascoltare");
        CompilaSorgenti(ascolta);

        var modello = Sottomenu(menu, "Precisione della trascrizione");
        foreach (var (nome, descrizione) in Trascrittore.Modelli)
            Voce(modello, descrizione, _imp.ModelloWhisper == nome, () => Cambia(() => _imp.ModelloWhisper = nome));
        modello.Items.Add(new Separator());
        Voce(modello, "Usa la scheda grafica (dal prossimo avvio)", _imp.UsaSchedaGrafica, () =>
        {
            _imp.UsaSchedaGrafica = !_imp.UsaSchedaGrafica;
            _imp.Salva();
        });

        var traduzione = Sottomenu(menu, "Traduzione");
        // In vista solo le scelte principali; gli altri servizi sono in un sottomenu.
        bool sceltaNota = Servizi.Elenco.Any(s => s.Codice == _imp.ServizioTraduzione);
        var altri = new MenuItem { Header = "Un solo servizio" };
        foreach (var (codice, nome) in Servizi.Elenco)
        {
            bool scelto = _imp.ServizioTraduzione == codice || (!sceltaNota && codice == Servizi.Automatico);
            Voce(codice is Servizi.Automatico or Servizi.Integrata ? traduzione : altri, nome, scelto,
                () => Cambia(() => _imp.ServizioTraduzione = codice));
        }
        traduzione.Items.Add(altri);
        traduzione.Items.Add(new Separator());
        Voce(traduzione, "Usa l'AI integrata (la prima volta si scarica: 2,5 GB)", _imp.UsaAIIntegrata,
            () => Cambia(() => _imp.UsaAIIntegrata = !_imp.UsaAIIntegrata));
        Voce(traduzione, "Usa i servizi online (ricevono il testo trascritto)", _imp.ServiziOnline == true, () =>
        {
            bool attivare = _imp.ServiziOnline != true;
            if (attivare && !ChiediServiziOnline()) return;
            Cambia(() => _imp.ServiziOnline = attivare);
        });
        Voce(traduzione, "Servizi e chiavi…", false, async () =>
        {
            if (ApriServizi()) await RiavviaMotoreAsync();
        });

        menu.Items.Add(new Separator());
        Voce(menu, "Mostra il testo originale", _imp.MostraOriginale, () =>
        {
            _imp.MostraOriginale = !_imp.MostraOriginale;
            _imp.Salva();
            foreach (var r in _righe) r.MostraOriginale = _imp.MostraOriginale;
        });
        Voce(menu, "Testo più grande     (Ctrl + rotellina)", false, () => CambiaDimensioneTesto(2));
        Voce(menu, "Testo più piccolo", false, () => CambiaDimensioneTesto(-2));
        Voce(menu, "Cancella i sottotitoli", false, _righe.Clear);

        menu.Items.Add(new Separator());
        Voce(menu, "Salva la trascrizione della riunione", _imp.SalvaTrascrizione, () =>
        {
            _imp.SalvaTrascrizione = !_imp.SalvaTrascrizione;
            _imp.Salva();
        });
        Voce(menu, "Apri le trascrizioni salvate", false, () => ApriCartella(RegistroRiunione.Cartella));
        Voce(menu, "Apri la cartella delle impostazioni", false, () => ApriCartella(Impostazioni.Cartella));

        menu.Items.Add(new Separator());
        Voce(menu, "Esci", false, Close);

        if (origine is not null)
        {
            menu.PlacementTarget = origine;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        }
        menu.IsOpen = true;
    }

    void CompilaSorgenti(MenuItem ascolta)
    {
        bool dispositivo = _imp.Sorgente != "programma";
        Voce(ascolta, "Tutto l'audio del PC (uscita predefinita)", dispositivo && _imp.DispositivoId is null, () => Cambia(() =>
        {
            _imp.Sorgente = "dispositivo";
            _imp.DispositivoId = null;
        }));

        try
        {
            using var enumeratore = new MMDeviceEnumerator();
            foreach (var d in enumeratore.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                string id = d.ID;
                Voce(ascolta, $"Tutto l'audio di: {d.FriendlyName}", dispositivo && _imp.DispositivoId == id, () => Cambia(() =>
                {
                    _imp.Sorgente = "dispositivo";
                    _imp.DispositivoId = id;
                }));
                d.Dispose();
            }
        }
        catch (Exception)
        {
            // Elenco dei dispositivi non disponibile: resta la voce del dispositivo predefinito.
        }

        ascolta.Items.Add(new Separator());
        ascolta.Items.Add(new MenuItem { Header = "Solo l'audio di un programma:", IsEnabled = false });
        var programmi = Programmi.ConAudioAperto();
        if (!dispositivo && _imp.NomeProgramma is { } attuale
            && !programmi.Any(p => p.NomeProcesso.Equals(attuale, StringComparison.OrdinalIgnoreCase)))
            programmi.Insert(0, new ProgrammaAudio(attuale, attuale));
        if (programmi.Count == 0)
            ascolta.Items.Add(new MenuItem { Header = "   (nessun programma sta usando l'audio adesso)", IsEnabled = false });
        foreach (var p in programmi)
        {
            bool scelto = !dispositivo && p.NomeProcesso.Equals(_imp.NomeProgramma, StringComparison.OrdinalIgnoreCase);
            Voce(ascolta, "   " + p.NomeVisibile, scelto, () => Cambia(() =>
            {
                _imp.Sorgente = "programma";
                _imp.NomeProgramma = p.NomeProcesso;
            }));
        }
    }

    static MenuItem Sottomenu(ItemsControl padre, string titolo)
    {
        var voce = new MenuItem { Header = titolo };
        padre.Items.Add(voce);
        return voce;
    }

    static void Voce(ItemsControl padre, string titolo, bool selezionata, Action azione)
    {
        var voce = new MenuItem { Header = titolo, IsChecked = selezionata };
        voce.Click += (_, _) => azione();
        padre.Items.Add(voce);
    }

    /// <summary>Cambia un'impostazione che richiede di riavviare ascolto e trascrizione.</summary>
    async void Cambia(Action modifica)
    {
        modifica();
        _imp.Salva();
        await RiavviaMotoreAsync();
    }

    static void ApriCartella(string cartella)
    {
        Directory.CreateDirectory(cartella);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{cartella}\"") { UseShellExecute = true });
    }

    // ---- Aspetto e posizione ----

    void CambiaDimensioneTesto(double delta)
    {
        _imp.DimensioneTesto = Math.Clamp(_imp.DimensioneTesto + delta, 12, 72);
        ApplicaDimensioneTesto();
        _imp.Salva();
    }

    void ApplicaDimensioneTesto()
    {
        Resources["DimensioneTesto"] = _imp.DimensioneTesto;
        Resources["DimensioneOriginale"] = Math.Max(11.0, Math.Round(_imp.DimensioneTesto * 0.62));
    }

    void Posiziona()
    {
        Width = Math.Max(MinWidth, _imp.FinestraLarghezza);
        Height = Math.Max(MinHeight, _imp.FinestraAltezza);
        var area = SystemParameters.WorkArea;
        var schermi = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        if (_imp.FinestraSinistra is double x && _imp.FinestraAlto is double y
            && schermi.IntersectsWith(new Rect(x, y, Width, Height)))
        {
            Left = x;
            Top = y;
        }
        else
        {
            // La prima volta: in basso al centro, dove di solito stanno i sottotitoli.
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Bottom - Height - 24;
        }
    }

    void SuChiusura(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _imp.FinestraSinistra = Left;
        _imp.FinestraAlto = Top;
        _imp.FinestraLarghezza = ActualWidth;
        _imp.FinestraAltezza = ActualHeight;
        _imp.Salva();

        var motore = _motore;
        _motore = null;
        if (motore is not null)
            Task.Run(() => motore.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(3));
        _registro.Dispose();
    }
}
