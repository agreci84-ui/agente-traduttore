using System.Windows;
using System.Windows.Threading;

namespace TraduttoreLive;

public partial class App : Application
{
    void ErroreNonGestito(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Si è verificato un errore imprevisto:\n\n{e.Exception.Message}",
            "Traduttore dal vivo", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
