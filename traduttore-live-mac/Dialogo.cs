using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TraduttoreLive;

/// <summary>Le finestre di messaggio (Avalonia non ne ha): una domanda sì/no o un avviso con OK.</summary>
public static class Dialogo
{
    /// <summary>Fa una domanda; restituisce true se si sceglie il primo pulsante.</summary>
    public static Task<bool> ChiediAsync(Window? proprietario, string titolo, string testo, string si = "Sì", string no = "No") =>
        MostraFinestraAsync(proprietario, titolo, testo, si, no);

    public static Task MostraAsync(Window? proprietario, string titolo, string testo) =>
        MostraFinestraAsync(proprietario, titolo, testo, "OK", null);

    static async Task<bool> MostraFinestraAsync(Window? proprietario, string titolo, string testo, string si, string? no)
    {
        var finestra = new Window
        {
            Title = titolo,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = proprietario is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
        };

        bool risposta = false;
        var pulsanti = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 18, 0, 0),
        };
        if (no is not null)
        {
            var pulsanteNo = new Button { Content = no, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
            pulsanteNo.Click += (_, _) => finestra.Close(false);
            pulsanti.Children.Add(pulsanteNo);
        }
        var pulsanteSi = new Button { Content = si, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
        pulsanteSi.Classes.Add("accent");
        pulsanteSi.Click += (_, _) =>
        {
            risposta = true;
            finestra.Close(true);
        };
        pulsanti.Children.Add(pulsanteSi);

        finestra.Content = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 18),
            Children =
            {
                new TextBlock { Text = titolo, FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 10) },
                new TextBlock { Text = testo, TextWrapping = TextWrapping.Wrap },
                pulsanti,
            },
        };

        if (proprietario is { IsVisible: true })
            return await finestra.ShowDialog<bool>(proprietario);

        var chiusa = new TaskCompletionSource<bool>();
        finestra.Closed += (_, _) => chiusa.TrySetResult(risposta);
        finestra.Show();
        return await chiusa.Task;
    }
}
