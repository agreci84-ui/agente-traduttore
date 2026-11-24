using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace TraduttoreLive;

/// <summary>Regolazioni della finestra fatte direttamente con AppKit.</summary>
static class FinestraMac
{
    const nuint SuTuttiGliSpazi = 1 << 0;        // NSWindowCollectionBehaviorCanJoinAllSpaces
    const nuint SopraLoSchermoIntero = 1 << 8;   // NSWindowCollectionBehaviorFullScreenAuxiliary

    /// <summary>
    /// La finestra dei sottotitoli resta visibile su tutte le scrivanie (Spaces) e anche sopra le app a tutto
    /// schermo, come le riunioni di Teams o Zoom ingrandite.
    /// </summary>
    public static void MostraOvunque(Window finestra)
    {
        try
        {
            var handle = finestra.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero) return;
            IntPtr nsWindow = handle.HandleDescriptor switch
            {
                "NSWindow" => handle.Handle,
                "NSView" => Messaggio(handle.Handle, Selettore("window")),
                _ => IntPtr.Zero,
            };
            if (nsWindow == IntPtr.Zero) return;
            nuint attuale = LeggiNUInt(nsWindow, Selettore("collectionBehavior"));
            ScriviNUInt(nsWindow, Selettore("setCollectionBehavior:"), attuale | SuTuttiGliSpazi | SopraLoSchermoIntero);
        }
        catch (Exception)
        {
            // Solo una comodità: senza, la finestra funziona lo stesso.
        }
    }

    const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    static extern IntPtr Selettore(string nome);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    static extern IntPtr Messaggio(IntPtr oggetto, IntPtr selettore);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    static extern nuint LeggiNUInt(IntPtr oggetto, IntPtr selettore);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    static extern void ScriviNUInt(IntPtr oggetto, IntPtr selettore, nuint valore);
}
