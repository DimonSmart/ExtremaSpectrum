namespace ExtremaSpectrum.Demo;

internal static class AnalysisResultFactory
{
    internal static AnalysisResult FromExperimentReport(ExtremaExperimentReport report)
    {
        return new AnalysisResult
        {
            Spectrum = report.Spectrum,
            BinStartHz = report.BinStartHz,
            BinEndHz = report.BinEndHz,
            BinCenterHz = report.BinCenterHz,
            SampleRate = report.SampleRate,
            InputSampleCount = report.InputSampleCount,
            PassesPerformed = report.PassesPerformed,
            OscillationsDetected = report.OscillationsDetected
        };
    }
}
