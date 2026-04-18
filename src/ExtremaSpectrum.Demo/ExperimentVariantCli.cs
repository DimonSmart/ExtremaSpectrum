namespace ExtremaSpectrum.Demo;

internal static class ExperimentVariantCli
{
    internal static ExtremaExperimentVariant Parse(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        return value.Trim().ToLowerInvariant() switch
        {
            "baseline" => ExtremaExperimentVariant.Baseline,
            "hard-gap-raw" => ExtremaExperimentVariant.HardGapRaw,
            "hard-gap-period-normalized" => ExtremaExperimentVariant.HardGapPeriodNormalized,
            _ => throw new ArgumentException(
                $"Unknown experiment variant '{value}'. Expected one of: baseline, hard-gap-raw, hard-gap-period-normalized.")
        };
    }

    internal static string ToCliValue(ExtremaExperimentVariant variant)
    {
        return variant switch
        {
            ExtremaExperimentVariant.Baseline => "baseline",
            ExtremaExperimentVariant.HardGapRaw => "hard-gap-raw",
            ExtremaExperimentVariant.HardGapPeriodNormalized => "hard-gap-period-normalized",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown experiment variant.")
        };
    }

    internal static string ToDisplayName(ExtremaExperimentVariant variant)
    {
        return variant switch
        {
            ExtremaExperimentVariant.Baseline => "Baseline",
            ExtremaExperimentVariant.HardGapRaw => "HardGapRaw",
            ExtremaExperimentVariant.HardGapPeriodNormalized => "HardGapPeriodNormalized",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown experiment variant.")
        };
    }
}
