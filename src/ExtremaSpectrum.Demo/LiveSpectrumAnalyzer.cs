namespace ExtremaSpectrum.Demo;

internal sealed class LiveSpectrumAnalyzer
{
    private readonly ExtremaSpectrumAnalyzer _analyzer;
    private readonly int _windowSamples;
    private readonly int _hopSamples;

    private float[] _buffer;
    private int _count;

    public LiveSpectrumAnalyzer(
        ExtremaSpectrumOptions options,
        int analysisWindowSamples,
        int hopSamples)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (analysisWindowSamples < 3)
            throw new ArgumentOutOfRangeException(nameof(analysisWindowSamples), "analysisWindowSamples must be ≥ 3.");
        if (hopSamples < 1 || hopSamples > analysisWindowSamples)
            throw new ArgumentOutOfRangeException(nameof(hopSamples), "hopSamples must be in [1, analysisWindowSamples].");

        _analyzer = new ExtremaSpectrumAnalyzer(options);
        _windowSamples = analysisWindowSamples;
        _hopSamples = hopSamples;
        _buffer = new float[Math.Max(analysisWindowSamples * 2, 4096)];
    }

    public bool Push(ReadOnlySpan<float> samples, int sampleRate, out LiveSpectrumFrame? frame)
    {
        Append(samples);
        if (_count < _windowSamples)
        {
            frame = null;
            return false;
        }

        var window = new float[_windowSamples];
        Array.Copy(_buffer, 0, window, 0, _windowSamples);

        var result = _analyzer.Analyze(window, sampleRate);

        frame = new LiveSpectrumFrame
        {
            Result = result,
            Rms = ComputeRms(window),
            CapturedAt = DateTimeOffset.Now
        };

        Advance();
        return true;
    }

    public bool PushPcm16(ReadOnlySpan<byte> buffer, AudioBufferFormat format, out LiveSpectrumFrame? frame)
    {
        var mono = PcmConverter.ToMonoFloat(buffer, format);
        return Push(mono, format.SampleRate, out frame);
    }

    private void Append(ReadOnlySpan<float> samples)
    {
        var required = _count + samples.Length;
        if (required > _buffer.Length)
        {
            var grown = new float[Math.Max(_buffer.Length * 2, required)];
            Array.Copy(_buffer, 0, grown, 0, _count);
            _buffer = grown;
        }

        samples.CopyTo(_buffer.AsSpan(_count));
        _count += samples.Length;
    }

    private void Advance()
    {
        var remaining = _count - _hopSamples;
        if (remaining > 0)
            Array.Copy(_buffer, _hopSamples, _buffer, 0, remaining);
        _count = Math.Max(0, remaining);
    }

    private static float ComputeRms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return 0f;

        var sum = 0d;
        for (var i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
