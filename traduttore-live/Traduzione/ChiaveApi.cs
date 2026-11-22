using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// La chiave API di Claude inserita nel programma. È salvata nella cartella delle impostazioni, cifrata con
/// l'account di Windows (DPAPI): solo lo stesso utente sullo stesso PC può leggerla.
/// Il programma la usa al posto di ANTHROPIC_API_KEY, che resta valida per chi preferisce la variabile
/// d'ambiente: impostarla per tutto l'utente però la fa usare anche ad altri programmi, come Claude Code.
/// </summary>
public static class ChiaveApi
{
    static readonly byte[] Entropia = "TraduttoreLive.ChiaveApi.v1"u8.ToArray();

    static string Percorso => Path.Combine(Impostazioni.Cartella, "chiave-api.dat");

    public static bool Salvata => File.Exists(Percorso);

    /// <summary>La chiave salvata, o null se non c'è o non si riesce a leggerla.</summary>
    public static string? Leggi()
    {
        try
        {
            if (!File.Exists(Percorso)) return null;
            byte[] chiaro = ProtectedData.Unprotect(File.ReadAllBytes(Percorso), Entropia, DataProtectionScope.CurrentUser);
            string chiave = Encoding.UTF8.GetString(chiaro).Trim();
            return chiave.Length > 0 ? chiave : null;
        }
        catch (Exception)
        {
            // File rovinato o copiato da un altro utente o PC: come se non ci fosse.
            return null;
        }
    }

    public static void Salva(string chiave)
    {
        Directory.CreateDirectory(Impostazioni.Cartella);
        byte[] cifrato = ProtectedData.Protect(Encoding.UTF8.GetBytes(chiave.Trim()), Entropia, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Percorso, cifrato);
    }

    public static void Rimuovi()
    {
        try { File.Delete(Percorso); } catch (Exception) { }
    }

    /// <summary>C'è un modo di autenticarsi con Claude: chiave salvata, variabile d'ambiente o profilo di `ant auth login`.</summary>
    public static bool CredenzialiPresenti()
    {
        if (Leggi() is not null) return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))) return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN"))) return true;
        string profili = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "anthropic");
        return Directory.Exists(profili) && Directory.EnumerateFiles(profili, "*", SearchOption.AllDirectories).Any();
    }
}
