namespace ExtremaSpectrum;

/// <summary>
/// Detailed trace data for one decomposition pass.
/// </summary>
public sealed class ExtremaPassSnapshot
{
    /// <summary>Zero-based index of the pass.</summary>
    public required int PassIndex { get; init; }

    /// <summary>
    /// Segments that were still active at the beginning of the pass.
    /// </summary>
    public required IReadOnlyList<ExtremaSegmentRange> SourceSegments { get; init; }

    /// <summary>
    /// Segments that remain active after the pass and will be revisited later.
    /// </summary>
    public required IReadOnlyList<ExtremaSegmentRange> RemainingSegments { get; init; }

    /// <summary>
    /// Oscillations accepted during this pass.
    /// </summary>
    public required IReadOnlyList<ExtremaOscillationTrace> Oscillations { get; init; }

    /// <summary>
    /// Spectrum contribution accumulated during this pass.
    /// </summary>
    public required float[] SpectrumContribution { get; init; }

    /// <summary>Number of accepted oscillations recorded for this pass.</summary>
    public int OscillationCount => Oscillations.Count;

    /// <summary>Total spectrum contribution accumulated during this pass.</summary>
    public float TotalContribution => SpectrumContribution.Sum();
}
