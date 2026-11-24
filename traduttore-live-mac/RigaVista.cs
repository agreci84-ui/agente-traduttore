using System.ComponentModel;
using Avalonia.Threading;

namespace TraduttoreLive;

/// <summary>
/// Una riga dei sottotitoli vista dall'interfaccia. La traduzione arriva da un thread in background, mentre
/// Avalonia accetta modifiche solo dal thread dell'interfaccia: qui le notifiche vengono spostate su quel thread.
/// </summary>
public sealed class RigaVista : INotifyPropertyChanged
{
    public RigaVista(RigaSottotitolo riga)
    {
        Riga = riga;
        riga.PropertyChanged += (_, e) => Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, e));
    }

    public RigaSottotitolo Riga { get; }

    public string RigaOriginale => Riga.RigaOriginale;
    public bool RigaOriginaleVisibile => Riga.RigaOriginaleVisibile;
    public string TestoPrincipale => Riga.TestoPrincipale;
    public bool Avviso => Riga.Avviso;

    public event PropertyChangedEventHandler? PropertyChanged;
}
