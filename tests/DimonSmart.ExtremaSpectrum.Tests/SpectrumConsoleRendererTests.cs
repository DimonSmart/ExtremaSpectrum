using DimonSmart.ExtremaSpectrum.Demo;

namespace DimonSmart.ExtremaSpectrum.Tests;

public sealed class SpectrumConsoleRendererTests
{
    [Fact]
    public void ComputeHeights_SmallerOverallScale_ProducesShorterBars()
    {
        var spectrum = new[] { 1f, 0.25f };

        var fullScale = SpectrumConsoleRenderer.ComputeHeights(spectrum, chartHeight: 12, overallScale: 1f);
        var halfScale = SpectrumConsoleRenderer.ComputeHeights(spectrum, chartHeight: 12, overallScale: 0.5f);

        Assert.Equal([12, 6], fullScale);
        Assert.Equal([6, 3], halfScale);
    }

    [Fact]
    public void ComputeHeights_ZeroOverallScale_ReturnsZeroHeights()
    {
        var heights = SpectrumConsoleRenderer.ComputeHeights([1f, 0.25f], chartHeight: 12, overallScale: 0f);

        Assert.Equal([0, 0], heights);
    }

    [Fact]
    public void ComputeHeights_FullOverallScale_PreservesExistingBehavior()
    {
        var heights = SpectrumConsoleRenderer.ComputeHeights([1f, 0.25f, 0f], chartHeight: 12, overallScale: 1f);

        Assert.Equal([12, 6, 0], heights);
    }

    [Fact]
    public void BuildPassDiagnostics_IncludesAverageContributionAndTopBin()
    {
        var report = new ExtremaAnalysisReport
        {
            Spectrum = [2f, 1f],
            BinStartHz = [100f, 200f],
            BinEndHz = [200f, 300f],
            BinCenterHz = [150f, 250f],
            PassSpectra = new List<float[]>
            {
                new float[] { 2f, 0f },
                new float[] { 0f, 1f }
            },
            OscillationsPerPass = [2, 4],
            Passes = new List<ExtremaPassSnapshot>
            {
                new()
                {
                    PassIndex = 0,
                    SourceSegments = [new ExtremaSegmentRange(0, 10)],
                    RemainingSegments = [new ExtremaSegmentRange(4, 10)],
                    WaveformBeforePass = new float[] { 0f, 1f },
                    WaveformAfterPass = new float[] { 0f, 1f },
                    Oscillations =
                    [
                        new ExtremaOscillationTrace(1, 2, 3, 150f, 1f, 0, 1f),
                        new ExtremaOscillationTrace(4, 5, 6, 150f, 1f, 0, 1f)
                    ],
                    SpectrumContribution = new float[] { 2f, 0f }
                },
                new()
                {
                    PassIndex = 1,
                    SourceSegments = [new ExtremaSegmentRange(4, 10)],
                    RemainingSegments = [new ExtremaSegmentRange(7, 10)],
                    WaveformBeforePass = new float[] { 0f, 1f },
                    WaveformAfterPass = new float[] { 0f, 0.5f },
                    Oscillations =
                    [
                        new ExtremaOscillationTrace(4, 5, 6, 250f, 0.25f, 1, 0.25f),
                        new ExtremaOscillationTrace(5, 6, 7, 250f, 0.25f, 1, 0.25f),
                        new ExtremaOscillationTrace(6, 7, 8, 250f, 0.25f, 1, 0.25f),
                        new ExtremaOscillationTrace(7, 8, 9, 250f, 0.25f, 1, 0.25f)
                    ],
                    SpectrumContribution = new float[] { 0f, 1f }
                }
            },
            SampleRate = 16000,
            InputSampleCount = 32
        };

        var lines = SpectrumConsoleRenderer.BuildPassDiagnostics(report);

        Assert.Equal(3, lines.Count);
        Assert.Contains("avg/osc=1", lines[1]);
        Assert.Contains("top=0.1-0.2 kHz", lines[1]);
        Assert.Contains("activeBins=1", lines[2]);
    }
}
