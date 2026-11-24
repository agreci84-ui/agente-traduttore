using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace TraduttoreLive.Audio;

/// <summary>Un'app che sta usando l'audio (es. Microsoft Teams, zoom.us, Google Chrome).</summary>
public sealed record ProgrammaAudio(string NomeProcesso, string NomeVisibile);

/// <summary>
/// Trova le app collegate all'audio e il processo principale di un'app. Sul Mac un'app è il suo pacchetto .app:
/// i processi secondari (le «Helper» dei browser e di Teams, che riproducono l'audio) stanno al suo interno, e
/// cattura-audio li ascolta tutti. Il nome dell'app è quello del pacchetto, senza «.app».
/// </summary>
public static class Programmi
{
    // Servizi di sistema collegati all'audio (Centro di Controllo, Siri...): non sono app da ascoltare.
    static readonly string[] CartelleDiSistema = ["/System/Library/", "/Library/Apple/", "/usr/"];

    /// <summary>Elenca le app collegate all'audio in questo momento.</summary>
    public static List<ProgrammaAudio> ConAudioAperto()
    {
        var risultato = new Dictionary<string, ProgrammaAudio>(StringComparer.OrdinalIgnoreCase);
        string? questaApp = AppDelPercorso(Environment.ProcessPath ?? "");
        foreach (string percorso in PercorsiProcessiAudio())
        {
            string? app = AppDelPercorso(percorso);
            if (app is null || app == questaApp || CartelleDiSistema.Any(app.StartsWith)) continue;
            string nome = NomeApp(app);
            risultato.TryAdd(nome, new ProgrammaAudio(nome, nome));
        }
        return risultato.Values.OrderBy(p => p.NomeVisibile, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Il processo principale dell'app indicata (es. "Microsoft Teams"), o null se non è aperta.</summary>
    public static int? TrovaRadice(string nomeApp)
    {
        var candidati = new List<(int Pid, bool Principale)>();
        foreach (int pid in TuttiIProcessi())
        {
            if (PercorsoProcesso(pid) is not { } percorso || AppDelPercorso(percorso) is not { } app) continue;
            if (!string.Equals(NomeApp(app), nomeApp, StringComparison.OrdinalIgnoreCase)) continue;
            candidati.Add((pid, percorso.StartsWith(app + "/Contents/MacOS/", StringComparison.Ordinal)));
        }
        if (candidati.Count == 0) return null;
        var principali = candidati.Where(c => c.Principale).ToList();
        return (principali.Count > 0 ? principali : candidati).Min(c => c.Pid);
    }

    /// <summary>Il pacchetto .app che contiene il processo, o null.</summary>
    public static string? PercorsoApp(int pid) => PercorsoProcesso(pid) is { } p ? AppDelPercorso(p) : null;

    /// <summary>Il pacchetto .app più esterno che contiene il file: «/Applications/Microsoft Teams.app».</summary>
    static string? AppDelPercorso(string percorso)
    {
        int i = percorso.IndexOf(".app/", StringComparison.Ordinal);
        return i < 0 ? null : percorso[..(i + 4)];
    }

    static string NomeApp(string app) => Path.GetFileNameWithoutExtension(app);

    static List<string> PercorsiProcessiAudio()
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "cattura-audio"), "elenco")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        try
        {
            using var processo = Process.Start(info);
            if (processo is null) return [];
            string json = processo.StandardOutput.ReadToEnd();
            processo.WaitForExit(3000);
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.EnumerateArray()
                .Select(p => p.TryGetProperty("percorso", out var v) ? v.GetString() : null)
                .OfType<string>()
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    // ---- Processi (libproc) ----

    const string LibProc = "/usr/lib/libproc.dylib";

    [DllImport(LibProc)]
    static extern int proc_listallpids(int[] buffer, int dimensioneInByte);

    [DllImport(LibProc)]
    static extern int proc_pidpath(int pid, byte[] buffer, uint dimensione);

    static int[] TuttiIProcessi()
    {
        var pid = new int[16384];
        int n = proc_listallpids(pid, pid.Length * sizeof(int));
        return n > 0 ? pid[..Math.Min(n, pid.Length)] : [];
    }

    static string? PercorsoProcesso(int pid)
    {
        var buffer = new byte[4096];
        int n = proc_pidpath(pid, buffer, (uint)buffer.Length);
        return n > 0 ? Encoding.UTF8.GetString(buffer, 0, n) : null;
    }
}
