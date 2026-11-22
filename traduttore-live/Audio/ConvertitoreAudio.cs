using NAudio.Dsp;
using NAudio.Wave;

namespace TraduttoreLive.Audio;

/// <summary>
/// Converte i pacchetti catturati da WASAPI (di solito float 32 bit, 48 kHz, stereo) nel formato
/// che serve a Whisper: float mono a 16 kHz.
/// </summary>
public sealed class ConvertitoreAudio
{
    public const int FrequenzaUscita = 16000;

    readonly int _canali;
    readonly int _bytePerCampione;
    readonly bool _float;
    readonly WdlResampler? _resampler;
    readonly double _rapporto;
    float[] _mono = [];

    public ConvertitoreAudio(WaveFormat formato)
    {
        _canali = Math.Max(1, formato.Channels);
        _bytePerCampione = formato.BitsPerSample / 8;
        _float = formato.Encoding == WaveFormatEncoding.IeeeFloat
                 || (formato is WaveFormatExtensible est && est.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT)
                 || (formato.Encoding == WaveFormatEncoding.Extensible && formato.BitsPerSample == 32 && formato is not WaveFormatExtensible);

        if (_float && _bytePerCampione != 4)
            throw new NotSupportedException($"Formato audio non supportato: float a {formato.BitsPerSample} bit.");
        if (!_float && _bytePerCampione is not (2 or 3 or 4))
            throw new NotSupportedException($"Formato audio non supportato: PCM a {formato.BitsPerSample} bit.");

        if (formato.SampleRate != FrequenzaUscita)
        {
            _resampler = new WdlResampler();
            _resampler.SetMode(true, 2, false);
            _resampler.SetFilterParms();
            _resampler.SetFeedMode(true); // si forniscono i campioni in ingresso, il resampler dice quanti ne escono
            _resampler.SetRates(formato.SampleRate, FrequenzaUscita);
            _rapporto = (double)FrequenzaUscita / formato.SampleRate;
        }
    }

    /// <summary>Converte un pacchetto. Restituisce un array vuoto se non ci sono campioni in uscita.</summary>
    public float[] Converti(ReadOnlySpan<byte> dati, bool silenzioso)
    {
        int frame = dati.Length / (_bytePerCampione * _canali);
        if (frame == 0) return [];
        if (_mono.Length < frame) _mono = new float[frame];

        if (silenzioso)
        {
            Array.Clear(_mono, 0, frame);
        }
        else
        {
            int pos = 0;
            for (int f = 0; f < frame; f++)
            {
                float somma = 0;
                for (int c = 0; c < _canali; c++)
                {
                    somma += LeggiCampione(dati, pos);
                    pos += _bytePerCampione;
                }
                _mono[f] = somma / _canali;
            }
        }

        if (_resampler is null)
            return _mono.AsSpan(0, frame).ToArray();

        _resampler.ResamplePrepare(frame, 1, out Span<float> ingresso);
        _mono.AsSpan(0, frame).CopyTo(ingresso);
        var uscita = new float[(int)(frame * _rapporto) + 32];
        int prodotti = _resampler.ResampleOut(uscita, frame, uscita.Length, 1);
        return prodotti == uscita.Length ? uscita : uscita.AsSpan(0, prodotti).ToArray();
    }

    float LeggiCampione(ReadOnlySpan<byte> d, int i)
    {
        if (_float) return BitConverter.ToSingle(d.Slice(i, 4));
        return _bytePerCampione switch
        {
            2 => BitConverter.ToInt16(d.Slice(i, 2)) / 32768f,
            3 => ((d[i] << 8 | d[i + 1] << 16 | d[i + 2] << 24) >> 8) / 8388608f,
            _ => BitConverter.ToInt32(d.Slice(i, 4)) / 2147483648f,
        };
    }
}
