namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Describes the format of a raw PCM audio buffer.
/// </summary>
public readonly struct AudioBufferFormat
{
    /// <summary>Sample rate in Hz (e.g. 44100, 48000).</summary>
    public int SampleRate { get; init; }

    /// <summary>Number of interleaved channels (e.g. 1 for mono, 2 for stereo).</summary>
    public int Channels { get; init; }

    /// <summary>Bits per sample (e.g. 16 for PCM16, 32 for Float32).</summary>
    public int BitsPerSample { get; init; }

    /// <summary>
    /// Whether channels are interleaved (L0 R0 L1 R1 …).
    /// Non-interleaved (planar) layout is also supported.
    /// </summary>
    public bool Interleaved { get; init; }

    /// <summary>How to mix multiple channels down to mono for analysis.</summary>
    public ChannelMixMode ChannelMixMode { get; init; }

    /// <summary>
    /// Zero-based channel index used when <see cref="ChannelMixMode"/> is
    /// <see cref="ChannelMixMode.PreferredChannel"/>.
    /// </summary>
    public int PreferredChannel { get; init; }
}
