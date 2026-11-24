using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace TraduttoreLive.Traduzione;

/// <summary>
/// Chiavi dei servizi di traduzione (ed email per MyMemory), salvate nel Portachiavi di macOS, in una sola voce
/// «Traduttore dal vivo»: le legge solo questo programma, a meno che tu non dia il permesso a un altro.
/// </summary>
public static class Credenziali
{
    public const string Claude = "claude";
    public const string DeepL = "deepl";
    public const string Microsoft = "microsoft";
    public const string MyMemory = "mymemory";
    public const string Gemini = "gemini";
    public const string Groq = "groq";

    const string ServizioPortachiavi = "Traduttore dal vivo";
    const string AccountPortachiavi = "chiavi dei servizi di traduzione";
    static readonly object Blocco = new();

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
            Portachiavi.Scrivi(ServizioPortachiavi, AccountPortachiavi,
                tutte.Count == 0 ? null : JsonSerializer.SerializeToUtf8Bytes(tutte));
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
        try
        {
            if (Portachiavi.Leggi(ServizioPortachiavi, AccountPortachiavi) is { } dati)
                return JsonSerializer.Deserialize<Dictionary<string, string>>(dati) ?? [];
        }
        catch (Exception)
        {
            // Voce rovinata, o accesso al Portachiavi negato: come se non ci fosse.
        }
        return [];
    }
}

/// <summary>Una password generica nel Portachiavi dell'utente (Security.framework).</summary>
static class Portachiavi
{
    const string Security = "/System/Library/Frameworks/Security.framework/Security";
    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const int VoceNonTrovata = -25300; // errSecItemNotFound

    public static byte[]? Leggi(string servizio, string account)
    {
        byte[] s = Encoding.UTF8.GetBytes(servizio), a = Encoding.UTF8.GetBytes(account);
        int esito = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)s.Length, s, (uint)a.Length, a,
            out uint lunghezza, out IntPtr dati, out IntPtr voce);
        if (esito == VoceNonTrovata) return null;
        if (esito != 0) throw new InvalidOperationException($"Portachiavi: errore {esito}.");
        try
        {
            var valore = new byte[lunghezza];
            Marshal.Copy(dati, valore, 0, (int)lunghezza);
            return valore;
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, dati);
            if (voce != IntPtr.Zero) CFRelease(voce);
        }
    }

    /// <summary>Crea o aggiorna la voce; null la cancella.</summary>
    public static void Scrivi(string servizio, string account, byte[]? valore)
    {
        byte[] s = Encoding.UTF8.GetBytes(servizio), a = Encoding.UTF8.GetBytes(account);
        int esito = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)s.Length, s, (uint)a.Length, a,
            IntPtr.Zero, IntPtr.Zero, out IntPtr voce);
        if (esito != 0 && esito != VoceNonTrovata) throw new InvalidOperationException($"Portachiavi: errore {esito}.");
        try
        {
            if (voce != IntPtr.Zero)
                esito = valore is null
                    ? SecKeychainItemDelete(voce)
                    : SecKeychainItemModifyAttributesAndData(voce, IntPtr.Zero, (uint)valore.Length, valore);
            else if (valore is not null)
                esito = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)s.Length, s, (uint)a.Length, a,
                    (uint)valore.Length, valore, IntPtr.Zero);
            else
                esito = 0;
        }
        finally
        {
            if (voce != IntPtr.Zero) CFRelease(voce);
        }
        if (esito != 0) throw new InvalidOperationException($"Portachiavi: errore {esito}.");
    }

    [DllImport(Security)]
    static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

    [DllImport(Security, EntryPoint = "SecKeychainFindGenericPassword")]
    static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, IntPtr passwordLength, IntPtr passwordData, out IntPtr itemRef);

    [DllImport(Security)]
    static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, uint passwordLength, byte[] passwordData, IntPtr itemRef);

    [DllImport(Security)]
    static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

    [DllImport(Security)]
    static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport(Security)]
    static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport(CoreFoundation)]
    static extern void CFRelease(IntPtr cf);
}
