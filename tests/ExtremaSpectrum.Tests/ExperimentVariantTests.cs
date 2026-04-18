using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class ExperimentVariantTests
{
    private static readonly ExtremaSpectrumOptions ExperimentOptions = new()
    {
        BinCount = 64,
        MinFrequencyHz = 100f,
        MaxFrequencyHz = 8000f,
        MaxPasses = 12
    };

    public static TheoryData<string, string, float> SingleToneCases => new()
    {
        { "Baseline", "sine-0440hz.wav", 440f },
        { "Baseline", "sine-1000hz.wav", 1000f },
        { "Baseline", "sine-2600hz.wav", 2600f },
        { "HardGapRaw", "sine-0440hz.wav", 440f },
        { "HardGapRaw", "sine-1000hz.wav", 1000f },
        { "HardGapRaw", "sine-2600hz.wav", 2600f },
        { "HardGapPeriodNormalized", "sine-0440hz.wav", 440f },
        { "HardGapPeriodNormalized", "sine-1000hz.wav", 1000f },
        { "HardGapPeriodNormalized", "sine-2600hz.wav", 2600f }
    };

    public static TheoryData<string, string> MixedToneCases => new()
    {
        { "Baseline", "mix-0440-1000-equal.wav" },
        { "Baseline", "mix-0440-1000-2600-equal.wav" },
        { "Baseline", "mix-0440-1000-2600-descending.wav" },
        { "HardGapRaw", "mix-0440-1000-equal.wav" },
        { "HardGapRaw", "mix-0440-1000-2600-equal.wav" },
        { "HardGapRaw", "mix-0440-1000-2600-descending.wav" },
        { "HardGapPeriodNormalized", "mix-0440-1000-equal.wav" },
        { "HardGapPeriodNormalized", "mix-0440-1000-2600-equal.wav" },
        { "HardGapPeriodNormalized", "mix-0440-1000-2600-descending.wav" }
    };

    [Theory]
    [MemberData(nameof(SingleToneCases))]
    public void SingleTone_ExpectedRegionRemainsTop(
        string variantName,
        string fileName,
        float expectedFrequencyHz)
    {
        var variant = ParseVariant(variantName);
        var report = AnalyzeFixture(fileName, variant);
        var expectedBin = Helpers.ExpectedBin(ExperimentOptions, expectedFrequencyHz);
        var expectedRegion = Helpers.SumRegion(report.Spectrum, expectedBin, 1);
        var maxRegion = Helpers.MaxRegionValue(report.Spectrum, 1);

        Assert.True(
            expectedRegion >= maxRegion * 0.70f,
            $"{variant} lost the main region for {fileName}. Expected region={expectedRegion}, max={maxRegion}.");
    }

    [Fact]
    public void HardGapVariants_ReduceLowLeakage_OnPure1000Hz()
    {
        const float cutoffHz = 1000f;

        var baseline = AnalyzeFixture("sine-1000hz.wav", ExtremaExperimentVariant.Baseline);
        var hardGapRaw = AnalyzeFixture("sine-1000hz.wav", ExtremaExperimentVariant.HardGapRaw);
        var hardGapNormalized = AnalyzeFixture("sine-1000hz.wav", ExtremaExperimentVariant.HardGapPeriodNormalized);

        var baselineLeakage = baseline.LeakageRatioBelow(cutoffHz);
        Assert.True(baselineLeakage > 0f, "Baseline should exhibit measurable low-frequency leakage.");

        Assert.True(
            hardGapRaw.LeakageRatioBelow(cutoffHz) < baselineLeakage,
            "HardGapRaw should reduce low-frequency leakage.");
        Assert.True(
            hardGapNormalized.LeakageRatioBelow(cutoffHz) < baselineLeakage,
            "HardGapPeriodNormalized should reduce low-frequency leakage.");

        var baselineLaterLow = LaterPassContributionBelow(baseline, cutoffHz);
        Assert.True(baselineLaterLow > 0f, "Baseline should accumulate low leakage in later passes.");
        Assert.True(
            LaterPassContributionBelow(hardGapRaw, cutoffHz) < baselineLaterLow,
            "HardGapRaw should suppress low leakage from later passes.");
        Assert.True(
            LaterPassContributionBelow(hardGapNormalized, cutoffHz) < baselineLaterLow,
            "HardGapPeriodNormalized should suppress low leakage from later passes.");
    }

    [Theory]
    [MemberData(nameof(MixedToneCases))]
    public void MixedTones_ReportsAreNonEmpty(
        string variantName,
        string fileName)
    {
        var variant = ParseVariant(variantName);
        var report = AnalyzeFixture(fileName, variant);
        Assert.True(report.PassesPerformed > 0, $"{variant} produced no passes for {fileName}.");
        Assert.True(report.TotalContribution > 0f, $"{variant} produced an empty spectrum for {fileName}.");
        Assert.Equal(report.PassesPerformed, report.OscillationsPerPass.Count);
    }

    private static float LaterPassContributionBelow(ExtremaExperimentReport report, float cutoffHz)
    {
        var sum = 0f;
        for (var passIndex = 1; passIndex < report.PassSpectra.Count; passIndex++)
            sum += report.ContributionBelow(cutoffHz, passIndex);
        return sum;
    }

    private static ExtremaExperimentReport AnalyzeFixture(string fileName, ExtremaExperimentVariant variant)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        Assert.True(File.Exists(path), $"Missing test fixture '{path}'.");

        var wave = WaveFileReader.ReadMono(path);
        return ExtremaExperimentRunner.Analyze(wave.Samples, wave.SampleRate, ExperimentOptions, variant);
    }

    private static ExtremaExperimentVariant ParseVariant(string variantName)
    {
        return variantName switch
        {
            "Baseline" => ExtremaExperimentVariant.Baseline,
            "HardGapRaw" => ExtremaExperimentVariant.HardGapRaw,
            "HardGapPeriodNormalized" => ExtremaExperimentVariant.HardGapPeriodNormalized,
            _ => throw new ArgumentOutOfRangeException(nameof(variantName), variantName, "Unknown experiment variant.")
        };
    }
}
