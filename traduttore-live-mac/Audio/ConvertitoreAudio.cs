namespace TraduttoreLive.Audio;

/// <summary>
/// Il formato dell'audio per Whisper: float mono a 16 kHz. Su Windows la conversione la fa questa classe; sul Mac
/// la fa già il programma cattura-audio, quindi qui resta solo la frequenza, usata dal codice in comune.
/// </summary>
public static class ConvertitoreAudio
{
    public const int FrequenzaUscita = 16000;
}
