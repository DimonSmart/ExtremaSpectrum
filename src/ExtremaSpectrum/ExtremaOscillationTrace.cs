namespace ExtremaSpectrum;

/// <summary>
/// Describes one local oscillation accepted during a decomposition pass.
/// </summary>
public readonly record struct ExtremaOscillationTrace
{
    /// <summary>
    /// Creates an oscillation trace entry.
    /// </summary>
    /// <param name="leftSample">Index of the left extremum.</param>
    /// <param name="midSample">Index of the middle extremum.</param>
    /// <param name="rightSample">Index of the right extremum.</param>
    /// <param name="frequencyHz">Estimated local frequency in Hz.</param>
    /// <param name="amplitude">Measured local amplitude.</param>
    /// <param name="binIndex">Spectrum bin index, or <c>null</c> when the frequency is outside the configured range.</param>
    /// <param name="contribution">Value added to the spectrum, or <c>0</c> when no bin was updated.</param>
    /// <exception cref="ArgumentException">The extremum order is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric argument is out of range.</exception>
    public ExtremaOscillationTrace(
        int leftSample,
        int midSample,
        int rightSample,
        float frequencyHz,
        float amplitude,
        int? binIndex,
        float contribution)
    {
        if (leftSample < 0)
            throw new ArgumentOutOfRangeException(nameof(leftSample), "leftSample must be >= 0.");
        if (midSample < leftSample)
            throw new ArgumentException("midSample must be >= leftSample.", nameof(midSample));
        if (rightSample < midSample)
            throw new ArgumentException("rightSample must be >= midSample.", nameof(rightSample));
        if (frequencyHz <= 0f)
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), "frequencyHz must be > 0.");
        if (amplitude < 0f)
            throw new ArgumentOutOfRangeException(nameof(amplitude), "amplitude must be >= 0.");
        if (contribution < 0f)
            throw new ArgumentOutOfRangeException(nameof(contribution), "contribution must be >= 0.");
        if (binIndex is int index && index < 0)
            throw new ArgumentOutOfRangeException(nameof(binIndex), "binIndex must be >= 0.");

        LeftSample = leftSample;
        MidSample = midSample;
        RightSample = rightSample;
        FrequencyHz = frequencyHz;
        Amplitude = amplitude;
        BinIndex = binIndex;
        Contribution = contribution;
    }

    /// <summary>Index of the left extremum.</summary>
    public int LeftSample { get; }

    /// <summary>Index of the middle extremum.</summary>
    public int MidSample { get; }

    /// <summary>Index of the right extremum.</summary>
    public int RightSample { get; }

    /// <summary>Estimated local frequency in Hz.</summary>
    public float FrequencyHz { get; }

    /// <summary>Measured local amplitude relative to the local baseline.</summary>
    public float Amplitude { get; }

    /// <summary>
    /// Spectrum bin index that received the contribution, or <c>null</c> if the
    /// oscillation was outside the configured frequency range.
    /// </summary>
    public int? BinIndex { get; }

    /// <summary>
    /// Value added to the spectrum. This is <c>0</c> when <see cref="BinIndex"/> is <c>null</c>.
    /// </summary>
    public float Contribution { get; }

    /// <summary>Measured oscillation width in samples.</summary>
    public int PeriodSamples => RightSample - LeftSample;

    /// <summary>Whether this oscillation contributed to the output spectrum.</summary>
    public bool ContributedToSpectrum => BinIndex is not null;
}
