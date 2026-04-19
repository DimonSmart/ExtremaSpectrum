namespace ExtremaSpectrum;

internal static class ExtremaEngine
{
    private readonly record struct ActiveSegment(int Start, int End)
    {
        public int Length => End - Start + 1;
    }

    private readonly record struct ComputedContribution(float FrequencyHz, int? BinIndex, float Value)
    {
        public bool ContributesToSpectrum => BinIndex is not null;
    }

    private readonly record struct SmoothingRange(int Start, int End, float StartValue, float EndValue);

    internal static (int passes, int oscillations) Run(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions options,
        float[] spectrum)
    {
        var execution = Execute(samples, sampleRate, options, captureDetails: false);
        execution.TotalSpectrum.AsSpan().CopyTo(spectrum);
        return (execution.PassesPerformed, execution.OscillationsDetected);
    }

    internal static ExtremaEngineExecutionResult Execute(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions options,
        bool captureDetails)
    {
        var sampleCount = samples.Length;
        var workSamples = samples.ToArray();
        var totalSpectrum = new float[options.BinCount];
        var passSpectra = captureDetails ? new List<float[]>() : [];
        var oscillationsPerPass = captureDetails ? new List<int>() : [];
        var passes = captureDetails ? new List<ExtremaPassSnapshot>() : [];

        if (sampleCount < 3)
        {
            return new ExtremaEngineExecutionResult
            {
                TotalSpectrum = totalSpectrum,
                PassSpectra = passSpectra,
                OscillationsPerPass = oscillationsPerPass,
                Passes = passes,
                PassesPerformed = 0,
                OscillationsDetected = 0
            };
        }

        var effectiveMaxPeriod = options.MaxPeriodSamples > 0
            ? options.MaxPeriodSamples
            : sampleCount;

        var totalOscillations = 0;
        var passesPerformed = 0;
        var activeSegments = new List<ActiveSegment> { new(0, sampleCount - 1) };
        var extremaIndices = new int[sampleCount];
        var extremaKinds = new bool[sampleCount];

        for (var passIndex = 0; passIndex < options.MaxPasses && activeSegments.Count > 0; passIndex++)
        {
            var passSpectrum = new float[options.BinCount];
            var nextSegments = new List<ActiveSegment>();
            var smoothingRanges = new List<SmoothingRange>();
            var passOscillations = captureDetails ? new List<ExtremaOscillationTrace>() : null;
            var sourceSegments = captureDetails ? CopySegments(activeSegments) : [];
            var waveformBeforePass = captureDetails ? (float[])workSamples.Clone() : null;
            var foundOscillations = 0;

            foreach (var segment in activeSegments)
            {
                if (segment.Length < 3)
                    continue;

                var extremaCount = FindExtrema(workSamples, segment.Start, segment.End, extremaIndices, extremaKinds);
                if (extremaCount < 3)
                    continue;

                foundOscillations += ProcessSegment(
                    workSamples,
                    sampleRate,
                    options,
                    effectiveMaxPeriod,
                    segment,
                    extremaIndices,
                    extremaKinds,
                    extremaCount,
                    passSpectrum,
                    passOscillations,
                    nextSegments,
                    smoothingRanges);
            }

            if (foundOscillations == 0)
                break;

            ApplySmoothingRanges(workSamples, smoothingRanges);
            AddSpectrum(totalSpectrum, passSpectrum);
            totalOscillations += foundOscillations;
            passesPerformed++;

            if (captureDetails)
            {
                passSpectra.Add(passSpectrum);
                oscillationsPerPass.Add(foundOscillations);
                passes.Add(new ExtremaPassSnapshot
                {
                    PassIndex = passIndex,
                    SourceSegments = sourceSegments,
                    RemainingSegments = CopySegments(nextSegments),
                    WaveformBeforePass = waveformBeforePass!,
                    WaveformAfterPass = (float[])workSamples.Clone(),
                    Oscillations = passOscillations!,
                    SpectrumContribution = passSpectrum
                });
            }

            activeSegments = [new ActiveSegment(0, sampleCount - 1)];
        }

        return new ExtremaEngineExecutionResult
        {
            TotalSpectrum = totalSpectrum,
            PassSpectra = passSpectra,
            OscillationsPerPass = oscillationsPerPass,
            Passes = passes,
            PassesPerformed = passesPerformed,
            OscillationsDetected = totalOscillations
        };
    }

    private static int ProcessSegment(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions options,
        int effectiveMaxPeriod,
        ActiveSegment segment,
        int[] extremaIndices,
        bool[] extremaKinds,
        int extremaCount,
        float[] spectrum,
        List<ExtremaOscillationTrace>? passOscillations,
        List<ActiveSegment> nextSegments,
        List<SmoothingRange> smoothingRanges)
    {
        var foundOscillations = 0;
        var remainderStart = segment.Start;
        var extremaCursor = 0;
        var acceptedAny = false;
        var previousAcceptedCursor = -2;
        var previousRightBoundary = -1;

        while (extremaCursor + 2 < extremaCount)
        {
            var leftIndex = extremaIndices[extremaCursor];
            var midIndex = extremaIndices[extremaCursor + 1];
            var rightIndex = extremaIndices[extremaCursor + 2];

            var leftIsMin = !extremaKinds[extremaCursor];
            var midIsMax = extremaKinds[extremaCursor + 1];
            var rightIsMin = !extremaKinds[extremaCursor + 2];

            var isValidTriple =
                (leftIsMin && midIsMax && rightIsMin) ||
                (!leftIsMin && !midIsMax && !rightIsMin);

            if (!isValidTriple)
            {
                extremaCursor++;
                continue;
            }

            var periodSamples = rightIndex - leftIndex;
            if (periodSamples < options.MinPeriodSamples || periodSamples > effectiveMaxPeriod)
            {
                extremaCursor++;
                continue;
            }

            var baseline = (samples[leftIndex] + samples[rightIndex]) * 0.5f;
            var amplitude = MathF.Abs(samples[midIndex] - baseline);
            if (amplitude < options.MinAmplitude)
            {
                extremaCursor++;
                continue;
            }

            acceptedAny = true;

            var computedContribution = ComputeContribution(sampleRate, options, periodSamples, amplitude);
            if (computedContribution.BinIndex is int binIndex)
            {
                spectrum[binIndex] += computedContribution.Value;
                foundOscillations++;
            }

            var leftBoundary = extremaCursor == previousAcceptedCursor + 1
                ? previousRightBoundary
                : GetLeftBoundarySample(segment.Start, extremaCursor, leftIndex, midIndex);
            var rightBoundary = GetRightBoundarySample(segment.End, extremaCursor, extremaCount, midIndex, rightIndex);

            passOscillations?.Add(new ExtremaOscillationTrace(
                leftIndex,
                midIndex,
                rightIndex,
                computedContribution.FrequencyHz,
                amplitude,
                computedContribution.BinIndex,
                computedContribution.BinIndex is null ? 0f : computedContribution.Value,
                leftBoundary,
                rightBoundary));

            TryAddSegment(nextSegments, remainderStart, leftBoundary);
            smoothingRanges.Add(new SmoothingRange(
                leftBoundary,
                rightBoundary,
                samples[leftBoundary],
                samples[rightBoundary]));
            remainderStart = rightBoundary;
            previousAcceptedCursor = extremaCursor;
            previousRightBoundary = rightBoundary;
            extremaCursor++;
        }

        if (acceptedAny)
            TryAddSegment(nextSegments, remainderStart, segment.End);

        return foundOscillations;
    }

    private static int GetLeftBoundarySample(int segmentStart, int extremaCursor, int leftIndex, int midIndex)
        => extremaCursor == 0
            ? segmentStart
            : leftIndex + ((midIndex - leftIndex) / 2);

    private static int GetRightBoundarySample(int segmentEnd, int extremaCursor, int extremaCount, int midIndex, int rightIndex)
        => extremaCursor + 2 == extremaCount - 1
            ? segmentEnd
            : midIndex + ((rightIndex - midIndex + 1) / 2);

    private static void ApplySmoothingRanges(float[] samples, IReadOnlyList<SmoothingRange> smoothingRanges)
    {
        foreach (var smoothingRange in smoothingRanges)
        {
            var width = smoothingRange.End - smoothingRange.Start;
            if (width < 1)
                continue;

            for (var offset = 1; offset < width; offset++)
            {
                var t = offset / (float)width;
                samples[smoothingRange.Start + offset] =
                    smoothingRange.StartValue + ((smoothingRange.EndValue - smoothingRange.StartValue) * t);
            }
        }
    }

    private static int FindExtrema(
        ReadOnlySpan<float> samples,
        int start,
        int end,
        int[] extremaIndices,
        bool[] extremaKinds)
    {
        var count = 0;

        for (var sampleIndex = start + 1; sampleIndex < end; sampleIndex++)
        {
            var previous = samples[sampleIndex - 1];
            var current = samples[sampleIndex];
            var next = samples[sampleIndex + 1];

            if (previous < current && current >= next)
            {
                extremaIndices[count] = sampleIndex;
                extremaKinds[count] = true;
                count++;
            }
            else if (previous > current && current <= next)
            {
                extremaIndices[count] = sampleIndex;
                extremaKinds[count] = false;
                count++;
            }
        }

        return count;
    }

    private static ComputedContribution ComputeContribution(
        int sampleRate,
        ExtremaSpectrumOptions options,
        int periodSamples,
        float amplitude)
    {
        var frequencyHz = (float)sampleRate / periodSamples;
        var binWidth = (options.MaxFrequencyHz - options.MinFrequencyHz) / options.BinCount;
        var binIndex = (int)MathF.Floor((frequencyHz - options.MinFrequencyHz) / binWidth);
        if (binIndex < 0 || binIndex >= options.BinCount)
            return new ComputedContribution(frequencyHz, BinIndex: null, Value: 0f);

        var contribution = options.AccumulationMode == AccumulationMode.Energy
            ? amplitude * amplitude
            : amplitude;

        return new ComputedContribution(frequencyHz, binIndex, contribution);
    }

    private static void AddSpectrum(float[] target, float[] source)
    {
        for (var i = 0; i < target.Length; i++)
            target[i] += source[i];
    }

    private static ExtremaSegmentRange[] CopySegments(IReadOnlyList<ActiveSegment> segments)
    {
        var copy = new ExtremaSegmentRange[segments.Count];
        for (var i = 0; i < segments.Count; i++)
            copy[i] = new ExtremaSegmentRange(segments[i].Start, segments[i].End);

        return copy;
    }

    private static void TryAddSegment(List<ActiveSegment> segments, int start, int end)
    {
        if (end >= start)
            segments.Add(new ActiveSegment(start, end));
    }
}

internal sealed class ExtremaEngineExecutionResult
{
    public required float[] TotalSpectrum { get; init; }

    public required IReadOnlyList<float[]> PassSpectra { get; init; }

    public required IReadOnlyList<int> OscillationsPerPass { get; init; }

    public required IReadOnlyList<ExtremaPassSnapshot> Passes { get; init; }

    public required int PassesPerformed { get; init; }

    public required int OscillationsDetected { get; init; }
}
