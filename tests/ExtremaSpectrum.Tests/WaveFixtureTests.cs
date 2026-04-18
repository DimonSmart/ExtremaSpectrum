using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class WaveFixtureTests
{
    [Theory]
    [InlineData("sine-0440hz.wav", 440f)]
    [InlineData("sine-1000hz.wav", 1000f)]
    [InlineData("sine-2600hz.wav", 2600f)]
    public void PureSineWaveFile_PeakBinContainsSineFrequency(string fileName, float expectedFrequencyHz)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        Assert.True(File.Exists(path), $"Missing test fixture '{path}'.");

        var wave = WaveFileReader.ReadMono(path);
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 64,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = wave.NyquistHz,
            MaxPasses = 20
        };

        var result = new ExtremaSpectrumAnalyzer(options).Analyze(wave.Samples, wave.SampleRate);

        var peakBin = Helpers.PeakBin(result.Spectrum);
        var expectedBin = Helpers.ExpectedBin(options, expectedFrequencyHz);

        Assert.InRange(
            peakBin,
            Math.Max(0, expectedBin - 2),
            Math.Min(options.BinCount - 1, expectedBin + 2));
    }
}
