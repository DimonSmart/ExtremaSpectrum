using Spectre.Console;
using System.Globalization;
using System.Text;

namespace ExtremaSpectrum.Demo;

internal static class SpectrumConsoleRenderer
{
    internal static string BuildChartMarkup(AnalysisResult result, int chartHeight, float overallScale = 1f)
    {
        const string filledCell = "██  ";
        const string emptyCell = "    ";
        const string baselineCell = "─── ";

        var heights = ComputeHeights(result.Spectrum, chartHeight, overallScale);
        var builder = new StringBuilder();

        for (var row = chartHeight; row >= 1; row--)
        {
            for (var i = 0; i < heights.Length; i++)
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

        for (var i = 0; i < result.Spectrum.Length; i++)
            builder.Append("[grey]").Append(baselineCell).Append("[/]");
        builder.AppendLine();

        for (var i = 0; i < result.BinCenterHz.Length; i++)
        {
            builder.Append("[grey]")
                .Append(FormatBucketLabel(result.BinCenterHz[i]))
                .Append("[/]");
        }

        builder.AppendLine();
        builder.AppendLine("[grey]kHz (центры бакетов)[/]");

        return builder.ToString();
    }

    internal static string BuildPeakSummary(AnalysisResult result, int count)
    {
        var topBins = Enumerable.Range(0, result.Spectrum.Length)
            .OrderByDescending(index => result.Spectrum[index])
            .ThenBy(index => index)
            .Take(count)
            .Where(index => result.Spectrum[index] > 0f)
            .ToArray();

        if (topBins.Length == 0)
            return "нет вкладов в выбранном диапазоне";

        return string.Join(", ", topBins.Select(index =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{result.BinStartHz[index] / 1000f:0.0}-{result.BinEndHz[index] / 1000f:0.0} kHz")));
    }

    public static void Render(
        SegmentedSpectrumOptions options,
        WaveFile waveFile,
        IReadOnlyList<SpectrumSegment> segments)
    {
        AnsiConsole.Write(new Rule("[bold green]Extrema Spectrum Demo[/]"));

        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[grey]Параметр[/]")
            .AddColumn("[grey]Значение[/]");

        summary.AddRow("Файл", Markup.Escape(options.InputPath));
        summary.AddRow("Формат", $"{waveFile.SampleRate} Hz, {waveFile.Channels} ch, {waveFile.BitsPerSample} bit");
        summary.AddRow("Длительность", $"{waveFile.Duration.TotalSeconds:F3} s");
        summary.AddRow("Вариант", ExperimentVariantCli.ToDisplayName(options.ExperimentVariant));
        summary.AddRow("Окно / перекрытие", $"{options.WindowSeconds:F1} s / {options.OverlapSeconds:F1} s");
        summary.AddRow("Шаг анализа", $"{options.HopSeconds:F1} s");
        summary.AddRow("Бакеты", options.BinCount.ToString(CultureInfo.InvariantCulture));
        summary.AddRow("Диапазон", $"{options.MinFrequencyHz / 1000f:F1} - {waveFile.NyquistHz / 1000f:F1} kHz");

        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        foreach (var segment in segments)
        {
            var start = segment.StartTime(waveFile.SampleRate);
            var end = segment.EndTime(waveFile.SampleRate);
            var actualDuration = end - start;

            AnsiConsole.Write(new Rule(
                $"[yellow]Окно {segment.Index + 1}[/] [grey]{FormatTimestamp(start)} - {FormatTimestamp(end)}[/]"));

            AnsiConsole.MarkupLine(
                $"[grey]Длина:[/] {actualDuration.TotalSeconds:F3} s   " +
                $"[grey]Сэмплы:[/] {segment.SampleCount}   " +
                $"[grey]Проходы:[/] {segment.Result.PassesPerformed}   " +
                $"[grey]Осцилляции:[/] {segment.Result.OscillationsDetected}");

            var chartMarkup = BuildChartMarkup(segment.Result, options.ChartHeight);
            AnsiConsole.Write(new Markup(chartMarkup));

            var peaks = BuildPeakSummary(segment.Result, count: 3);
            AnsiConsole.MarkupLine($"[grey]Пики:[/] {Markup.Escape(peaks)}");
            AnsiConsole.WriteLine();
        }
    }

    internal static int[] ComputeHeights(float[] spectrum, int chartHeight, float overallScale = 1f)
    {
        var heights = new int[spectrum.Length];
        var clampedScale = Math.Clamp(overallScale, 0f, 1f);
        if (clampedScale <= 0f)
            return heights;

        var maxValue = 0f;
        for (var i = 0; i < spectrum.Length; i++)
        {
            if (spectrum[i] > maxValue)
                maxValue = spectrum[i];
        }

        if (maxValue <= 0f)
            return heights;

        for (var i = 0; i < spectrum.Length; i++)
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
