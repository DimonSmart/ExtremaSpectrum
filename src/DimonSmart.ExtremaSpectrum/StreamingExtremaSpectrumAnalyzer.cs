namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// A streaming wrapper over <see cref="ExtremaSpectrumAnalyzer"/> that accumulates
/// arbitrary-sized audio blocks (e.g. from a microphone callback) and triggers
/// analysis whenever a full window is available.
/// </summary>
/// <remarks>
/// <para>
/// Blocks may be of any size. Analysis fires each time the internal ring buffer
/// contains at least <c>analysisWindowSamples</c> samples. After analysis the
/// buffer advances by <c>hopSamples</c> (overlap-add / sliding-window semantics).
/// </para>
/// <para>
/// A single instance is <b>not</b> thread-safe for concurrent calls.
/// </para>
/// </remarks>
public sealed class StreamingExtremaSpectrumAnalyzer
{
    private readonly ExtremaSpectrumAnalyzer _inner;
    private readonly int _windowSamples;
    private readonly int _hopSamples;

    // Ring buffer implemented as a simple growable list; swap window is extracted per analysis.
    private float[] _buffer;
    private int _count; // number of valid samples currently in _buffer[0.._count)

    /// <summary>
    /// Initialises the streaming analyser.
    /// </summary>
    /// <param name="options">Analysis options.</param>
    /// <param name="analysisWindowSamples">
    /// Number of samples required before an analysis is triggered. Must be >= 3.
    /// </param>
    /// <param name="hopSamples">
    /// Number of samples to discard from the front of the buffer after each analysis.
    /// Must be >= 1 and &lt;= <paramref name="analysisWindowSamples"/>.
    /// </param>
    public StreamingExtremaSpectrumAnalyzer(
        ExtremaSpectrumOptions options,
        int analysisWindowSamples,
        int hopSamples)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (analysisWindowSamples < 3)
            throw new ArgumentOutOfRangeException(nameof(analysisWindowSamples), "analysisWindowSamples must be >= 3.");
        if (hopSamples < 1)
            throw new ArgumentOutOfRangeException(nameof(hopSamples), "hopSamples must be >= 1.");
        if (hopSamples > analysisWindowSamples)
            throw new ArgumentOutOfRangeException(nameof(hopSamples), "hopSamples must be <= analysisWindowSamples.");

        _inner = new ExtremaSpectrumAnalyzer(options);
        _windowSamples = analysisWindowSamples;
        _hopSamples = hopSamples;
        _buffer = new float[analysisWindowSamples * 2];
        _count = 0;
    }

    // -----------------------------------------------------------------------
    // Push overloads
    // -----------------------------------------------------------------------

    /// <summary>
    /// Appends a block of mono float samples and analyses if a full window is ready.
    /// </summary>
    /// <param name="samples">Incoming audio block.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="result">
    /// Set to the analysis result if a window was analysed; otherwise <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if an analysis was performed.</returns>
    public bool Push(ReadOnlySpan<float> samples, int sampleRate, out AnalysisResult? result)
    {
        Append(samples);
        if (!TryGetWindow(out var window))
        {
            result = null;
            return false;
        }

        result = _inner.Analyze(window, sampleRate);
        Advance();
        return true;
    }

    /// <summary>
    /// Appends a block of mono float samples and returns detailed per-pass trace data
    /// if a full window is ready.
    /// </summary>
    /// <param name="samples">Incoming audio block.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="result">
    /// Set to the detailed analysis report if a window was analysed; otherwise <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if an analysis was performed.</returns>
    public bool PushDetailed(ReadOnlySpan<float> samples, int sampleRate, out ExtremaAnalysisReport? result)
    {
        Append(samples);
        if (!TryGetWindow(out var window))
        {
            result = null;
            return false;
        }

        result = _inner.AnalyzeDetailed(window, sampleRate);
        Advance();
        return true;
    }

    /// <summary>
    /// Appends a block of PCM-16 bytes and analyses if a full window is ready.
    /// </summary>
    public bool PushPcm16(ReadOnlySpan<byte> buffer, AudioBufferFormat format, out AnalysisResult? result)
    {
        ExtremaSpectrumAnalyzer.ValidateFormat(format, expectedBitsPerSample: 16);
        var mono = PcmConverter.ToMonoFloat(buffer, format);
        return Push(mono, format.SampleRate, out result);
    }

    /// <summary>
    /// Appends a block of PCM-16 bytes and returns detailed per-pass trace data
    /// if a full window is ready.
    /// </summary>
    public bool PushDetailedPcm16(ReadOnlySpan<byte> buffer, AudioBufferFormat format, out ExtremaAnalysisReport? result)
    {
        ExtremaSpectrumAnalyzer.ValidateFormat(format, expectedBitsPerSample: 16);
        var mono = PcmConverter.ToMonoFloat(buffer, format);
        return PushDetailed(mono, format.SampleRate, out result);
    }

    /// <summary>
    /// Appends a block of 32-bit float bytes and analyses if a full window is ready.
    /// </summary>
    public bool PushFloat32(ReadOnlySpan<byte> buffer, AudioBufferFormat format, out AnalysisResult? result)
    {
        ExtremaSpectrumAnalyzer.ValidateFormat(format, expectedBitsPerSample: 32);
        var mono = PcmConverter.ToMonoFloatFrom32(buffer, format);
        return Push(mono, format.SampleRate, out result);
    }

    /// <summary>
    /// Appends a block of 32-bit float bytes and returns detailed per-pass trace data
    /// if a full window is ready.
    /// </summary>
    public bool PushDetailedFloat32(ReadOnlySpan<byte> buffer, AudioBufferFormat format, out ExtremaAnalysisReport? result)
    {
        ExtremaSpectrumAnalyzer.ValidateFormat(format, expectedBitsPerSample: 32);
        var mono = PcmConverter.ToMonoFloatFrom32(buffer, format);
        return PushDetailed(mono, format.SampleRate, out result);
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private void Append(ReadOnlySpan<float> samples)
    {
        var required = _count + samples.Length;
        if (required > _buffer.Length)
        {
            // Grow: at least double, but always large enough.
            var newSize = Math.Max(_buffer.Length * 2, required);
            var grown = new float[newSize];
            _buffer.AsSpan(0, _count).CopyTo(grown);
            _buffer = grown;
        }

        samples.CopyTo(_buffer.AsSpan(_count));
        _count += samples.Length;
    }

    private bool TryGetWindow(out ReadOnlySpan<float> window)
    {
        if (_count < _windowSamples)
        {
            window = default;
            return false;
        }

        window = new ReadOnlySpan<float>(_buffer, 0, _windowSamples);
        return true;
    }

    private void Advance()
    {
        var remaining = _count - _hopSamples;
        if (remaining > 0)
            Array.Copy(_buffer, _hopSamples, _buffer, 0, remaining);
        _count = Math.Max(0, remaining);
    }
}
