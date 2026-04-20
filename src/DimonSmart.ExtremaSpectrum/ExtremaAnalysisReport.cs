namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Detailed analysis output with both the final spectrum and per-pass trace data.
/// </summary>
public sealed class ExtremaAnalysisReport
{
    /// <summary>
    /// Final accumulated spectrum across all passes.
    /// </summary>
    public required float[] Spectrum { get; init; }

    /// <summary>Lower frequency boundary (Hz) of each bin.</summary>
    public required float[] BinStartHz { get; init; }

    /// <summary>Upper frequency boundary (Hz) of each bin.</summary>
    public required float[] BinEndHz { get; init; }

    /// <summary>Centre frequency (Hz) of each bin.</summary>
    public required float[] BinCenterHz { get; init; }

    /// <summary>
    /// Per-pass spectrum contributions. <c>PassSpectra[i]</c> corresponds to <c>Passes[i]</c>.
    /// </summary>
    public required IReadOnlyList<float[]> PassSpectra { get; init; }

    /// <summary>
    /// Number of oscillations per pass that contributed to the spectrum.
    /// This can be smaller than <c>Passes[i].OscillationCount</c> when some accepted
    /// oscillations fall outside the configured frequency range.
    /// </summary>
    public required IReadOnlyList<int> OscillationsPerPass { get; init; }

    /// <summary>
    /// Geometric trace for each decomposition pass.
    /// </summary>
    public required IReadOnlyList<ExtremaPassSnapshot> Passes { get; init; }

    /// <summary>Sample rate used during analysis.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Number of analysed input samples.</summary>
    public required int InputSampleCount { get; init; }

    /// <summary>Number of passes actually performed.</summary>
    public int PassesPerformed => PassSpectra.Count;

    /// <summary>Total number of oscillations that contributed to the spectrum.</summary>
    public int OscillationsDetected => OscillationsPerPass.Sum();

    /// <summary>Total accumulated spectrum contribution.</summary>
    public float TotalContribution => Spectrum.Sum();

    /// <summary>
    /// Returns the fraction of the total contribution that lies below the specified cutoff frequency.
    /// </summary>
    /// <param name="cutoffHz">Upper frequency limit in Hz.</param>
    /// <returns>A value in the range [0, 1].</returns>
    public float LeakageRatioBelow(float cutoffHz)
    {
        var total = TotalContribution;
        if (total <= 0f)
            return 0f;

        return ContributionBelow(cutoffHz) / total;
    }

    /// <summary>
    /// Sums spectrum contribution below the specified cutoff frequency.
    /// </summary>
    /// <param name="cutoffHz">Upper frequency limit in Hz.</param>
    /// <param name="passIndex">
    /// Optional zero-based pass index. When omitted, the method uses the total spectrum.
    /// </param>
    /// <returns>The accumulated contribution below <paramref name="cutoffHz"/>.</returns>
    public float ContributionBelow(float cutoffHz, int? passIndex = null)
    {
        var source = passIndex is int index
            ? PassSpectra[index]
            : Spectrum;

        var sum = 0f;
        for (var i = 0; i < source.Length; i++)
        {
            if (BinEndHz[i] <= cutoffHz)
                sum += source[i];
        }

        return sum;
    }
}
