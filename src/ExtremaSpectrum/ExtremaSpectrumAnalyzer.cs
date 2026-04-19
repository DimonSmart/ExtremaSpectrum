namespace ExtremaSpectrum;

/// <summary>
/// Analyses audio signals using an extrema-based oscillation decomposition algorithm.
/// </summary>
/// <remarks>
/// <para>
/// The output is <b>not a Fourier / FFT spectrum</b>. It represents the distribution
/// of detected local oscillations by frequency. See the README for a full description
/// of the algorithm and its limitations.
/// </para>
/// <para>
/// A single instance is <b>not</b> thread-safe for concurrent calls. Use separate
/// instances per thread.
/// </para>
/// </remarks>
public sealed class ExtremaSpectrumAnalyzer
{
    private readonly ExtremaSpectrumOptions _options;
    private readonly float[] _binStartHz;
    private readonly float[] _binEndHz;
    private readonly float[] _binCenterHz;

    /// <summary>
    /// Initialises a new analyser with the specified options.
    /// </summary>
    /// <param name="options">Analysis configuration. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option value is out of range.</exception>
    public ExtremaSpectrumAnalyzer(ExtremaSpectrumOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        _options = options;
        (_binStartHz, _binEndHz, _binCenterHz) = BuildBinFrequencies(options);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Analyses a mono float sample array.
    /// </summary>
    /// <param name="samples">
    /// Normalised audio samples in the range [−1.0, +1.0].
    /// </param>
    /// <param name="sampleRate">Sample rate in Hz. Must be &gt; 0.</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sampleRate"/> ≤ 0.</exception>
    public AnalysisResult Analyze(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sampleRate must be > 0.");

        return RunCore(samples, sampleRate);
    }

    /// <summary>
    /// Analyses a mono float sample array and returns detailed per-pass trace data.
    /// </summary>
    /// <param name="samples">Normalised audio samples in the range [-1.0, +1.0].</param>
    /// <param name="sampleRate">Sample rate in Hz. Must be &gt; 0.</param>
    /// <returns>The detailed analysis report.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sampleRate"/> is &lt;= 0.</exception>
    public ExtremaAnalysisReport AnalyzeDetailed(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sampleRate must be > 0.");

        var execution = ExtremaEngine.Execute(samples, sampleRate, _options, captureDetails: true);
        return CreateDetailedReport(execution, sampleRate, samples.Length);
    }

    /// <summary>
    /// Analyses a raw PCM-16 (signed 16-bit little-endian) byte buffer.
    /// </summary>
    /// <param name="buffer">Raw PCM bytes. Length must be a multiple of blockAlign.</param>
    /// <param name="format">Buffer format descriptor.</param>
    /// <returns>The analysis result.</returns>
    public AnalysisResult AnalyzePcm16(ReadOnlySpan<byte> buffer, AudioBufferFormat format)
    {
        ValidateFormat(format, 16);
        var mono = PcmConverter.ToMonoFloat(buffer, format);
        return RunCore(mono, format.SampleRate);
    }

    /// <summary>
    /// Analyses a raw 32-bit IEEE float byte buffer.
    /// </summary>
    /// <param name="buffer">Raw float bytes. Length must be a multiple of blockAlign.</param>
    /// <param name="format">Buffer format descriptor.</param>
    /// <returns>The analysis result.</returns>
    public AnalysisResult AnalyzeFloat32(ReadOnlySpan<byte> buffer, AudioBufferFormat format)
    {
        ValidateFormat(format, 32);
        var mono = PcmConverter.ToMonoFloatFrom32(buffer, format);
        return RunCore(mono, format.SampleRate);
    }

    // -----------------------------------------------------------------------
    // Core execution
    // -----------------------------------------------------------------------

    private AnalysisResult RunCore(ReadOnlySpan<float> samples, int sampleRate)
    {
        var spectrum = new float[_options.BinCount];

        var (passes, oscillations) = ExtremaEngine.Run(samples, sampleRate, _options, spectrum);

        return new AnalysisResult
        {
            Spectrum            = spectrum,
            BinStartHz          = _binStartHz,
            BinEndHz            = _binEndHz,
            BinCenterHz         = _binCenterHz,
            SampleRate          = sampleRate,
            InputSampleCount    = samples.Length,
            PassesPerformed     = passes,
            OscillationsDetected = oscillations
        };
    }

    private ExtremaAnalysisReport CreateDetailedReport(
        ExtremaEngineExecutionResult execution,
        int sampleRate,
        int inputSampleCount)
    {
        return new ExtremaAnalysisReport
        {
            Spectrum = execution.TotalSpectrum,
            BinStartHz = _binStartHz,
            BinEndHz = _binEndHz,
            BinCenterHz = _binCenterHz,
            PassSpectra = execution.PassSpectra,
            OscillationsPerPass = execution.OscillationsPerPass,
            Passes = execution.Passes,
            SampleRate = sampleRate,
            InputSampleCount = inputSampleCount
        };
    }

    // -----------------------------------------------------------------------
    // Bin construction
    // -----------------------------------------------------------------------

    private static (float[] starts, float[] ends, float[] centers) BuildBinFrequencies(
        ExtremaSpectrumOptions opts)
    {
        var n = opts.BinCount;
        var starts  = new float[n];
        var ends    = new float[n];
        var centers = new float[n];

        var binWidth = (opts.MaxFrequencyHz - opts.MinFrequencyHz) / n;
        for (var i = 0; i < n; i++)
        {
            starts[i]  = opts.MinFrequencyHz + i * binWidth;
            ends[i]    = starts[i] + binWidth;
            centers[i] = starts[i] + binWidth * 0.5f;
        }

        return (starts, ends, centers);
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    private static void ValidateOptions(ExtremaSpectrumOptions o)
    {
        if (o.BinCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(o.BinCount), "BinCount must be > 0.");
        if (o.MinFrequencyHz < 0)
            throw new ArgumentOutOfRangeException(nameof(o.MinFrequencyHz), "MinFrequencyHz must be ≥ 0.");
        if (o.MaxFrequencyHz <= o.MinFrequencyHz)
            throw new ArgumentOutOfRangeException(nameof(o.MaxFrequencyHz),
                "MaxFrequencyHz must be > MinFrequencyHz.");
    }

    private static void ValidateFormat(AudioBufferFormat fmt, int expectedBitsPerSample)
    {
        if (fmt.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(fmt.SampleRate), "SampleRate must be > 0.");
        if (fmt.Channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(fmt.Channels), "Channels must be > 0.");
        if (fmt.BitsPerSample != expectedBitsPerSample)
            throw new ArgumentException(
                $"BitsPerSample must be {expectedBitsPerSample} for this method, " +
                $"but got {fmt.BitsPerSample}.",
                nameof(fmt));
    }
}
