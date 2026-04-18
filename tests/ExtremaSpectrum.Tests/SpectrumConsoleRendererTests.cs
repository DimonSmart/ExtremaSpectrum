using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

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
}
