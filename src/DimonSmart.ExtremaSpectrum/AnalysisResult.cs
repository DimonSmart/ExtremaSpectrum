namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Result of an extrema-based spectrum analysis pass.
/// </summary>
/// <remarks>
/// <para>
/// <b>Important:</b> <see cref="Spectrum"/> is NOT a Fourier / FFT spectrum.
/// It represents the distribution of detected local oscillations by frequency,
/// weighted by their amplitude (or energy). Values in adjacent bins are not
/// mathematically orthogonal and should not be interpreted as sinusoidal components.
/// </para>
/// </remarks>
public sealed class AnalysisResult
{
    /// <summary>
    /// Accumulated contribution per frequency bin.
    /// <c>Spectrum[i]</c> corresponds to the frequency range
    /// [<see cref="BinStartHz"/>[i], <see cref="BinEndHz"/>[i]].
    /// </summary>
    public required float[] Spectrum { get; init; }

    /// <summary>Lower frequency boundary (Hz) of each bin.</summary>
    public required float[] BinStartHz { get; init; }

    /// <summary>Upper frequency boundary (Hz) of each bin.</summary>
    public required float[] BinEndHz { get; init; }

    /// <summary>Centre frequency (Hz) of each bin.</summary>
    public required float[] BinCenterHz { get; init; }

    /// <summary>Sample rate used during analysis.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Number of input samples analysed.</summary>
    public required int InputSampleCount { get; init; }

    /// <summary>Number of decomposition passes actually performed.</summary>
    public required int PassesPerformed { get; init; }

    /// <summary>Total number of oscillation triples detected and accepted across all passes.</summary>
    public required int OscillationsDetected { get; init; }
}
