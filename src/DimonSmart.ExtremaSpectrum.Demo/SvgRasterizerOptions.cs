namespace DimonSmart.ExtremaSpectrum.Demo;

internal sealed class SvgRasterizerOptions
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }

    public double CropLeftPercent { get; init; }

    public double CropTopPercent { get; init; }

    public double CropRightPercent { get; init; }

    public double CropBottomPercent { get; init; }

    public double Dpi { get; init; } = 96d;
}
