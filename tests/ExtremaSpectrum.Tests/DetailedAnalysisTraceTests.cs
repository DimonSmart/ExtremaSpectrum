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
    public void AnalyzeDetailed_RemainingSegmentsUseMidpointsBetweenExtrema()
    {
        const int sampleRate = 600;
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 8,
            MinFrequencyHz = 10f,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = 1
        };

        float[] samples = [0f, 1f, 2f, 1f, 0f, -1f, 0f, 1f, 2f, 1f, 0f];
        var report = new ExtremaSpectrumAnalyzer(options).AnalyzeDetailed(samples, sampleRate);

        Assert.Single(report.Passes);
        Assert.Equal(
            [new ExtremaSegmentRange(0, 0), new ExtremaSegmentRange(10, 10)],
            report.Passes[0].RemainingSegments);
        Assert.Single(report.Passes[0].Oscillations);
        Assert.Equal(0, report.Passes[0].Oscillations[0].LeftBoundarySample);
        Assert.Equal(10, report.Passes[0].Oscillations[0].RightBoundarySample);
    }

    [Fact]
    public void AnalyzeDetailed_AdjacentOscillationsReuseSharedMidpointBoundaries()
    {
        const int sampleRate = 1000;
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 8,
            MinFrequencyHz = 10f,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = 1
        };

        float[] samples =
        [
            0f, 1f, 2f, 1f, 0f,
            -1f, -2f, -1f, 0f,
            1f, 2f, 1f, 0f,
            -1f, -2f, -1f, 0f,
            1f, 2f, 1f, 0f
        ];

        var report = new ExtremaSpectrumAnalyzer(options).AnalyzeDetailed(samples, sampleRate);

        Assert.Single(report.Passes);
        Assert.Equal(3, report.Passes[0].OscillationCount);
        Assert.Equal(
            [new ExtremaSegmentRange(0, 0), new ExtremaSegmentRange(8, 8), new ExtremaSegmentRange(12, 12), new ExtremaSegmentRange(20, 20)],
            report.Passes[0].RemainingSegments);
        Assert.Equal(0, report.Passes[0].Oscillations[0].LeftBoundarySample);
        Assert.Equal(20, report.Passes[0].Oscillations[^1].RightBoundarySample);
    }

    [Fact]
    public void AnalyzeDetailed_NextPassReceivesSmoothedWaveform()
    {
        const int sampleRate = 16000;
        var options = new ExtremaSpectrumOptions
        {
            BinCount = 30,
            MinFrequencyHz = 0f,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = 8
        };

        var samples = new float[sampleRate / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var time = i / (double)sampleRate;
            samples[i] = (float)(
                (0.9 * Math.Sin(2 * Math.PI * 10 * time)) +
                (0.15 * Math.Sin(2 * Math.PI * 160 * time)));
        }

        var report = new ExtremaSpectrumAnalyzer(options).AnalyzeDetailed(samples, sampleRate);

        Assert.True(report.PassesPerformed > 1, "Expected the smoothed waveform to produce another pass.");
        Assert.Single(report.Passes[1].SourceSegments);
        Assert.Equal(0, report.Passes[1].SourceSegments[0].StartSample);
        Assert.Equal(samples.Length - 1, report.Passes[1].SourceSegments[0].EndSample);
        Assert.Equal(report.Passes[0].WaveformAfterPass, report.Passes[1].WaveformBeforePass);

        var changedSampleFound = false;
        for (var i = 0; i < samples.Length; i++)
        {
            if (MathF.Abs(report.Passes[0].WaveformAfterPass[i] - samples[i]) <= 1e-6f)
                continue;

            changedSampleFound = true;
            break;
        }

        Assert.True(changedSampleFound, "The first pass did not alter the waveform that feeds the next pass.");
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
                Assert.Contains("gray dashed=input waveform of this pass", passText);
                Assert.Contains("brown dots=extrema used on input", passText);
                Assert.Contains("bright blue=output waveform of this pass", passText);
                Assert.Contains("<polyline", passText);
                Assert.Contains("stroke-dasharray=\"1 10\"", passText);
                Assert.Contains("<circle", passText);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void WaveformStepSvgExporter_UsesInitialVerticalScaleForLaterPasses()
    {
        const int sampleRate = 1000;
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ExtremaSpectrumTests", Guid.NewGuid().ToString("N"));

        try
        {
            float[] samples = [0f, 1f, 0f, -1f, 0f];
            var report = new ExtremaAnalysisReport
            {
                Spectrum = [0f],
                BinStartHz = [0f],
                BinEndHz = [1f],
                BinCenterHz = [0.5f],
                PassSpectra = [new float[] { 0f }],
                OscillationsPerPass = [0],
                Passes =
                [
                    new ExtremaPassSnapshot
                    {
                        PassIndex = 0,
                        SourceSegments = [new ExtremaSegmentRange(0, 4)],
                        RemainingSegments = [new ExtremaSegmentRange(0, 4)],
                        WaveformBeforePass = new float[] { 0f, 0.25f, 0f, -0.25f, 0f },
                        WaveformAfterPass = new float[] { 0f, 0.25f, 0f, -0.25f, 0f },
                        Oscillations = [],
                        SpectrumContribution = new float[] { 0f }
                    }
                ],
                SampleRate = sampleRate,
                InputSampleCount = samples.Length
            };

            var exported = WaveformStepSvgExporter.ExportSegment(
                outputDirectory,
                "trace",
                segmentIndex: 0,
                sampleRate,
                samples,
                report);

            var passText = File.ReadAllText(exported[1]);

            Assert.Contains("448,271.5", passText);
            Assert.DoesNotContain("448,84", passText);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
