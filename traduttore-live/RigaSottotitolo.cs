using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TraduttoreLive;

/// <summary>Una frase mostrata nella finestra: il testo originale e, quando arriva, la traduzione.</summary>
public sealed class RigaSottotitolo : INotifyPropertyChanged
{
    string? _traduzione;
    bool _inTraduzione;
    bool _mostraOriginale = true;

    public RigaSottotitolo(DateTime ora, string lingua, string originale, bool daTradurre)
    {
        Ora = ora;
        Lingua = lingua;
        Originale = originale;
        _inTraduzione = daTradurre;
    }

    public DateTime Ora { get; }
    public string Lingua { get; }
    public string Originale { get; }

    /// <summary>Riga di sistema (avvisi), mostrata in un altro colore.</summary>
    public bool Avviso { get; init; }

    public string? Traduzione
    {
        get => _traduzione;
        set { _traduzione = value; Notifica(); NotificaTesti(); }
    }

    public bool InTraduzione
    {
        get => _inTraduzione;
        set { _inTraduzione = value; Notifica(); NotificaTesti(); }
    }

    public bool MostraOriginale
    {
        get => _mostraOriginale;
        set { _mostraOriginale = value; Notifica(); NotificaTesti(); }
    }

    bool HaTraduzione => !string.IsNullOrEmpty(_traduzione) || _inTraduzione;

    /// <summary>Riga piccola in alto: il testo originale con la lingua, solo se c'è (o arriva) una traduzione.</summary>
    public string RigaOriginale => $"{(string.IsNullOrEmpty(Lingua) ? "" : Lingua.ToUpperInvariant() + "  ")}{Originale}";

    public bool RigaOriginaleVisibile => HaTraduzione && _mostraOriginale;

    /// <summary>Riga grande: la traduzione; se non serve tradurre, il testo originale.</summary>
    public string TestoPrincipale =>
        !string.IsNullOrEmpty(_traduzione) ? _traduzione
        : _inTraduzione ? (_mostraOriginale ? "…" : Originale + " …")
        : Originale;

    public event PropertyChangedEventHandler? PropertyChanged;

    void NotificaTesti()
    {
        Notifica(nameof(TestoPrincipale));
        Notifica(nameof(RigaOriginaleVisibile));
    }

    void Notifica([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}
