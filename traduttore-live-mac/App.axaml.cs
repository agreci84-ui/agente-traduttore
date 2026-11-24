using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TraduttoreLive;

public partial class App : Application
{
    MainWindow? _finestra;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += ErroreNonGestito;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _finestra = new MainWindow();
            desktop.MainWindow = _finestra;
            NativeMenu.SetMenu(this, MenuApplicazione(desktop));
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Il menu «Traduttore dal vivo» nella barra dei menu del Mac.</summary>
    NativeMenu MenuApplicazione(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var servizi = new NativeMenuItem("Servizi di traduzione…") { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
        servizi.Click += async (_, _) => { if (_finestra is not null) await _finestra.ApriServiziERiavviaAsync(); };
        var menu = new NativeMenuItem("Menu dei sottotitoli");
        menu.Click += (_, _) => _finestra?.ApriMenuDaFuori();
        var esci = new NativeMenuItem("Esci da Traduttore dal vivo") { Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta) };
        esci.Click += (_, _) => desktop.Shutdown();
        return new NativeMenu { servizi, menu, new NativeMenuItemSeparator(), esci };
    }

    void ErroreNonGestito(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _ = Dialogo.MostraAsync(_finestra, "Traduttore dal vivo", $"Si è verificato un errore imprevisto:\n\n{e.Exception.Message}");
    }
}
