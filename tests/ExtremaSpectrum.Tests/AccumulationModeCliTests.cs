using ExtremaSpectrum.Demo;

namespace ExtremaSpectrum.Tests;

public sealed class AccumulationModeCliTests
{
    [Theory]
    [InlineData("amplitude", "amplitude")]
    [InlineData("energy", "energy")]
    public void Parse_KnownValues_RoundTrips(string input, string expectedCliValue)
    {
        var mode = AccumulationModeCli.Parse(input);
        Assert.Equal(expectedCliValue, AccumulationModeCli.ToCliValue(mode));
    }

    [Fact]
    public void Parse_UnknownValue_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => AccumulationModeCli.Parse("unknown"));
        Assert.Contains("Unknown accumulation mode", error.Message);
    }
}
