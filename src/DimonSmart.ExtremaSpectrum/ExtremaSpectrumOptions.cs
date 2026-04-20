namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Tuning parameters for <see cref="ExtremaSpectrumAnalyzer"/> and
/// <see cref="StreamingExtremaSpectrumAnalyzer"/>.
/// </summary>
public sealed class ExtremaSpectrumOptions
{
    /// <summary>Number of frequency bins in the output spectrum. Must be &gt; 0.</summary>
    public int BinCount { get; init; } = 128;

    /// <summary>Lower bound of the analysed frequency range in Hz. Must be ≥ 0.</summary>
    public float MinFrequencyHz { get; init; } = 100f;

    /// <summary>Upper bound of the analysed frequency range in Hz. Must be &gt; MinFrequencyHz.</summary>
    public float MaxFrequencyHz { get; init; } = 8000f;

    /// <summary>
    /// Maximum number of decomposition passes over the working signal.
    /// The algorithm stops earlier if no valid oscillation triple is found.
    /// </summary>
    public int MaxPasses { get; init; } = 16;

    /// <summary>
    /// Minimum oscillation width in samples. Triples narrower than this are ignored.
    /// </summary>
    public int MinPeriodSamples { get; init; } = 2;

    /// <summary>
    /// Maximum oscillation width in samples.
    /// <c>0</c> (default) means the limit is derived automatically from the input length.
    /// </summary>
    public int MaxPeriodSamples { get; init; } = 0;

    /// <summary>
    /// Minimum oscillation amplitude. Triples whose amplitude is below this threshold are ignored.
    /// </summary>
    public float MinAmplitude { get; init; } = 0f;

    /// <summary>What quantity to accumulate into each bin.</summary>
    public AccumulationMode AccumulationMode { get; init; } = AccumulationMode.Amplitude;

}
