using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace TraduttoreLive.Audio;

/// <summary>Un programma che sta usando l'audio (es. Teams, Zoom, Chrome).</summary>
public sealed record ProgrammaAudio(string NomeProcesso, string NomeVisibile);

/// <summary>
/// Trova i programmi che hanno l'audio aperto e il processo «radice» di un programma, cioè quello da
/// cui partono gli altri: catturando la radice si prende anche l'audio dei suoi processi figli
/// (Teams e i browser riproducono l'audio in processi secondari).
/// </summary>
public static class Programmi
{
    // Processi di sistema oltre i quali non si risale: sono i "genitori" dei programmi avviati dall'utente.
    static readonly HashSet<string> Contenitori = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "svchost", "services", "wininit", "winlogon", "sihost", "RuntimeBroker",
        "ApplicationFrameHost", "cmd", "powershell", "pwsh", "WindowsTerminal", "conhost", "System", "Idle",
    };

    /// <summary>Elenca i programmi con una sessione audio aperta su qualsiasi dispositivo di uscita.</summary>
    public static List<ProgrammaAudio> ConAudioAperto()
    {
        var albero = Istantanea();
        var risultato = new Dictionary<string, ProgrammaAudio>(StringComparer.OrdinalIgnoreCase);
        int mioPid = Environment.ProcessId;

        using var enumeratore = new MMDeviceEnumerator();
        foreach (var dispositivo in enumeratore.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                var sessioni = dispositivo.AudioSessionManager.Sessions;
                for (int i = 0; i < sessioni.Count; i++)
                {
                    var s = sessioni[i];
                    if (s.IsSystemSoundsSession) continue;
                    int pid = (int)s.GetProcessID;
                    if (pid == 0 || pid == mioPid) continue;

                    int radice = Radice(pid, albero);
                    if (!albero.TryGetValue(radice, out var info)) continue;
                    if (risultato.ContainsKey(info.Nome)) continue;
                    risultato[info.Nome] = new ProgrammaAudio(info.Nome, NomeLeggibile(radice, info.Nome));
                }
            }
            catch (Exception)
            {
                // Alcuni dispositivi non espongono le sessioni: si saltano.
            }
            finally
            {
                dispositivo.Dispose();
            }
        }
        return risultato.Values.OrderBy(p => p.NomeVisibile, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Restituisce il processo radice del programma indicato (es. "ms-teams"), o null se non è in esecuzione.
    /// </summary>
    public static int? TrovaRadice(string nomeProcesso)
    {
        var albero = Istantanea();
        var candidati = albero
            .Where(p => string.Equals(p.Value.Nome, nomeProcesso, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Key)
            .ToList();
        if (candidati.Count == 0) return null;

        // Tra i processi con quel nome, la radice è quello il cui genitore ha un nome diverso.
        var radici = candidati
            .Where(pid => !albero.TryGetValue(albero[pid].Genitore, out var g)
                          || !string.Equals(g.Nome, nomeProcesso, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return (radici.Count > 0 ? radici : candidati).Min();
    }

    static int Radice(int pid, Dictionary<int, (int Genitore, string Nome)> albero)
    {
        int attuale = pid;
        for (int passi = 0; passi < 8; passi++)
        {
            if (!albero.TryGetValue(attuale, out var info)) break;
            if (!albero.TryGetValue(info.Genitore, out var genitore)) break;
            if (Contenitori.Contains(genitore.Nome)) break;
            attuale = info.Genitore;
        }
        return attuale;
    }

    static string NomeLeggibile(int pid, string nomeProcesso)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            string? descrizione = p.MainModule?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(descrizione)) return $"{descrizione.Trim()} ({nomeProcesso})";
        }
        catch (Exception)
        {
            // Processi protetti o già chiusi: basta il nome del processo.
        }
        return nomeProcesso;
    }

    // ---- Elenco dei processi con il loro genitore (Toolhelp32) ----

    static Dictionary<int, (int Genitore, string Nome)> Istantanea()
    {
        var mappa = new Dictionary<int, (int, string)>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return mappa;
        try
        {
            var voce = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snap, ref voce)) return mappa;
            do
            {
                string nome = voce.szExeFile;
                if (nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) nome = nome[..^4];
                mappa[(int)voce.th32ProcessID] = ((int)voce.th32ParentProcessID, nome);
            } while (Process32NextW(snap, ref voce));
        }
        finally
        {
            CloseHandle(snap);
        }
        return mappa;
    }

    const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
}
