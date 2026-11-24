using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Chiavi dei servizi di traduzione (ed email per MyMemory), salvate nella cartella delle impostazioni e cifrate
/// con l'account di Windows (DPAPI): solo lo stesso utente sullo stesso PC può leggerle.
/// </summary>
public static class Credenziali
{
    public const string Claude = "claude";
    public const string DeepL = "deepl";
    public const string Microsoft = "microsoft";
    public const string MyMemory = "mymemory";
    public const string Gemini = "gemini";
    public const string Groq = "groq";

    static readonly byte[] Entropia = "TraduttoreLive.Credenziali.v1"u8.ToArray();
    static readonly object Blocco = new();

    static string Percorso => Path.Combine(Impostazioni.Cartella, "credenziali.dat");

    // Versione 1.0: c'era solo la chiave di Claude, in un file a parte.
    static string PercorsoVecchio => Path.Combine(Impostazioni.Cartella, "chiave-api.dat");
    static readonly byte[] EntropiaVecchia = "TraduttoreLive.ChiaveApi.v1"u8.ToArray();

    /// <summary>Il valore salvato per un servizio, o null.</summary>
    public static string? Leggi(string servizio)
    {
        lock (Blocco)
            return CaricaTutte().TryGetValue(servizio, out var valore) && !string.IsNullOrWhiteSpace(valore) ? valore : null;
    }

    /// <summary>Salva il valore di un servizio; vuoto o null lo toglie.</summary>
    public static void Salva(string servizio, string? valore)
    {
        lock (Blocco)
        {
            var tutte = CaricaTutte();
            if (string.IsNullOrWhiteSpace(valore)) tutte.Remove(servizio);
            else tutte[servizio] = valore.Trim();
            Directory.CreateDirectory(Impostazioni.Cartella);
            byte[] chiaro = JsonSerializer.SerializeToUtf8Bytes(tutte);
            File.WriteAllBytes(Percorso, ProtectedData.Protect(chiaro, Entropia, DataProtectionScope.CurrentUser));
            try { File.Delete(PercorsoVecchio); } catch (Exception) { }
        }
    }

    /// <summary>C'è un modo di autenticarsi con Claude: chiave salvata, variabile d'ambiente o profilo di `ant auth login`.</summary>
    public static bool ClaudeDisponibile()
    {
        if (Leggi(Claude) is not null) return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))) return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN"))) return true;
        string profili = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "anthropic");
        return Directory.Exists(profili) && Directory.EnumerateFiles(profili, "*", SearchOption.AllDirectories).Any();
    }

    static Dictionary<string, string> CaricaTutte()
    {
        var tutte = new Dictionary<string, string>();
        try
        {
            if (File.Exists(Percorso))
            {
                byte[] chiaro = ProtectedData.Unprotect(File.ReadAllBytes(Percorso), Entropia, DataProtectionScope.CurrentUser);
                tutte = JsonSerializer.Deserialize<Dictionary<string, string>>(chiaro) ?? tutte;
            }
        }
        catch (Exception)
        {
            // File rovinato o copiato da un altro utente o PC: come se non ci fosse.
        }
        if (!tutte.ContainsKey(Claude))
        {
            try
            {
                if (File.Exists(PercorsoVecchio))
                {
                    byte[] chiaro = ProtectedData.Unprotect(File.ReadAllBytes(PercorsoVecchio), EntropiaVecchia, DataProtectionScope.CurrentUser);
                    string chiave = Encoding.UTF8.GetString(chiaro).Trim();
                    if (chiave.Length > 0) tutte[Claude] = chiave;
                }
            }
            catch (Exception)
            {
            }
        }
        return tutte;
    }
}
