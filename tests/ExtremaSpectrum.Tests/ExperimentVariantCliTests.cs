using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class ExperimentVariantCliTests
{
    [Theory]
    [InlineData("baseline", "baseline")]
    [InlineData("hard-gap-raw", "hard-gap-raw")]
    [InlineData("hard-gap-period-normalized", "hard-gap-period-normalized")]
    public void Parse_KnownValues_RoundTrips(string input, string expectedCliValue)
    {
        var variant = ExperimentVariantCli.Parse(input);
        Assert.Equal(expectedCliValue, ExperimentVariantCli.ToCliValue(variant));
    }

    [Fact]
    public void Parse_UnknownValue_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => ExperimentVariantCli.Parse("unknown"));
        Assert.Contains("Unknown experiment variant", error.Message);
    }
}
