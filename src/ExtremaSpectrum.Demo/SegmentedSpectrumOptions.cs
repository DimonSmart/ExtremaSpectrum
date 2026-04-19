namespace ExtremaSpectrum.Demo;

internal sealed class SegmentedSpectrumOptions
{
    public required string InputPath { get; init; }

    public bool UseMicrophone { get; init; }

    public bool ListInputDevices { get; init; }

    public int MicrophoneDeviceIndex { get; init; } = 0;

    public int MicrophoneSampleRate { get; init; } = 16000;

    public int MicrophoneBufferMilliseconds { get; init; } = 50;

    public float MicrophoneSilenceRmsThreshold { get; init; } = 0.0005f;

    public float MicrophoneDisplayReferenceRms { get; init; } = 0.01f;

    public float MinFrequencyHz { get; init; } = 100f;

    public double WindowSeconds { get; init; } = 5d;

    public double OverlapSeconds { get; init; } = 1d;

    public int BinCount { get; init; } = 20;

    public int ChartHeight { get; init; } = 12;

    public int MaxPasses { get; init; } = 12;

    public float MinAmplitude { get; init; } = 0f;

    public AccumulationMode AccumulationMode { get; init; } = AccumulationMode.Amplitude;

    public bool DumpPasses { get; init; }

    public string? StepImageOutputDirectory { get; init; }

    public int WindowSamples(int sampleRate) => SecondsToSamples(WindowSeconds, sampleRate);

    public int OverlapSamples(int sampleRate) => SecondsToSamples(OverlapSeconds, sampleRate, minimumSamples: 0);

    public int HopSamples(int sampleRate)
    {
        var windowSamples = WindowSamples(sampleRate);
        var overlapSamples = OverlapSamples(sampleRate);

        if (overlapSamples >= windowSamples)
            throw new InvalidOperationException("Overlap must be smaller than the window length.");

        return windowSamples - overlapSamples;
    }

    public double HopSeconds => WindowSeconds - OverlapSeconds;

    private static int SecondsToSamples(double seconds, int sampleRate, int minimumSamples = 1)
        => Math.Max(minimumSamples, (int)Math.Round(seconds * sampleRate, MidpointRounding.AwayFromZero));
}
