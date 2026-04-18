namespace ExtremaSpectrum;

internal static class ExtremaExperimentRunner
{
    internal static ExtremaExperimentReport Analyze(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions options,
        ExtremaExperimentVariant variant)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sampleRate must be > 0.");

        ValidateOptions(options);
        var (binStartHz, binEndHz, binCenterHz) = BuildBinFrequencies(options);

        var execution = ExtremaEngine.Execute(
            samples,
            sampleRate,
            options,
            variant,
            capturePassSpectra: true);

        return new ExtremaExperimentReport
        {
            Variant = variant,
            Spectrum = execution.TotalSpectrum,
            BinStartHz = binStartHz,
            BinEndHz = binEndHz,
            BinCenterHz = binCenterHz,
            PassSpectra = execution.PassSpectra,
            OscillationsPerPass = execution.OscillationsPerPass,
            SampleRate = sampleRate,
            InputSampleCount = samples.Length
        };
    }

    private static (float[] starts, float[] ends, float[] centers) BuildBinFrequencies(
        ExtremaSpectrumOptions opts)
    {
        var n = opts.BinCount;
        var starts = new float[n];
        var ends = new float[n];
        var centers = new float[n];

        var binWidth = (opts.MaxFrequencyHz - opts.MinFrequencyHz) / n;
        for (var i = 0; i < n; i++)
        {
            starts[i] = opts.MinFrequencyHz + i * binWidth;
            ends[i] = starts[i] + binWidth;
            centers[i] = starts[i] + binWidth * 0.5f;
        }

        return (starts, ends, centers);
    }

    private static void ValidateOptions(ExtremaSpectrumOptions options)
    {
        if (options.BinCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.BinCount), "BinCount must be > 0.");
        if (options.MinFrequencyHz < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MinFrequencyHz), "MinFrequencyHz must be ≥ 0.");
        if (options.MaxFrequencyHz <= options.MinFrequencyHz)
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxFrequencyHz),
                "MaxFrequencyHz must be > MinFrequencyHz.");
    }
}
