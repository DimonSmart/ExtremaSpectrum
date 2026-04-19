namespace ExtremaSpectrum.Demo;

internal static class AccumulationModeCli
{
    internal static AccumulationMode Parse(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        return value.Trim().ToLowerInvariant() switch
        {
            "amplitude" => AccumulationMode.Amplitude,
            "energy" => AccumulationMode.Energy,
            _ => throw new ArgumentException(
                $"Unknown accumulation mode '{value}'. Expected one of: amplitude, energy.")
        };
    }

    internal static string ToCliValue(AccumulationMode mode)
    {
        return mode switch
        {
            AccumulationMode.Amplitude => "amplitude",
            AccumulationMode.Energy => "energy",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown accumulation mode.")
        };
    }

    internal static string ToDisplayName(AccumulationMode mode)
    {
        return mode switch
        {
            AccumulationMode.Amplitude => "Amplitude",
            AccumulationMode.Energy => "Energy",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown accumulation mode.")
        };
    }
}
