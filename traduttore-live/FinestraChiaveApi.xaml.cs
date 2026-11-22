using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using TraduttoreLive.Traduzione;

namespace TraduttoreLive;

/// <summary>Chiede la chiave API di Claude e la salva cifrata. DialogResult = true se la chiave è cambiata.</summary>
public partial class FinestraChiaveApi : Window
{
    public FinestraChiaveApi()
    {
        InitializeComponent();
        PulsanteRimuovi.Visibility = ChiaveApi.Salvata ? Visibility.Visible : Visibility.Collapsed;
    }

    void ApriPagina(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    void SuModifica(object sender, RoutedEventArgs e) => Errore.Visibility = Visibility.Collapsed;

    void SuSalva(object sender, RoutedEventArgs e)
    {
        string chiave = Chiave.Password.Trim();
        string? problema =
            chiave.Length == 0 ? "Incolla la chiave API." :
            !chiave.StartsWith("sk-ant-", StringComparison.Ordinal) || chiave.Any(char.IsWhiteSpace)
                ? "Questa non sembra una chiave API di Claude: deve iniziare con sk-ant- e non contenere spazi." :
            null;
        if (problema is not null)
        {
            Errore.Text = problema;
            Errore.Visibility = Visibility.Visible;
            Chiave.Focus();
            return;
        }

        try
        {
            ChiaveApi.Salva(chiave);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Errore.Text = $"Non riesco a salvare la chiave: {ex.Message}";
            Errore.Visibility = Visibility.Visible;
        }
    }

    void SuRimuovi(object sender, RoutedEventArgs e)
    {
        ChiaveApi.Rimuovi();
        DialogResult = true;
    }
}
