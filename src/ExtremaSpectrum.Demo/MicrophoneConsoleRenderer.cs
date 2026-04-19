using Spectre.Console;
using Spectre.Console.Rendering;
using System.Globalization;

namespace ExtremaSpectrum.Demo;

internal static class MicrophoneConsoleRenderer
{
    internal static IRenderable Build(
        SegmentedSpectrumOptions options,
        string deviceName,
        LiveSpectrumFrame? frame,
        string statusText)
    {
        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[grey]Parameter[/]")
            .AddColumn("[grey]Value[/]");

        summary.AddRow("Source", "Microphone");
        summary.AddRow("Device", Markup.Escape(deviceName));
        summary.AddRow("Algorithm", "HardGapRaw");
        summary.AddRow("Sample rate", $"{options.MicrophoneSampleRate} Hz");
        summary.AddRow("Buffer", $"{options.MicrophoneBufferMilliseconds} ms");
        summary.AddRow(
            "Silence gate",
            options.MicrophoneSilenceRmsThreshold <= 0f
                ? "off"
                : options.MicrophoneSilenceRmsThreshold.ToString("0.0000", CultureInfo.InvariantCulture));
        summary.AddRow(
            "Display ref RMS",
            options.MicrophoneDisplayReferenceRms.ToString("0.0000", CultureInfo.InvariantCulture));
        summary.AddRow("Window / overlap", $"{options.WindowSeconds:F2} s / {options.OverlapSeconds:F2} s");
        summary.AddRow("Bins", options.BinCount.ToString(CultureInfo.InvariantCulture));
        summary.AddRow("Range", $"{options.MinFrequencyHz / 1000f:F1} - {options.MicrophoneSampleRate / 2000f:F1} kHz");

        var chartContent = frame is null
            ? new Markup("[grey]Waiting for microphone data...[/]\n[grey]Speak or make a sound.[/]")
            : new Markup(SpectrumConsoleRenderer.BuildChartMarkup(
                frame.Result,
                options.ChartHeight,
                Math.Clamp(frame.Rms / options.MicrophoneDisplayReferenceRms, 0f, 1f)));

        var chartPanel = new Panel(chartContent)
            .Header("[bold]Live Spectrum[/]")
            .Border(BoxBorder.Rounded);

        var footer = frame is null
            ? new Markup($"[grey]{Markup.Escape(statusText)}[/]")
            : new Markup(
                $"[grey]RMS:[/] {frame.Rms.ToString("0.000", CultureInfo.InvariantCulture)}   " +
                $"[grey]Passes:[/] {frame.Result.PassesPerformed}   " +
                $"[grey]Oscillations:[/] {frame.Result.OscillationsDetected}   " +
                $"[grey]Peaks:[/] {Markup.Escape(SpectrumConsoleRenderer.BuildPeakSummary(frame.Result, 3))}\n" +
                $"[grey]{Markup.Escape(statusText)}   Updated: {Markup.Escape(frame.CapturedAt.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))}[/]");

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(summary);
        grid.AddEmptyRow();
        grid.AddRow(chartPanel);
        grid.AddEmptyRow();
        grid.AddRow(footer);

        return new Panel(grid)
            .Header("[bold green]Extrema Spectrum Demo[/]")
            .Border(BoxBorder.Rounded);
    }
}
