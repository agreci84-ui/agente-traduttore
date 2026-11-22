using System.IO;
using System.Text;

namespace TraduttoreLive;

/// <summary>
/// Salva la trascrizione e la traduzione della riunione in Documenti\Traduttore dal vivo,
/// un file per ogni sessione. Il file si crea alla prima frase.
/// </summary>
public sealed class RegistroRiunione : IDisposable
{
    public static string Cartella { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Traduttore dal vivo");

    readonly DateTime _inizio = DateTime.Now;
    readonly object _lock = new();
    StreamWriter? _file;

    public string? Percorso { get; private set; }

    public void Scrivi(DateTime ora, string lingua, string originale, string? traduzione, string linguaArrivo)
    {
        lock (_lock)
        {
            try
            {
                if (_file is null)
                {
                    Directory.CreateDirectory(Cartella);
                    Percorso = Path.Combine(Cartella, $"Riunione {_inizio:yyyy-MM-dd HH.mm.ss}.txt");
                    _file = new StreamWriter(Percorso, append: true, new UTF8Encoding(true)) { AutoFlush = true };
                    _file.WriteLine($"Riunione del {_inizio:dddd d MMMM yyyy, HH:mm}");
                    _file.WriteLine();
                }
                string etichetta = string.IsNullOrEmpty(lingua) ? "??" : lingua.ToUpperInvariant();
                _file.WriteLine($"[{ora:HH:mm:ss}] {etichetta}: {originale}");
                if (!string.IsNullOrEmpty(traduzione) && traduzione != originale)
                    _file.WriteLine($"           {linguaArrivo.ToUpperInvariant()}: {traduzione}");
            }
            catch (Exception)
            {
                // Se il file non si può scrivere (disco pieno, cartella protetta) i sottotitoli continuano.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _file?.Dispose();
            _file = null;
        }
    }
}
