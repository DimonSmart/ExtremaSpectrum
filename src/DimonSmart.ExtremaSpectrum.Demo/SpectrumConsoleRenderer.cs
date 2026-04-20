using Spectre.Console;
using System.Globalization;
using System.Text;

namespace DimonSmart.ExtremaSpectrum.Demo;

internal static class SpectrumConsoleRenderer
{
    internal static string BuildChartMarkup(
        AnalysisResult result,
        int chartHeight,
        float overallScale = 1f,
        int fromBin = 0,
        int toBin = -1)
    {
        const string filledCell = "██  ";
        const string emptyCell = "    ";
        const string baselineCell = "─── ";

        if (toBin < 0) toBin = result.Spectrum.Length - 1;
        fromBin = Math.Clamp(fromBin, 0, result.Spectrum.Length - 1);
        toBin = Math.Clamp(toBin, fromBin, result.Spectrum.Length - 1);

        var heights = ComputeHeights(result.Spectrum, chartHeight, overallScale, fromBin, toBin);
        var builder = new StringBuilder();

        for (var row = chartHeight; row >= 1; row--)
        {
            for (var i = fromBin; i <= toBin; i++)
            {
                if (heights[i] >= row)
                {
                    builder.Append('[')
                        .Append(GetColor(row, chartHeight))
                        .Append(']')
                        .Append(filledCell)
                        .Append("[/]");
                }
                else
                {
                    builder.Append(emptyCell);
                }
            }

            builder.AppendLine();
        }

        for (var i = fromBin; i <= toBin; i++)
            builder.Append("[grey]").Append(baselineCell).Append("[/]");
        builder.AppendLine();

        for (var i = fromBin; i <= toBin; i++)
        {
            builder.Append("[grey]")
                .Append(FormatBucketLabel(result.BinCenterHz[i]))
                .Append("[/]");
        }

        builder.AppendLine();
        builder.AppendLine("[grey]kHz (bin centers)[/]");

        return builder.ToString();
    }

    internal static string BuildPeakSummary(AnalysisResult result, int count, int fromBin = 0, int toBin = -1)
    {
        if (toBin < 0) toBin = result.Spectrum.Length - 1;
        fromBin = Math.Clamp(fromBin, 0, result.Spectrum.Length - 1);
        toBin = Math.Clamp(toBin, fromBin, result.Spectrum.Length - 1);

        var topBins = Enumerable.Range(fromBin, toBin - fromBin + 1)
            .OrderByDescending(index => result.Spectrum[index])
            .ThenBy(index => index)
            .Take(count)
            .Where(index => result.Spectrum[index] > 0f)
            .ToArray();

        if (topBins.Length == 0)
            return "no contributions in the selected range";

        return string.Join(", ", topBins.Select(index =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{result.BinStartHz[index] / 1000f:0.0}-{result.BinEndHz[index] / 1000f:0.0} kHz")));
    }

    internal static IReadOnlyList<string> BuildPassDiagnostics(ExtremaAnalysisReport report)
    {
        var lines = new List<string>(report.PassesPerformed + 1)
        {
            $"Pass diagnostics: total={report.TotalContribution.ToString("0.###", CultureInfo.InvariantCulture)}, avg/osc={(report.OscillationsDetected > 0 ? (report.TotalContribution / report.OscillationsDetected).ToString("0.######", CultureInfo.InvariantCulture) : "0")}"
        };

        for (var passIndex = 0; passIndex < report.PassSpectra.Count; passIndex++)
        {
            var passSpectrum = report.PassSpectra[passIndex];
            var passContribution = passSpectrum.Sum();
            var oscillationCount = report.OscillationsPerPass[passIndex];
            var averageContribution = oscillationCount > 0 ? passContribution / oscillationCount : 0f;

            var topBinIndex = 0;
            for (var binIndex = 1; binIndex < passSpectrum.Length; binIndex++)
            {
                if (passSpectrum[binIndex] > passSpectrum[topBinIndex])
                    topBinIndex = binIndex;
            }

            var topBinContribution = passSpectrum[topBinIndex];
            var topBinShare = passContribution > 0f
                ? topBinContribution / passContribution * 100f
                : 0f;

            var activeBins = 0;
            for (var binIndex = 0; binIndex < passSpectrum.Length; binIndex++)
            {
                if (passSpectrum[binIndex] > 0f)
                    activeBins++;
            }

            lines.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  pass {passIndex + 1}: osc={oscillationCount}, total={passContribution:0.###}, avg/osc={averageContribution:0.######}, top={report.BinStartHz[topBinIndex] / 1000f:0.0}-{report.BinEndHz[topBinIndex] / 1000f:0.0} kHz ({topBinShare:0.0}%), activeBins={activeBins}"));
        }

        return lines;
    }

    public static void Render(
        SegmentedSpectrumOptions options,
        WaveFile waveFile,
        IReadOnlyList<SpectrumSegment> segments)
    {
        AnsiConsole.Write(new Rule("[bold green]Extrema Spectrum Demo[/]"));

        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[grey]Parameter[/]")
            .AddColumn("[grey]Value[/]");

        summary.AddRow("File", Markup.Escape(options.InputPath));
        summary.AddRow("Format", $"{waveFile.SampleRate} Hz, {waveFile.Channels} ch, {waveFile.BitsPerSample} bit");
        summary.AddRow("Duration", $"{waveFile.Duration.TotalSeconds:F3} s");
        summary.AddRow("Algorithm", "Extrema oscillation decomposition");
        summary.AddRow("Accumulation", AccumulationModeCli.ToDisplayName(options.AccumulationMode));
        summary.AddRow("Window / overlap", $"{options.WindowSeconds:F1} s / {options.OverlapSeconds:F1} s");
        summary.AddRow("Analysis step", $"{options.HopSeconds:F1} s");
        summary.AddRow("Min amplitude", options.MinAmplitude.ToString("0.####", CultureInfo.InvariantCulture));
        summary.AddRow("Bins", options.BinCount.ToString(CultureInfo.InvariantCulture));
        summary.AddRow("Range", $"{options.MinFrequencyHz / 1000f:F1} - {waveFile.NyquistHz / 1000f:F1} kHz");

        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        foreach (var segment in segments)
        {
            var start = segment.StartTime(waveFile.SampleRate);
            var end = segment.EndTime(waveFile.SampleRate);
            var actualDuration = end - start;

            AnsiConsole.Write(new Rule(
                $"[yellow]Window {segment.Index + 1}[/] [grey]{FormatTimestamp(start)} - {FormatTimestamp(end)}[/]"));

            AnsiConsole.MarkupLine(
                $"[grey]Length:[/] {actualDuration.TotalSeconds:F3} s   " +
                $"[grey]Samples:[/] {segment.SampleCount}   " +
                $"[grey]Passes:[/] {segment.Result.PassesPerformed}   " +
                $"[grey]Oscillations:[/] {segment.Result.OscillationsDetected}");

            var effectiveToBin = options.EffectiveToBin(segment.Result.Spectrum.Length);
            var chartMarkup = BuildChartMarkup(segment.Result, options.ChartHeight, 1f, options.FromBin, effectiveToBin);
            AnsiConsole.Write(new Markup(chartMarkup));

            var peaks = BuildPeakSummary(segment.Result, count: 3, options.FromBin, effectiveToBin);
            AnsiConsole.MarkupLine($"[grey]Peaks:[/] {Markup.Escape(peaks)}");

            if (options.DumpPasses && segment.DetailedReport is not null)
            {
                foreach (var line in BuildPassDiagnostics(segment.DetailedReport))
                    AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
            }

            AnsiConsole.WriteLine();
        }
    }

    internal static int[] ComputeHeights(float[] spectrum, int chartHeight, float overallScale = 1f, int fromBin = 0, int toBin = -1)
    {
        if (toBin < 0) toBin = spectrum.Length - 1;

        var heights = new int[spectrum.Length];
        var clampedScale = Math.Clamp(overallScale, 0f, 1f);
        if (clampedScale <= 0f)
            return heights;

        var maxValue = 0f;
        for (var i = fromBin; i <= toBin; i++)
        {
            if (spectrum[i] > maxValue)
                maxValue = spectrum[i];
        }

        if (maxValue <= 0f)
            return heights;

        for (var i = fromBin; i <= toBin; i++)
        {
            var normalized = spectrum[i] / maxValue;
            var perceptual = MathF.Sqrt(normalized);
            heights[i] = (int)MathF.Round(chartHeight * clampedScale * perceptual, MidpointRounding.AwayFromZero);
        }

        return heights;
    }

    private static string GetColor(int row, int chartHeight)
    {
        var ratio = (float)row / chartHeight;
        if (ratio >= 0.8f)
            return "#E74C3C";
        if (ratio >= 0.5f)
            return "#F1C40F";
        return "#2ECC71";
    }

    private static string FormatBucketLabel(float centerHz)
    {
        var kiloHertz = centerHz / 1000f;
        return kiloHertz.ToString("0.0", CultureInfo.InvariantCulture).PadRight(4);
    }

    private static string FormatTimestamp(TimeSpan value)
        => value.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);
}
