namespace ExtremaSpectrum;

internal sealed class ExtremaExperimentReport
{
    public required ExtremaExperimentVariant Variant { get; init; }

    public required float[] Spectrum { get; init; }

    public required float[] BinStartHz { get; init; }

    public required float[] BinEndHz { get; init; }

    public required float[] BinCenterHz { get; init; }

    public required IReadOnlyList<float[]> PassSpectra { get; init; }

    public required IReadOnlyList<int> OscillationsPerPass { get; init; }

    public required int SampleRate { get; init; }

    public required int InputSampleCount { get; init; }

    public int PassesPerformed => PassSpectra.Count;

    public int OscillationsDetected => OscillationsPerPass.Sum();

    public float TotalContribution => Spectrum.Sum();

    public float LeakageRatioBelow(float cutoffHz)
    {
        var total = TotalContribution;
        if (total <= 0f)
            return 0f;

        return ContributionBelow(cutoffHz) / total;
    }

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
