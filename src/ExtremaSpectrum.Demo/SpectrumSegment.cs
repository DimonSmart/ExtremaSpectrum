namespace ExtremaSpectrum.Demo;

internal sealed class SpectrumSegment
{
    public required int Index { get; init; }

    public required int StartSample { get; init; }

    public required int SampleCount { get; init; }

    public required AnalysisResult Result { get; init; }

    public ExtremaAnalysisReport? DetailedReport { get; init; }

    public TimeSpan StartTime(int sampleRate) => TimeSpan.FromSeconds((double)StartSample / sampleRate);

    public TimeSpan EndTime(int sampleRate) => TimeSpan.FromSeconds((double)(StartSample + SampleCount) / sampleRate);
}
