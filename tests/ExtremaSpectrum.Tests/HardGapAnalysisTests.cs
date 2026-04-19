using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class HardGapAnalysisTests
{
    private static readonly ExtremaSpectrumOptions AnalysisOptions = new()
    {
        BinCount = 64,
        MinFrequencyHz = 100f,
        MaxFrequencyHz = 8000f,
        MaxPasses = 12
    };

    public static TheoryData<string, float> SingleToneCases => new()
    {
        { "sine-0440hz.wav", 440f },
        { "sine-1000hz.wav", 1000f },
        { "sine-2600hz.wav", 2600f }
    };

    public static TheoryData<string> MixedToneCases => new()
    {
        { "mix-0440-1000-equal.wav" },
        { "mix-0440-1000-2600-equal.wav" },
        { "mix-0440-1000-2600-descending.wav" },
        { "mix-0350-1100-3200-equal.wav" },
        { "mix-0440-0880-1760-descending.wav" },
        { "mix-0500-1500-3500-equal.wav" }
    };

    [Theory]
    [MemberData(nameof(SingleToneCases))]
    public void SingleTone_ExpectedRegionRemainsTop(string fileName, float expectedFrequencyHz)
    {
        var report = AnalyzeFixture(fileName);
        var expectedBin = Helpers.ExpectedBin(AnalysisOptions, expectedFrequencyHz);
        var expectedRegion = Helpers.SumRegion(report.Spectrum, expectedBin, 1);
        var maxRegion = Helpers.MaxRegionValue(report.Spectrum, 1);

        Assert.True(
            expectedRegion >= maxRegion * 0.70f,
            $"HardGapRaw lost the main region for {fileName}. Expected region={expectedRegion}, max={maxRegion}.");
    }

    [Theory]
    [MemberData(nameof(MixedToneCases))]
    public void MixedTones_ReportsAreNonEmpty(string fileName)
    {
        var report = AnalyzeFixture(fileName);
        Assert.True(report.PassesPerformed > 0, $"HardGapRaw produced no passes for {fileName}.");
        Assert.True(report.TotalContribution > 0f, $"HardGapRaw produced an empty spectrum for {fileName}.");
        Assert.Equal(report.PassesPerformed, report.OscillationsPerPass.Count);
    }

    private static ExtremaAnalysisReport AnalyzeFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        Assert.True(File.Exists(path), $"Missing test fixture '{path}'.");

        var wave = WaveFileReader.ReadMono(path);
        return new ExtremaSpectrumAnalyzer(AnalysisOptions).AnalyzeDetailed(wave.Samples, wave.SampleRate);
    }
}
