using System.Globalization;
using System.Text;

namespace ExtremaSpectrum.Demo;

internal static class WaveformStepSvgExporter
{
    private const int ImageWidth = 1600;
    private const int ImageHeight = 640;
    private const int LeftMargin = 72;
    private const int RightMargin = 24;
    private const int TopMargin = 84;
    private const int BottomMargin = 56;
    private const int MaxPolylinePointsPerRange = 4096;

    internal static IReadOnlyList<string> Export(
        string outputDirectory,
        string inputPath,
        WaveFile waveFile,
        IReadOnlyList<SpectrumSegment> segments)
    {
        Directory.CreateDirectory(outputDirectory);

        var exportedFiles = new List<string>();
        var inputStem = Path.GetFileNameWithoutExtension(inputPath);

        foreach (var segment in segments)
        {
            if (segment.DetailedReport is null)
            {
                throw new InvalidOperationException(
                    "Detailed analysis data is required to export step images.");
            }

            var segmentSamples = waveFile.Samples.AsSpan(segment.StartSample, segment.SampleCount);
            exportedFiles.AddRange(ExportSegment(
                outputDirectory,
                inputStem,
                segment.Index,
                waveFile.SampleRate,
                segmentSamples,
                segment.DetailedReport));
        }

        return exportedFiles;
    }

    internal static IReadOnlyList<string> ExportSegment(
        string outputDirectory,
        string inputStem,
        int segmentIndex,
        int sampleRate,
        ReadOnlySpan<float> samples,
        ExtremaAnalysisReport report)
    {
        Directory.CreateDirectory(outputDirectory);

        var files = new List<string>(report.PassesPerformed + 1);
        var prefix = $"{inputStem}-window-{segmentIndex + 1:00}";
        var fullRange = new[] { new ExtremaSegmentRange(0, samples.Length - 1) };
        var referenceMaxAbs = ComputeMaxAbs(samples);

        var initialPath = Path.Combine(outputDirectory, $"{prefix}-step-00-initial.svg");
        File.WriteAllText(
            initialPath,
            BuildStepSvg(
                samples,
                samples.ToArray(),
                referenceMaxAbs,
                sampleRate,
                title: "Initial waveform",
                detail: $"Window {segmentIndex + 1}, samples={samples.Length}, duration={samples.Length / (double)sampleRate:0.###} s",
                oscillations: [],
                visibleSegments: fullRange,
                removedRanges: []));
        files.Add(initialPath);

        for (var passIndex = 0; passIndex < report.Passes.Count; passIndex++)
        {
            var pass = report.Passes[passIndex];
            var removedRanges = ToRemovedRanges(pass.Oscillations);
            var path = Path.Combine(outputDirectory, $"{prefix}-step-{passIndex + 1:00}-pass-{passIndex + 1:00}.svg");

            File.WriteAllText(
                path,
                BuildStepSvg(
                    pass.WaveformBeforePass,
                    pass.WaveformAfterPass,
                    referenceMaxAbs,
                    sampleRate,
                    title: $"Pass {passIndex + 1}",
                    detail:
                        $"removed={pass.OscillationCount}, spectrumOsc={report.OscillationsPerPass[passIndex]}, sourceSegments={pass.SourceSegments.Count}, remainingSegments={pass.RemainingSegments.Count}",
                    oscillations: pass.Oscillations,
                    visibleSegments: pass.RemainingSegments,
                    removedRanges: removedRanges));

            files.Add(path);
        }

        return files;
    }

    private static string BuildStepSvg(
        ReadOnlySpan<float> inputSamples,
        ReadOnlySpan<float> outputSamples,
        float referenceMaxAbs,
        int sampleRate,
        string title,
        string detail,
        IReadOnlyList<ExtremaOscillationTrace> oscillations,
        IReadOnlyList<ExtremaSegmentRange> visibleSegments,
        IReadOnlyList<ExtremaSegmentRange> removedRanges)
    {
        var builder = new StringBuilder();
        var plotWidth = ImageWidth - LeftMargin - RightMargin;
        var plotHeight = ImageHeight - TopMargin - BottomMargin;
        var maxAbs = referenceMaxAbs > 0f ? referenceMaxAbs : 1f;

        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{ImageWidth}" height="{ImageHeight}" viewBox="0 0 {ImageWidth} {ImageHeight}">""");
        builder.AppendLine("""  <rect width="100%" height="100%" fill="#fffdf8"/>""");
        builder.AppendLine(
            $"""  <rect x="{LeftMargin}" y="{TopMargin}" width="{plotWidth}" height="{plotHeight}" rx="16" fill="#f8f5ee" stroke="#d7cfbf" stroke-width="1"/>""");

        var zeroY = ToSvgY(0f, maxAbs, plotHeight);
        builder.AppendLine(
            $"""  <line x1="{LeftMargin}" y1="{zeroY}" x2="{LeftMargin + plotWidth}" y2="{zeroY}" stroke="#c8bda9" stroke-width="1"/>""");

        AppendWaveformPolylines(
            builder,
            inputSamples,
            new[] { new ExtremaSegmentRange(0, inputSamples.Length - 1) },
            maxAbs,
            "#c6c3bd",
            1.1,
            0.8,
            strokeDashArray: "8 6");
        AppendWaveformPolylines(builder, inputSamples, removedRanges, maxAbs, "#d54b3d", 2.0, 0.95, strokeDashArray: "1 10");

        if (visibleSegments.Count > 1)
            AppendStitchedWaveform(builder, inputSamples, visibleSegments, maxAbs, "#0f3f66", 2.2, 0.95);

        AppendExtremaMarkers(builder, inputSamples, oscillations, maxAbs, "#5a4330", 2.6, 0.9);
        AppendWaveformPolylines(builder, outputSamples, new[] { new ExtremaSegmentRange(0, outputSamples.Length - 1) }, maxAbs, "#1f78c8", 2.8, 1.0);

        builder.AppendLine(
            $"""  <text x="{LeftMargin}" y="38" fill="#2c241b" font-family="Consolas, 'Courier New', monospace" font-size="28" font-weight="700">{EscapeXml(title)}</text>""");
        builder.AppendLine(
            $"""  <text x="{LeftMargin}" y="64" fill="#5e5548" font-family="Consolas, 'Courier New', monospace" font-size="16">{EscapeXml(detail)}</text>""");
        builder.AppendLine(
            $"""  <text x="{LeftMargin}" y="{ImageHeight - 18}" fill="#6f6454" font-family="Consolas, 'Courier New', monospace" font-size="14">sampleRate={sampleRate} Hz, samples={inputSamples.Length}, gray dashed=input waveform of this pass, brown dots=extrema used on input, dark blue=midpoint stitch guide, red dotted=removed on this pass, bright blue=output waveform of this pass</text>""");
        builder.AppendLine("</svg>");

        return builder.ToString();
    }

    private static float ComputeMaxAbs(ReadOnlySpan<float> samples)
    {
        var maxAbs = 0f;

        for (var i = 0; i < samples.Length; i++)
        {
            var abs = MathF.Abs(samples[i]);
            if (abs > maxAbs)
                maxAbs = abs;
        }

        return maxAbs > 0f ? maxAbs : 1f;
    }

    private static void AppendWaveformPolylines(
        StringBuilder builder,
        ReadOnlySpan<float> samples,
        IReadOnlyList<ExtremaSegmentRange> ranges,
        float maxAbs,
        string stroke,
        double strokeWidth,
        double opacity,
        string? strokeDashArray = null)
    {
        if (samples.Length < 2 || ranges.Count == 0)
            return;

        builder.Append("  <g fill=\"none\" stroke=\"")
            .Append(stroke)
            .Append("\" stroke-width=\"")
            .Append(strokeWidth.ToString("0.###", CultureInfo.InvariantCulture))
            .Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"")
            .Append(opacity.ToString("0.###", CultureInfo.InvariantCulture))
            .Append('"');

        if (!string.IsNullOrWhiteSpace(strokeDashArray))
        {
            builder.Append(" stroke-dasharray=\"")
                .Append(strokeDashArray)
                .Append('"');
        }

        builder.AppendLine(">");

        foreach (var range in ranges)
        {
            if (range.Length < 2)
                continue;

            AppendRangePolyline(builder, samples, range, maxAbs);
        }

        builder.AppendLine("  </g>");
    }

    private static void AppendStitchedWaveform(
        StringBuilder builder,
        ReadOnlySpan<float> samples,
        IReadOnlyList<ExtremaSegmentRange> visibleSegments,
        float maxAbs,
        string stroke,
        double strokeWidth,
        double opacity)
    {
        if (samples.Length < 2 || visibleSegments.Count == 0)
            return;

        var points = new StringBuilder();
        var hasPoint = false;

        foreach (var segment in visibleSegments)
        {
            if (segment.Length < 1)
                continue;

            AppendRangePoints(points, samples, segment, maxAbs, ref hasPoint);
        }

        if (!hasPoint)
            return;

        builder.Append("  <g fill=\"none\" stroke=\"")
            .Append(stroke)
            .Append("\" stroke-width=\"")
            .Append(strokeWidth.ToString("0.###", CultureInfo.InvariantCulture))
            .Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"")
            .Append(opacity.ToString("0.###", CultureInfo.InvariantCulture))
            .AppendLine("\">");

        builder.Append("    <polyline points=\"")
            .Append(points)
            .AppendLine("\"/>");

        builder.AppendLine("  </g>");
    }

    private static void AppendRangePolyline(
        StringBuilder builder,
        ReadOnlySpan<float> samples,
        ExtremaSegmentRange range,
        float maxAbs)
    {
        var points = new StringBuilder();
        var hasPoint = false;
        AppendRangePoints(points, samples, range, maxAbs, ref hasPoint);
        if (!hasPoint)
            return;

        builder.Append("    <polyline points=\"")
            .Append(points)
            .AppendLine("\"/>");
    }

    private static void AppendExtremaMarkers(
        StringBuilder builder,
        ReadOnlySpan<float> samples,
        IReadOnlyList<ExtremaOscillationTrace> oscillations,
        float maxAbs,
        string fill,
        double radius,
        double opacity)
    {
        if (samples.Length == 0 || oscillations.Count == 0)
            return;

        var sampleIndices = new HashSet<int>();
        foreach (var oscillation in oscillations)
        {
            sampleIndices.Add(oscillation.LeftSample);
            sampleIndices.Add(oscillation.MidSample);
            sampleIndices.Add(oscillation.RightSample);
        }

        var lastSampleIndex = samples.Length - 1;
        var plotWidth = ImageWidth - LeftMargin - RightMargin;
        var plotHeight = ImageHeight - TopMargin - BottomMargin;

        builder.Append("  <g fill=\"")
            .Append(fill)
            .Append("\" opacity=\"")
            .Append(opacity.ToString("0.###", CultureInfo.InvariantCulture))
            .AppendLine("\">");

        foreach (var sampleIndex in sampleIndices.OrderBy(sampleIndex => sampleIndex))
        {
            var x = LeftMargin + (double)sampleIndex * plotWidth / Math.Max(1, lastSampleIndex);
            var y = ToSvgY(samples[sampleIndex], maxAbs, plotHeight);
            builder.Append("    <circle cx=\"")
                .Append(x.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("\" cy=\"")
                .Append(y.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("\" r=\"")
                .Append(radius.ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine("\"/>");
        }

        builder.AppendLine("  </g>");
    }

    private static void AppendRangePoints(
        StringBuilder points,
        ReadOnlySpan<float> samples,
        ExtremaSegmentRange range,
        float maxAbs,
        ref bool hasPoint)
    {
        var lastSampleIndex = samples.Length - 1;
        var step = Math.Max(1, (int)Math.Ceiling(range.Length / (double)MaxPolylinePointsPerRange));
        var endIncluded = false;

        for (var sampleIndex = range.StartSample; sampleIndex <= range.EndSample; sampleIndex += step)
        {
            AppendPoint(points, samples, sampleIndex, lastSampleIndex, maxAbs, ref hasPoint);
            endIncluded = sampleIndex == range.EndSample;
        }

        if (!endIncluded)
            AppendPoint(points, samples, range.EndSample, lastSampleIndex, maxAbs, ref hasPoint);
    }

    private static void AppendPoint(
        StringBuilder points,
        ReadOnlySpan<float> samples,
        int sampleIndex,
        int lastSampleIndex,
        float maxAbs,
        ref bool hasPoint)
    {
        var plotWidth = ImageWidth - LeftMargin - RightMargin;
        var x = LeftMargin + (double)sampleIndex * plotWidth / Math.Max(1, lastSampleIndex);
        var y = ToSvgY(samples[sampleIndex], maxAbs, ImageHeight - TopMargin - BottomMargin);

        if (hasPoint)
            points.Append(' ');

        points.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{x:0.###},{y:0.###}"));

        hasPoint = true;
    }

    private static ExtremaSegmentRange[] ToRemovedRanges(IReadOnlyList<ExtremaOscillationTrace> oscillations)
    {
        var ranges = new List<ExtremaSegmentRange>(oscillations.Count);

        for (var i = 0; i < oscillations.Count; i++)
        {
            var leftBoundary = oscillations[i].LeftBoundarySample;
            var rightBoundary = oscillations[i].RightBoundarySample;
            var removedStart = leftBoundary + 1;
            var removedEnd = rightBoundary - 1;

            if (removedStart <= removedEnd)
                ranges.Add(new ExtremaSegmentRange(removedStart, removedEnd));
        }

        return ranges.ToArray();
    }

    private static double ToSvgY(float value, float maxAbs, int plotHeight)
    {
        var normalized = 0.5 - value / (2d * maxAbs);
        return TopMargin + normalized * plotHeight;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
