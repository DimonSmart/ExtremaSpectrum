using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class DetailedAnalysisTraceTests
{
    [Fact]
    public void AnalyzeDetailed_SummaryMatchesRegularAnalyze()
    {
        const int sampleRate = 16000;
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 32,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = 12
        };

        var samples = Helpers.Sine(sampleRate, 440f, sampleRate * 2, 0.8f);
        var analyzer = new ExtremaSpectrumAnalyzer(options);

        var regular = analyzer.Analyze(samples, sampleRate);
        var detailed = analyzer.AnalyzeDetailed(samples, sampleRate);

        Assert.Equal(regular.Spectrum, detailed.Spectrum);
        Assert.Equal(regular.PassesPerformed, detailed.PassesPerformed);
        Assert.Equal(regular.OscillationsDetected, detailed.OscillationsDetected);
        Assert.Equal(detailed.PassesPerformed, detailed.PassSpectra.Count);
        Assert.Equal(detailed.PassesPerformed, detailed.Passes.Count);
    }

    [Fact]
    public void AnalyzeDetailed_FirstPassStartsFromWholeSignal()
    {
        const int sampleRate = 16000;
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 32,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = 12
        };

        var samples = Helpers.Sine(sampleRate, 1000f, sampleRate, 0.8f);
        var report = new ExtremaSpectrumAnalyzer(options).AnalyzeDetailed(samples, sampleRate);

        Assert.NotEmpty(report.Passes);
        Assert.Single(report.Passes[0].SourceSegments);
        Assert.Equal(0, report.Passes[0].SourceSegments[0].StartSample);
        Assert.Equal(samples.Length - 1, report.Passes[0].SourceSegments[0].EndSample);

        for (var i = 0; i < report.Passes.Count; i++)
            Assert.Equal(i, report.Passes[i].PassIndex);
    }

    [Fact]
    public void WaveformStepSvgExporter_WritesInitialAndPassImages()
    {
        const int sampleRate = 16000;
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ExtremaSpectrumTests", Guid.NewGuid().ToString("N"));

        try
        {
            var options = new ExtremaSpectrumOptions
            {
                BinCount = 32,
                MinFrequencyHz = 100f,
                MaxFrequencyHz = sampleRate / 2f,
                MaxPasses = 6
            };

            var samples = Helpers.Sine(sampleRate, 440f, sampleRate, 0.8f);
            var report = new ExtremaSpectrumAnalyzer(options).AnalyzeDetailed(samples, sampleRate);

            var exported = WaveformStepSvgExporter.ExportSegment(
                outputDirectory,
                "trace",
                segmentIndex: 0,
                sampleRate,
                samples,
                report);

            Assert.Equal(report.PassesPerformed + 1, exported.Count);
            Assert.All(exported, path => Assert.True(File.Exists(path), $"Missing exported SVG '{path}'."));

            var initialText = File.ReadAllText(exported[0]);
            Assert.Contains("<svg", initialText);
            Assert.Contains("Initial waveform", initialText);
            Assert.Contains("<polyline", initialText);

            if (exported.Count > 1)
            {
                var passText = File.ReadAllText(exported[1]);
                Assert.Contains("Pass 1", passText);
                Assert.Contains("bright blue=remaining waveform", passText);
                Assert.Contains("dark blue=stitched after cut", passText);
                Assert.Contains("<polyline", passText);
                Assert.Contains("stroke-dasharray=\"1 10\"", passText);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
