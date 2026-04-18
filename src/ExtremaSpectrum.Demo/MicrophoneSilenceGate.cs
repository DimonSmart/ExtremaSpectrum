namespace ExtremaSpectrum.Demo;

internal static class MicrophoneSilenceGate
{
    internal static LiveSpectrumFrame Apply(LiveSpectrumFrame frame, float rmsThreshold)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (rmsThreshold < 0f)
            throw new ArgumentOutOfRangeException(nameof(rmsThreshold), "rmsThreshold must be >= 0.");

        if (rmsThreshold <= 0f || frame.Rms >= rmsThreshold)
            return frame;

        return new LiveSpectrumFrame
        {
            Result = CreateSilentResult(frame.Result),
            Rms = frame.Rms,
            CapturedAt = frame.CapturedAt
        };
    }

    private static AnalysisResult CreateSilentResult(AnalysisResult source)
    {
        return new AnalysisResult
        {
            Spectrum = new float[source.Spectrum.Length],
            BinStartHz = source.BinStartHz,
            BinEndHz = source.BinEndHz,
            BinCenterHz = source.BinCenterHz,
            SampleRate = source.SampleRate,
            InputSampleCount = source.InputSampleCount,
            PassesPerformed = 0,
            OscillationsDetected = 0
        };
    }
}
