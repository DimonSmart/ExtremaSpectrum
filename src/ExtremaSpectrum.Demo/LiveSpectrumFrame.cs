namespace ExtremaSpectrum.Demo;

internal sealed class LiveSpectrumFrame
{
    public required AnalysisResult Result { get; init; }

    public required float Rms { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }
}
