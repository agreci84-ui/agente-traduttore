// cattura-audio: ascolta l'audio in uscita dal Mac con i «process tap» di Core Audio (macOS 14.2 o successivo)
// e lo scrive sullo standard output come campioni float 32 bit, mono, 16 kHz (il formato di Whisper).
//
//   cattura-audio elenco                        programmi collegati all'audio, in JSON
//   cattura-audio ascolta                       tutto l'audio del Mac
//   cattura-audio ascolta --programma <app>     solo l'audio di un'app (percorso del pacchetto .app)
//
// Messaggi sullo standard error, una riga ciascuno: «PRONTO <descrizione>» quando l'ascolto è partito,
// «ERRORE <testo>» prima di uscire per un errore. Codici di uscita: 0 fine normale (standard input chiuso),
// 1 errore, 2 è cambiata l'uscita audio predefinita (chi lo ha avviato lo riavvia).
// Non usa il microfono: sente quello che esce dalle casse o dalle cuffie.

import AVFoundation
import CoreAudio
import Darwin
import Foundation

let frequenzaUscita = 16_000.0

// ---- Utilità ----

func messaggio(_ testo: String) {
    FileHandle.standardError.write((testo.replacingOccurrences(of: "\n", with: " ") + "\n").data(using: .utf8)!)
}

func esci(_ testo: String, codice: Int32 = 1) -> Never {
    messaggio("ERRORE " + testo)
    exit(codice)
}

func indirizzo(_ selettore: AudioObjectPropertySelector) -> AudioObjectPropertyAddress {
    AudioObjectPropertyAddress(mSelector: selettore, mScope: kAudioObjectPropertyScopeGlobal,
                               mElement: kAudioObjectPropertyElementMain)
}

func leggiValore<T>(_ oggetto: AudioObjectID, _ selettore: AudioObjectPropertySelector, _ iniziale: T) -> T? {
    var ind = indirizzo(selettore)
    var valore = iniziale
    var dimensione = UInt32(MemoryLayout<T>.size)
    let esito = withUnsafeMutablePointer(to: &valore) {
        AudioObjectGetPropertyData(oggetto, &ind, 0, nil, &dimensione, $0)
    }
    return esito == noErr ? valore : nil
}

func leggiStringa(_ oggetto: AudioObjectID, _ selettore: AudioObjectPropertySelector) -> String? {
    var ind = indirizzo(selettore)
    var valore: Unmanaged<CFString>?
    var dimensione = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
    let esito = withUnsafeMutablePointer(to: &valore) {
        AudioObjectGetPropertyData(oggetto, &ind, 0, nil, &dimensione, $0)
    }
    guard esito == noErr, let stringa = valore?.takeRetainedValue() else { return nil }
    return stringa as String
}

func leggiOggetti(_ oggetto: AudioObjectID, _ selettore: AudioObjectPropertySelector) -> [AudioObjectID] {
    var ind = indirizzo(selettore)
    var dimensione: UInt32 = 0
    guard AudioObjectGetPropertyDataSize(oggetto, &ind, 0, nil, &dimensione) == noErr, dimensione > 0 else { return [] }
    var oggetti = [AudioObjectID](repeating: 0, count: Int(dimensione) / MemoryLayout<AudioObjectID>.size)
    guard AudioObjectGetPropertyData(oggetto, &ind, 0, nil, &dimensione, &oggetti) == noErr else { return [] }
    return oggetti
}

func testoErrore(_ codice: OSStatus) -> String {
    let byte = [24, 16, 8, 0].map { UInt8((UInt32(bitPattern: codice) >> $0) & 0xFF) }
    if byte.allSatisfy({ $0 >= 32 && $0 < 127 }) { return "'\(String(decoding: byte, as: UTF8.self))' (\(codice))" }
    return "\(codice)"
}

@_silgen_name("proc_pidpath")
func proc_pidpath(_ pid: Int32, _ buffer: UnsafeMutableRawPointer, _ dimensione: UInt32) -> Int32

func percorsoProcesso(_ pid: pid_t) -> String? {
    var buffer = [UInt8](repeating: 0, count: 4 * Int(MAXPATHLEN))
    let n = proc_pidpath(pid, &buffer, UInt32(buffer.count))
    return n > 0 ? String(decoding: buffer[0..<Int(n)], as: UTF8.self) : nil
}

// ---- Processi collegati all'audio ----

struct ProcessoAudio {
    let oggetto: AudioObjectID
    let pid: pid_t
    let percorso: String
    let bundle: String
    let uscita: Bool
}

func processiAudio() -> [ProcessoAudio] {
    leggiOggetti(AudioObjectID(kAudioObjectSystemObject), kAudioHardwarePropertyProcessObjectList).compactMap { oggetto in
        guard let pid = leggiValore(oggetto, kAudioProcessPropertyPID, pid_t(0)), pid > 0,
              let percorso = percorsoProcesso(pid) else { return nil }
        let uscita = (leggiValore(oggetto, kAudioProcessPropertyIsRunningOutput, UInt32(0)) ?? 0) != 0
        return ProcessoAudio(oggetto: oggetto, pid: pid, percorso: percorso,
                             bundle: leggiStringa(oggetto, kAudioProcessPropertyBundleID) ?? "", uscita: uscita)
    }
}

/// I processi audio che fanno parte di un'app: i suoi processi secondari (le «Helper» dei browser, di Teams...)
/// stanno dentro il pacchetto .app.
func processiDelProgramma(_ programma: String) -> [AudioObjectID] {
    processiAudio()
        .filter { $0.percorso == programma || $0.percorso.hasPrefix(programma + "/") }
        .map(\.oggetto)
        .sorted()
}

func elenco() -> Never {
    let voci: [[String: Any]] = processiAudio().map {
        ["pid": Int($0.pid), "percorso": $0.percorso, "bundle": $0.bundle, "uscita": $0.uscita]
    }
    let dati = try! JSONSerialization.data(withJSONObject: voci, options: [])
    FileHandle.standardOutput.write(dati)
    FileHandle.standardOutput.write("\n".data(using: .utf8)!)
    exit(0)
}

// ---- Conversione in mono a 16 kHz ----

final class Convertitore {
    let canali: Int
    let interlacciato: Bool
    let frequenza: Double
    let convertitore: AVAudioConverter?
    let formatoMono: AVAudioFormat
    let formatoUscita: AVAudioFormat

    init(_ formato: AudioStreamBasicDescription) {
        guard formato.mFormatID == kAudioFormatLinearPCM,
              formato.mFormatFlags & kAudioFormatFlagIsFloat != 0,
              formato.mBitsPerChannel == 32 else {
            esci("formato audio non supportato (\(formato.mFormatID), \(formato.mBitsPerChannel) bit)")
        }
        canali = max(1, Int(formato.mChannelsPerFrame))
        interlacciato = formato.mFormatFlags & kAudioFormatFlagIsNonInterleaved == 0
        frequenza = formato.mSampleRate
        formatoMono = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: frequenza, channels: 1, interleaved: false)!
        formatoUscita = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: frequenzaUscita, channels: 1, interleaved: false)!
        convertitore = frequenza == frequenzaUscita ? nil : AVAudioConverter(from: formatoMono, to: formatoUscita)
        if frequenza != frequenzaUscita && convertitore == nil { esci("non riesco a convertire l'audio da \(frequenza) Hz") }
    }

    /// Media dei canali del tap. Il tap è l'ultimo flusso del dispositivo aggregato: se l'uscita predefinita ha
    /// anche un ingresso (una cuffia USB col microfono), i suoi flussi vengono prima e si ignorano.
    func mono(_ lista: UnsafePointer<AudioBufferList>) -> [Float] {
        let buffer = UnsafeMutableAudioBufferListPointer(UnsafeMutablePointer(mutating: lista))
        if buffer.count == 0 { return [] }
        if interlacciato {
            let b = buffer[buffer.count - 1]
            let n = max(1, Int(b.mNumberChannels))
            guard let dati = b.mData?.assumingMemoryBound(to: Float.self) else { return [] }
            let frame = Int(b.mDataByteSize) / (4 * n)
            var uscita = [Float](repeating: 0, count: frame)
            for f in 0..<frame {
                var somma: Float = 0
                for c in 0..<n { somma += dati[f * n + c] }
                uscita[f] = somma / Float(n)
            }
            return uscita
        }
        let ultimi = (max(0, buffer.count - canali)..<buffer.count).map { buffer[$0] }
        let frame = ultimi.map { Int($0.mDataByteSize) / 4 }.min() ?? 0
        var uscita = [Float](repeating: 0, count: frame)
        for b in ultimi {
            guard let dati = b.mData?.assumingMemoryBound(to: Float.self) else { continue }
            for f in 0..<frame { uscita[f] += dati[f] }
        }
        let fattore = 1 / Float(ultimi.count)
        for f in 0..<frame { uscita[f] *= fattore }
        return uscita
    }

    func converti(_ mono: [Float]) -> [Float] {
        guard let convertitore, !mono.isEmpty else { return mono }
        let ingresso = AVAudioPCMBuffer(pcmFormat: formatoMono, frameCapacity: AVAudioFrameCount(mono.count))!
        ingresso.frameLength = AVAudioFrameCount(mono.count)
        mono.withUnsafeBufferPointer { ingresso.floatChannelData![0].update(from: $0.baseAddress!, count: mono.count) }
        let capacita = AVAudioFrameCount(Double(mono.count) * frequenzaUscita / frequenza) + 64
        let uscita = AVAudioPCMBuffer(pcmFormat: formatoUscita, frameCapacity: capacita)!
        var fornito = false
        var errore: NSError?
        convertitore.convert(to: uscita, error: &errore) { _, stato in
            if fornito {
                stato.pointee = .noDataNow
                return nil
            }
            fornito = true
            stato.pointee = .haveData
            return ingresso
        }
        if errore != nil { return [] }
        return Array(UnsafeBufferPointer(start: uscita.floatChannelData![0], count: Int(uscita.frameLength)))
    }
}

func scrivi(_ campioni: [Float]) {
    campioni.withUnsafeBytes { byte in
        var inviati = 0
        while inviati < byte.count {
            let n = write(1, byte.baseAddress! + inviati, byte.count - inviati)
            if n <= 0 {
                if errno == EINTR { continue }
                exit(0) // chi legge ha chiuso: si esce in silenzio
            }
            inviati += n
        }
    }
}

// ---- Ascolto ----

final class Ascolto {
    let programma: String?
    /// Comandi e notifiche di Core Audio, uno alla volta.
    let coda = DispatchQueue(label: "cattura-audio.comandi")
    /// Il ciclo dell'audio: separato, così fermare l'ascolto non deve aspettare sé stesso.
    let codaAudio = DispatchQueue(label: "cattura-audio.audio", qos: .userInteractive)
    var descrizione: CATapDescription!
    var tap = AudioObjectID(kAudioObjectUnknown)
    var aggregato = AudioObjectID(kAudioObjectUnknown)
    var procedura: AudioDeviceIOProcID?
    var processi: [AudioObjectID] = []

    init(programma: String?) {
        self.programma = programma
    }

    var uidUscita = ""

    /// Prepara l'ascolto. Va chiamato sulla coda dell'ascolto, dove arrivano anche le notifiche di Core Audio.
    func avvia() {
        let sistema = AudioObjectID(kAudioObjectSystemObject)
        guard let uscita = leggiValore(sistema, kAudioHardwarePropertyDefaultOutputDevice, AudioDeviceID(0)), uscita != 0,
              let uid = leggiStringa(uscita, kAudioDevicePropertyDeviceUID) else {
            esci("non trovo l'uscita audio predefinita")
        }
        uidUscita = uid

        // Cambia l'uscita predefinita (cuffie collegate, AirPods...): il dispositivo aggregato non la segue,
        // quindi si esce e chi ha avviato l'ascolto lo riavvia sulla nuova uscita.
        var indUscita = indirizzo(kAudioHardwarePropertyDefaultOutputDevice)
        AudioObjectAddPropertyListenerBlock(sistema, &indUscita, coda) { _, _ in
            messaggio("ERRORE è cambiata l'uscita audio")
            self.ferma()
            exit(2)
        }

        // Solo un'app: quando apre o chiude processi audio (una chiamata che inizia, una scheda del browser),
        // si aggiorna l'elenco dei processi ascoltati.
        if let programma {
            processi = processiDelProgramma(programma)
            var indProcessi = indirizzo(kAudioHardwarePropertyProcessObjectList)
            AudioObjectAddPropertyListenerBlock(sistema, &indProcessi, coda) { _, _ in
                self.processiCambiati(programma)
            }
            if processi.isEmpty {
                // L'app è aperta ma non usa ancora l'audio: si comincia ad ascoltare appena lo fa.
                messaggio("PRONTO in attesa che l'app usi l'audio")
                return
            }
        }

        crea()
        messaggio("PRONTO \(leggiStringa(uscita, kAudioObjectPropertyName) ?? "uscita predefinita")")
    }

    func processiCambiati(_ programma: String) {
        let nuovi = processiDelProgramma(programma)
        if nuovi == processi { return }
        processi = nuovi
        if tap == kAudioObjectUnknown {
            if !nuovi.isEmpty { crea() }
            return
        }
        descrizione.processes = nuovi
        var indDescrizione = indirizzo(kAudioTapPropertyDescription)
        var nuova: CATapDescription = descrizione
        let esito = withUnsafeMutablePointer(to: &nuova) {
            AudioObjectSetPropertyData(tap, &indDescrizione, 0, nil, UInt32(MemoryLayout<CATapDescription>.size), $0)
        }
        if esito != noErr {
            messaggio("ERRORE non riesco ad aggiornare i processi ascoltati: \(testoErrore(esito))")
            ferma()
            exit(2)
        }
    }

    /// Crea il tap sui processi da ascoltare, il dispositivo aggregato che lo contiene e avvia la ricezione.
    func crea() {
        descrizione = programma == nil
            ? CATapDescription(stereoGlobalTapButExcludeProcesses: [])
            : CATapDescription(stereoMixdownOfProcesses: processi)
        descrizione.uuid = UUID()
        descrizione.name = "Traduttore dal vivo"
        descrizione.isPrivate = true
        descrizione.muteBehavior = .unmuted

        var esito = AudioHardwareCreateProcessTap(descrizione, &tap)
        guard esito == noErr else { esci("non riesco a creare l'ascolto dell'audio: \(testoErrore(esito))") }

        guard let formato = leggiValore(tap, kAudioTapPropertyFormat, AudioStreamBasicDescription()) else {
            esci("non riesco a leggere il formato dell'audio")
        }
        let convertitore = Convertitore(formato)

        let impostazioni: [String: Any] = [
            kAudioAggregateDeviceNameKey: "Traduttore dal vivo",
            kAudioAggregateDeviceUIDKey: UUID().uuidString,
            kAudioAggregateDeviceMainSubDeviceKey: uidUscita,
            kAudioAggregateDeviceIsPrivateKey: true,
            kAudioAggregateDeviceIsStackedKey: false,
            kAudioAggregateDeviceTapAutoStartKey: true,
            kAudioAggregateDeviceSubDeviceListKey: [[kAudioSubDeviceUIDKey: uidUscita]],
            kAudioAggregateDeviceTapListKey: [[
                kAudioSubTapDriftCompensationKey: true,
                kAudioSubTapUIDKey: descrizione.uuid.uuidString,
            ]],
        ]
        esito = AudioHardwareCreateAggregateDevice(impostazioni as CFDictionary, &aggregato)
        guard esito == noErr else { esci("non riesco a creare il dispositivo di ascolto: \(testoErrore(esito))") }

        esito = AudioDeviceCreateIOProcIDWithBlock(&procedura, aggregato, codaAudio) { _, ingresso, _, _, _ in
            scrivi(convertitore.converti(convertitore.mono(ingresso)))
        }
        guard esito == noErr else { esci("non riesco a ricevere l'audio: \(testoErrore(esito))") }
        esito = AudioDeviceStart(aggregato, procedura)
        guard esito == noErr else { esci("non riesco ad avviare l'ascolto: \(testoErrore(esito))") }
    }

    func ferma() {
        if aggregato != kAudioObjectUnknown {
            AudioDeviceStop(aggregato, procedura)
            if let procedura { AudioDeviceDestroyIOProcID(aggregato, procedura) }
            AudioHardwareDestroyAggregateDevice(aggregato)
            aggregato = AudioObjectID(kAudioObjectUnknown)
        }
        if tap != kAudioObjectUnknown {
            AudioHardwareDestroyProcessTap(tap)
            tap = AudioObjectID(kAudioObjectUnknown)
        }
    }
}

// ---- Avvio ----

setvbuf(stderr, nil, _IONBF, 0)
signal(SIGPIPE, SIG_IGN)

let argomenti = Array(CommandLine.arguments.dropFirst())
guard #available(macOS 14.2, *) else { esci("serve macOS 14.2 o successivo") }

switch argomenti.first {
case "elenco":
    elenco()
case "ascolta":
    var programma: String?
    if let i = argomenti.firstIndex(of: "--programma"), i + 1 < argomenti.count {
        programma = argomenti[i + 1]
    }
    let ascolto = Ascolto(programma: programma)
    ascolto.coda.sync { ascolto.avvia() }

    // Fine: quando chi ci ha avviato chiude lo standard input (o termina), o con SIGTERM / SIGINT.
    let fine = {
        ascolto.coda.sync { ascolto.ferma() }
        exit(0)
    }
    Thread.detachNewThread {
        var buffer = [UInt8](repeating: 0, count: 256)
        while read(0, &buffer, buffer.count) > 0 {}
        DispatchQueue.main.async(execute: fine)
    }
    var segnali: [DispatchSourceSignal] = []
    for numero in [SIGTERM, SIGINT] {
        signal(numero, SIG_IGN)
        let sorgente = DispatchSource.makeSignalSource(signal: numero, queue: .main)
        sorgente.setEventHandler(handler: fine)
        sorgente.resume()
        segnali.append(sorgente)
    }
    dispatchMain()
default:
    esci("uso: cattura-audio elenco | ascolta [--programma <percorso dell'app>]")
}
