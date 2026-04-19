namespace ExtremaSpectrum;

/// <summary>
/// Inclusive sample range within an analysed signal.
/// </summary>
public readonly record struct ExtremaSegmentRange
{
    /// <summary>
    /// Creates an inclusive sample range.
    /// </summary>
    /// <param name="startSample">Index of the first sample in the range.</param>
    /// <param name="endSample">Index of the last sample in the range.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startSample"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="endSample"/> is smaller than <paramref name="startSample"/>.</exception>
    public ExtremaSegmentRange(int startSample, int endSample)
    {
        if (startSample < 0)
            throw new ArgumentOutOfRangeException(nameof(startSample), "startSample must be >= 0.");
        if (endSample < startSample)
            throw new ArgumentException("endSample must be >= startSample.", nameof(endSample));

        StartSample = startSample;
        EndSample = endSample;
    }

    /// <summary>Index of the first sample in the range.</summary>
    public int StartSample { get; }

    /// <summary>Index of the last sample in the range.</summary>
    public int EndSample { get; }

    /// <summary>Number of samples covered by the range.</summary>
    public int Length => EndSample - StartSample + 1;
}
