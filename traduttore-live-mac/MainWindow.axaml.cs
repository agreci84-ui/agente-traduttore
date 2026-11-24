using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    readonly ObservableCollection<RigaVista> _righe = [];
    readonly SemaphoreSlim _riavvio = new(1, 1);
    Motore? _motore;
    bool _seguiUltimaRiga = true;
    bool _chiusuraFatta;

    // Ridimensionamento dall'angolo in basso a destra.
    bool _ridimensiono;
    Vector _scartoManiglia;

    public MainWindow()
    {
        InitializeComponent();
        Elenco.ItemsSource = _righe;
        Scorrimento.ScrollChanged += SuScorrimento;
        Cornice.PointerPressed += SuPressione;
        Cornice.PointerReleased += SuRilascio;
        Maniglia.PointerPressed += IniziaRidimensiona;
        Maniglia.PointerMoved += Ridimensiona;
        Maniglia.PointerReleased += FineRidimensiona;
        AddHandler(PointerWheelChangedEvent, SuRotellina, RoutingStrategies.Tunnel);
        Opened += SuAperta;
        Closing += SuChiusura;
        ApplicaDimensioneTesto();
        Posiziona();
    }

    async void SuAperta(object? sender, EventArgs e)
    {
        FinestraMac.MostraOvunque(this);
        if (_imp.ServiziOnline is null)
        {
            _imp.ServiziOnline = await ChiediServiziOnlineAsync();
            _imp.Salva();
        }
        // La cartella Documenti è protetta: la prima volta macOS chiede il permesso. Meglio adesso che a metà riunione.
        if (_imp.SalvaTrascrizione)
            _ = Task.Run(() =>
            {
                try { Directory.CreateDirectory(RegistroRiunione.Cartella); }
                catch (Exception) { }
            });
        await RiavviaMotoreAsync();
    }

    // ---- Servizi online: si usano solo se l'utente li accetta ----

    Task<bool> ChiediServiziOnlineAsync() => Dialogo.ChiediAsync(this, "Servizi di traduzione online",
        "Traduttore dal vivo trascrive e traduce sul Mac con la sua AI integrata: l'audio e il testo non lasciano " +
        "il computer. La prima volta l'AI si scarica da sola (2,5 GB): ci vuole qualche minuto.\n\n" +
        "Mentre si scarica, o se il Mac è troppo lento per usarla, il programma può tradurre con servizi online " +
        "gratuiti (MyMemory e, se li configuri, Google Gemini, Groq, DeepL o Microsoft Translator), che ricevono il " +
        "testo trascritto, mai l'audio.\n\n" +
        "Vuoi usare anche i servizi online?\n\n" +
        "• Sì: traduce subito, anche mentre l'AI integrata si scarica.\n" +
        "• No: traduce solo sul Mac, appena l'AI integrata è pronta.\n\n" +
        "Puoi cambiare idea in qualsiasi momento dal menu › Traduzione.");

    /// <summary>Mostra la finestra dei servizi di traduzione e, se qualcosa è stato salvato, riavvia la traduzione.</summary>
    public async Task ApriServiziERiavviaAsync()
    {
        if (OwnedWindows.Count > 0) return; // c'è già una finestra aperta
        if (await new FinestraServizi(_imp).ShowDialog<bool>(this)) await RiavviaMotoreAsync();
    }

    // ---- Motore ----

    async Task RiavviaMotoreAsync()
    {
        await _riavvio.WaitAsync();
        try
        {
            if (_chiusuraFatta) return;
            if (_motore is not null)
            {
                TestoStato.Text = "Riavvio…";
                var vecchio = _motore;
                _motore = null;
                await Task.Run(() => vecchio.DisposeAsync().AsTask());
            }

            var motore = new Motore(_imp, _registro);
            motore.StatoCambiato += s => Dispatcher.UIThread.Post(() =>
            {
                if (_motore == motore) TestoStato.Text = s;
            });
            motore.NuovaRiga += r => Dispatcher.UIThread.Post(() =>
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
        _righe.Add(new RigaVista(riga));
        while (_righe.Count > RigheMassime) _righe.RemoveAt(0);
    }

    // Scorre da solo verso l'ultima frase, a meno che tu non sia risalito a rileggere.
    void SuScorrimento(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Y == 0)
            _seguiUltimaRiga = Scorrimento.Offset.Y >= Scorrimento.Extent.Height - Scorrimento.Viewport.Height - 4;
        else if (_seguiUltimaRiga)
            Dispatcher.UIThread.Post(Scorrimento.ScrollToEnd);
    }

    // ---- Pulsanti e mouse ----

    void SuPausa(object? sender, RoutedEventArgs e)
    {
        if (_motore is null) return;
        _motore.InPausa = !_motore.InPausa;
        AggiornaPulsantePausa();
    }

    void AggiornaPulsantePausa()
    {
        bool inPausa = _motore?.InPausa ?? false;
        PulsantePausa.Content = inPausa ? "▶︎" : "⏸︎";
        ToolTip.SetTip(PulsantePausa, inPausa ? "Riprendi" : "Metti in pausa");
    }

    void SuMenu(object? sender, RoutedEventArgs e) => ApriMenu(PulsanteMenu, false);

    /// <summary>Dalla barra dei menu del Mac.</summary>
    public void ApriMenuDaFuori() => ApriMenu(PulsanteMenu, false);

    void SuChiudi(object? sender, RoutedEventArgs e) => Close();

    void SuPressione(object? sender, PointerPressedEventArgs e)
    {
        var punto = e.GetCurrentPoint(this);
        if (!punto.Properties.IsLeftButtonPressed) return;
        // Ctrl + clic è il clic destro del Mac.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ApriMenu(Cornice, true);
            e.Handled = true;
            return;
        }
        BeginMoveDrag(e);
    }

    void SuRilascio(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right) ApriMenu(Cornice, true);
    }

    void SuRotellina(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Meta) && !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (e.Delta.Y == 0) return;
        CambiaDimensioneTesto(e.Delta.Y > 0 ? 2 : -2);
        e.Handled = true;
    }

    void IniziaRidimensiona(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        _scartoManiglia = new Vector(Bounds.Width - p.X, Bounds.Height - p.Y);
        _ridimensiono = true;
        e.Pointer.Capture(Maniglia);
        e.Handled = true;
    }

    void Ridimensiona(object? sender, PointerEventArgs e)
    {
        if (!_ridimensiono) return;
        // L'angolo in alto a sinistra resta fermo: la posizione del puntatore nella finestra dà la nuova dimensione.
        var p = e.GetPosition(this);
        Width = Math.Max(MinWidth, p.X + _scartoManiglia.X);
        Height = Math.Max(MinHeight, p.Y + _scartoManiglia.Y);
        e.Handled = true;
    }

    void FineRidimensiona(object? sender, PointerReleasedEventArgs e)
    {
        if (!_ridimensiono) return;
        _ridimensiono = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    // ---- Menu ----

    void ApriMenu(Control origine, bool alPuntatore)
    {
        var menu = new MenuFlyout
        {
            Placement = alPuntatore ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedRight,
        };

        var arrivo = Sottomenu(menu.Items, "Lingua dei sottotitoli");
        foreach (var (codice, nome) in Lingue.Elenco)
            Voce(arrivo, nome, _imp.LinguaArrivo == codice, () => Cambia(() => _imp.LinguaArrivo = codice));

        var parlata = Sottomenu(menu.Items, "Lingua parlata nella riunione");
        Voce(parlata, "Riconosci automaticamente", _imp.LinguaParlata == "auto", () => Cambia(() => _imp.LinguaParlata = "auto"));
        parlata.Add(new Separator());
        foreach (var (codice, nome) in Lingue.Elenco)
            Voce(parlata, nome, _imp.LinguaParlata == codice, () => Cambia(() => _imp.LinguaParlata = codice));

        var ascolta = Sottomenu(menu.Items, "Cosa ascoltare");
        CompilaSorgenti(ascolta);

        var modello = Sottomenu(menu.Items, "Precisione della trascrizione");
        foreach (var (nome, descrizione) in Trascrittore.Modelli)
            Voce(modello, descrizione, _imp.ModelloWhisper == nome, () => Cambia(() => _imp.ModelloWhisper = nome));
        modello.Add(new Separator());
        Voce(modello, "Usa la scheda grafica (dal prossimo avvio)", _imp.UsaSchedaGrafica, () =>
        {
            _imp.UsaSchedaGrafica = !_imp.UsaSchedaGrafica;
            _imp.Salva();
        });

        var traduzione = Sottomenu(menu.Items, "Traduzione");
        // In vista solo le scelte principali; gli altri servizi sono in un sottomenu.
        bool sceltaNota = Servizi.Elenco.Any(s => s.Codice == _imp.ServizioTraduzione);
        var altri = new MenuItem { Header = "Un solo servizio" };
        foreach (var (codice, nome) in Servizi.Elenco)
        {
            bool scelto = _imp.ServizioTraduzione == codice || (!sceltaNota && codice == Servizi.Automatico);
            Voce(codice is Servizi.Automatico or Servizi.Integrata ? traduzione : altri.Items, nome, scelto,
                () => Cambia(() => _imp.ServizioTraduzione = codice));
        }
        traduzione.Add(altri);
        traduzione.Add(new Separator());
        Voce(traduzione, "Usa l'AI integrata (la prima volta si scarica: 2,5 GB)", _imp.UsaAIIntegrata,
            () => Cambia(() => _imp.UsaAIIntegrata = !_imp.UsaAIIntegrata));
        Voce(traduzione, "Usa i servizi online (ricevono il testo trascritto)", _imp.ServiziOnline == true, async () =>
        {
            bool attivare = _imp.ServiziOnline != true;
            if (attivare && !await ChiediServiziOnlineAsync()) return;
            Cambia(() => _imp.ServiziOnline = attivare);
        });
        Voce(traduzione, "Servizi e chiavi…", null, async () => await ApriServiziERiavviaAsync());

        menu.Items.Add(new Separator());
        Voce(menu.Items, "Mostra il testo originale", _imp.MostraOriginale, () =>
        {
            _imp.MostraOriginale = !_imp.MostraOriginale;
            _imp.Salva();
            foreach (var r in _righe) r.Riga.MostraOriginale = _imp.MostraOriginale;
        });
        Voce(menu.Items, "Testo più grande     (⌘ + rotellina)", null, () => CambiaDimensioneTesto(2));
        Voce(menu.Items, "Testo più piccolo", null, () => CambiaDimensioneTesto(-2));
        Voce(menu.Items, "Cancella i sottotitoli", null, _righe.Clear);

        menu.Items.Add(new Separator());
        Voce(menu.Items, "Salva la trascrizione della riunione", _imp.SalvaTrascrizione, () =>
        {
            _imp.SalvaTrascrizione = !_imp.SalvaTrascrizione;
            _imp.Salva();
        });
        Voce(menu.Items, "Apri le trascrizioni salvate", null, () => ApriCartella(RegistroRiunione.Cartella));
        Voce(menu.Items, "Apri la cartella delle impostazioni", null, () => ApriCartella(Impostazioni.Cartella));
        Voce(menu.Items, "Permesso di registrare l'audio…", null, ApriPermessoAudio);

        menu.Items.Add(new Separator());
        Voce(menu.Items, "Esci", null, Close);

        menu.ShowAt(origine, alPuntatore);
    }

    void CompilaSorgenti(ItemCollection ascolta)
    {
        bool tutto = _imp.Sorgente != "programma";
        Voce(ascolta, "Tutto l'audio del Mac", tutto, () => Cambia(() =>
        {
            _imp.Sorgente = "dispositivo";
            _imp.DispositivoId = null;
        }));

        ascolta.Add(new Separator());
        ascolta.Add(new MenuItem { Header = "Solo l'audio di un'app:", IsEnabled = false });
        var programmi = Programmi.ConAudioAperto();
        if (!tutto && _imp.NomeProgramma is { } attuale
            && !programmi.Any(p => p.NomeProcesso.Equals(attuale, StringComparison.OrdinalIgnoreCase)))
            programmi.Insert(0, new ProgrammaAudio(attuale, attuale));
        if (programmi.Count == 0)
            ascolta.Add(new MenuItem { Header = "   (nessuna app sta usando l'audio adesso)", IsEnabled = false });
        foreach (var p in programmi)
        {
            bool scelto = !tutto && p.NomeProcesso.Equals(_imp.NomeProgramma, StringComparison.OrdinalIgnoreCase);
            Voce(ascolta, "   " + p.NomeVisibile, scelto, () => Cambia(() =>
            {
                _imp.Sorgente = "programma";
                _imp.NomeProgramma = p.NomeProcesso;
            }));
        }
    }

    static ItemCollection Sottomenu(ItemCollection padre, string titolo)
    {
        var voce = new MenuItem { Header = titolo };
        padre.Add(voce);
        return voce.Items;
    }

    /// <param name="selezionata">null per le azioni; true/false per le scelte, che mostrano il segno di spunta.</param>
    static void Voce(ItemCollection padre, string titolo, bool? selezionata, Action azione)
    {
        var voce = new MenuItem { Header = titolo };
        if (selezionata is bool scelta)
        {
            voce.ToggleType = MenuItemToggleType.CheckBox;
            voce.IsChecked = scelta;
        }
        voce.Click += (_, _) => azione();
        padre.Add(voce);
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
        Apri(cartella);
    }

    /// <summary>Impostazioni di Sistema › Privacy e sicurezza › Registrazione schermo e audio di sistema.</summary>
    static void ApriPermessoAudio() =>
        Apri("x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");

    static void Apri(string destinazione)
    {
        try
        {
            var info = new ProcessStartInfo("open") { UseShellExecute = false };
            info.ArgumentList.Add(destinazione);
            Process.Start(info)?.Dispose();
        }
        catch (Exception)
        {
        }
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
        var schermo = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (schermo is null) return;

        if (_imp.FinestraSinistra is double x && _imp.FinestraAlto is double y)
        {
            var salvata = new PixelRect(new PixelPoint((int)x, (int)y), PixelSize.FromSize(new Size(Width, Height), schermo.Scaling));
            if (Screens.All.Any(s => s.Bounds.Intersects(salvata)))
            {
                Position = salvata.Position;
                return;
            }
        }

        // La prima volta: in basso al centro, dove di solito stanno i sottotitoli.
        var area = schermo.WorkingArea;
        var dimensione = PixelSize.FromSize(new Size(Width, Height), schermo.Scaling);
        Position = new PixelPoint(area.X + (area.Width - dimensione.Width) / 2,
            area.Bottom - dimensione.Height - (int)(24 * schermo.Scaling));
    }

    void SuChiusura(object? sender, WindowClosingEventArgs e)
    {
        if (_chiusuraFatta) return;
        _chiusuraFatta = true;
        _imp.FinestraSinistra = Position.X;
        _imp.FinestraAlto = Position.Y;
        _imp.FinestraLarghezza = Bounds.Width;
        _imp.FinestraAltezza = Bounds.Height;
        _imp.Salva();

        var motore = _motore;
        _motore = null;
        if (motore is not null)
            Task.Run(() => motore.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(3));
        _registro.Dispose();
    }
}
