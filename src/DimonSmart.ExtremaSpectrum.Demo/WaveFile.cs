namespace DimonSmart.ExtremaSpectrum.Demo;

internal sealed class WaveFile
{
    public required float[] Samples { get; init; }

    public required int SampleRate { get; init; }

    public required short Channels { get; init; }

    public required short BitsPerSample { get; init; }

    public required short FormatTag { get; init; }

    public int FrameCount => Samples.Length;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)FrameCount / SampleRate);

    public float NyquistHz => SampleRate / 2f;
}
