namespace ExtremaSpectrum;

/// <summary>
/// Internal engine that executes the extrema-decomposition algorithm.
/// </summary>
internal static class ExtremaEngine
{
    private readonly record struct ActiveSegment(int Start, int End)
    {
        public int Length => End - Start + 1;
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the full multi-pass decomposition on <paramref name="samples"/> and
    /// fills <paramref name="spectrum"/> with accumulated contributions.
    /// </summary>
    /// <returns>
    /// A tuple of (passesPerformed, oscillationsDetected).
    /// </returns>
    internal static (int passes, int oscillations) Run(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        float[] spectrum,
        float[] binStartHz,
        float[] binEndHz)
    {
        var execution = Execute(
            samples,
            sampleRate,
            opts,
            ExtremaExperimentVariant.Baseline,
            capturePassSpectra: false);

        execution.TotalSpectrum.AsSpan().CopyTo(spectrum);
        return (execution.PassesPerformed, execution.OscillationsDetected);
    }

    internal static ExtremaEngineExecutionResult Execute(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        ExtremaExperimentVariant variant,
        bool capturePassSpectra)
    {
        return variant switch
        {
            ExtremaExperimentVariant.Baseline => ExecuteBaseline(samples, sampleRate, opts, capturePassSpectra),
            ExtremaExperimentVariant.HardGapRaw or ExtremaExperimentVariant.HardGapPeriodNormalized
                => ExecuteHardGap(samples, sampleRate, opts, variant, capturePassSpectra),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown experiment variant.")
        };
    }

    // -----------------------------------------------------------------------
    // One pass
    // -----------------------------------------------------------------------

    private static ExtremaEngineExecutionResult ExecuteBaseline(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        bool capturePassSpectra)
    {
        var n = samples.Length;
        var totalSpectrum = new float[opts.BinCount];
        var passSpectra = capturePassSpectra ? new List<float[]>() : [];
        var oscillationsPerPass = capturePassSpectra ? new List<int>() : [];

        if (n < 3)
        {
            return new ExtremaEngineExecutionResult
            {
                TotalSpectrum = totalSpectrum,
                PassSpectra = passSpectra,
                OscillationsPerPass = oscillationsPerPass,
                PassesPerformed = 0,
                OscillationsDetected = 0
            };
        }

        var work = new float[n];
        samples.CopyTo(work);

        var effectiveMaxPeriod = opts.MaxPeriodSamples > 0
            ? opts.MaxPeriodSamples
            : n;

        var totalOscillations = 0;
        var passesPerformed = 0;
        var extremaIdx = new int[n];
        var extremaIsMax = new bool[n];

        for (var pass = 0; pass < opts.MaxPasses; pass++)
        {
            var extremaCount = FindExtrema(work, 0, n - 1, extremaIdx, extremaIsMax);
            if (extremaCount < 3)
                break;

            var passSpectrum = new float[opts.BinCount];
            var found = ProcessBaselinePass(
                work,
                sampleRate,
                opts,
                effectiveMaxPeriod,
                extremaIdx,
                extremaIsMax,
                extremaCount,
                passSpectrum,
                n);

            if (found == 0)
                break;

            AddSpectrum(totalSpectrum, passSpectrum);
            totalOscillations += found;
            passesPerformed++;
            if (capturePassSpectra)
            {
                passSpectra.Add(passSpectrum);
                oscillationsPerPass.Add(found);
            }
        }

        return new ExtremaEngineExecutionResult
        {
            TotalSpectrum = totalSpectrum,
            PassSpectra = passSpectra,
            OscillationsPerPass = oscillationsPerPass,
            PassesPerformed = passesPerformed,
            OscillationsDetected = totalOscillations
        };
    }

    private static ExtremaEngineExecutionResult ExecuteHardGap(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        ExtremaExperimentVariant variant,
        bool capturePassSpectra)
    {
        var n = samples.Length;
        var totalSpectrum = new float[opts.BinCount];
        var passSpectra = capturePassSpectra ? new List<float[]>() : [];
        var oscillationsPerPass = capturePassSpectra ? new List<int>() : [];

        if (n < 3)
        {
            return new ExtremaEngineExecutionResult
            {
                TotalSpectrum = totalSpectrum,
                PassSpectra = passSpectra,
                OscillationsPerPass = oscillationsPerPass,
                PassesPerformed = 0,
                OscillationsDetected = 0
            };
        }

        var effectiveMaxPeriod = opts.MaxPeriodSamples > 0
            ? opts.MaxPeriodSamples
            : n;

        var totalOscillations = 0;
        var passesPerformed = 0;
        var activeSegments = new List<ActiveSegment> { new(0, n - 1) };
        var extremaIdx = new int[n];
        var extremaIsMax = new bool[n];

        for (var pass = 0; pass < opts.MaxPasses && activeSegments.Count > 0; pass++)
        {
            var passSpectrum = new float[opts.BinCount];
            var nextSegments = new List<ActiveSegment>();
            var found = 0;

            foreach (var segment in activeSegments)
            {
                if (segment.Length < 3)
                    continue;

                var extremaCount = FindExtrema(samples, segment.Start, segment.End, extremaIdx, extremaIsMax);
                if (extremaCount < 3)
                    continue;

                found += ProcessHardGapSegment(
                    samples,
                    sampleRate,
                    opts,
                    variant,
                    n,
                    effectiveMaxPeriod,
                    segment,
                    extremaIdx,
                    extremaIsMax,
                    extremaCount,
                    passSpectrum,
                    nextSegments);
            }

            if (found == 0)
                break;

            AddSpectrum(totalSpectrum, passSpectrum);
            totalOscillations += found;
            passesPerformed++;
            if (capturePassSpectra)
            {
                passSpectra.Add(passSpectrum);
                oscillationsPerPass.Add(found);
            }

            activeSegments = nextSegments;
        }

        return new ExtremaEngineExecutionResult
        {
            TotalSpectrum = totalSpectrum,
            PassSpectra = passSpectra,
            OscillationsPerPass = oscillationsPerPass,
            PassesPerformed = passesPerformed,
            OscillationsDetected = totalOscillations
        };
    }

    private static int ProcessBaselinePass(
        float[] work,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        int effectiveMaxPeriod,
        int[] extremaIdx,
        bool[] extremaIsMax,
        int extremaCount,
        float[] spectrum,
        int inputSampleCount)
    {
        var found = 0;
        var ei = 0; // extrema cursor

        while (ei + 2 < extremaCount)
        {
            var li = extremaIdx[ei];
            var mi = extremaIdx[ei + 1];
            var ri = extremaIdx[ei + 2];

            var leftIsMin = !extremaIsMax[ei];
            var midIsMax  =  extremaIsMax[ei + 1];
            var rightIsMin = !extremaIsMax[ei + 2];

            var isValidTriple =
                (leftIsMin && midIsMax && rightIsMin) ||   // min-max-min
                (!leftIsMin && !midIsMax && !rightIsMin);  // max-min-max

            if (!isValidTriple)
            {
                ei++;
                continue;
            }

            var periodSamples = ri - li;

            // Period filter
            if (periodSamples < opts.MinPeriodSamples || periodSamples > effectiveMaxPeriod)
            {
                ei++;
                continue;
            }

            var baseline = (work[li] + work[ri]) * 0.5f;
            var amplitude = MathF.Abs(work[mi] - baseline);

            // Amplitude filter
            if (amplitude < opts.MinAmplitude)
            {
                ei++;
                continue;
            }

            var freqHz = (float)sampleRate / periodSamples;

            if (TryAccumulateContribution(
                spectrum,
                sampleRate,
                opts,
                ExtremaExperimentVariant.Baseline,
                inputSampleCount,
                periodSamples,
                amplitude))
            {
                found++;
            }

            Linearize(work, li, ri);

            var nextEi = ei + 2;
            while (nextEi < extremaCount && extremaIdx[nextEi] <= ri)
                nextEi++;
            ei = nextEi;
        }

        return found;
    }

    private static int ProcessHardGapSegment(
        ReadOnlySpan<float> samples,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        ExtremaExperimentVariant variant,
        int inputSampleCount,
        int effectiveMaxPeriod,
        ActiveSegment segment,
        int[] extremaIdx,
        bool[] extremaIsMax,
        int extremaCount,
        float[] spectrum,
        List<ActiveSegment> nextSegments)
    {
        var found = 0;
        var remainderStart = segment.Start;
        var ei = 0;
        var acceptedAny = false;

        while (ei + 2 < extremaCount)
        {
            var li = extremaIdx[ei];
            var mi = extremaIdx[ei + 1];
            var ri = extremaIdx[ei + 2];

            var leftIsMin = !extremaIsMax[ei];
            var midIsMax = extremaIsMax[ei + 1];
            var rightIsMin = !extremaIsMax[ei + 2];

            var isValidTriple =
                (leftIsMin && midIsMax && rightIsMin) ||
                (!leftIsMin && !midIsMax && !rightIsMin);

            if (!isValidTriple)
            {
                ei++;
                continue;
            }

            var periodSamples = ri - li;
            if (periodSamples < opts.MinPeriodSamples || periodSamples > effectiveMaxPeriod)
            {
                ei++;
                continue;
            }

            var baseline = (samples[li] + samples[ri]) * 0.5f;
            var amplitude = MathF.Abs(samples[mi] - baseline);

            if (amplitude < opts.MinAmplitude)
            {
                ei++;
                continue;
            }

            acceptedAny = true;

            if (TryAccumulateContribution(
                spectrum,
                sampleRate,
                opts,
                variant,
                inputSampleCount,
                periodSamples,
                amplitude))
            {
                found++;
            }

            TryAddSegment(nextSegments, remainderStart, li);
            remainderStart = ri;

            var nextEi = ei + 2;
            while (nextEi < extremaCount && extremaIdx[nextEi] <= ri)
                nextEi++;
            ei = nextEi;
        }

        if (acceptedAny)
            TryAddSegment(nextSegments, remainderStart, segment.End);

        return found;
    }

    // -----------------------------------------------------------------------
    // Extrema detection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds local extrema in <paramref name="work"/> using strict discrete comparison.
    /// Boundary points (index 0 and Length-1) are never considered extrema.
    /// Flat plateaus are handled by choosing the first point of the plateau that
    /// satisfies the comparison — this is deterministic and documented behaviour.
    /// </summary>
    /// <returns>Number of extrema written.</returns>
    private static int FindExtrema(
        ReadOnlySpan<float> work,
        int start,
        int end,
        int[] extremaIdx,
        bool[] extremaIsMax)
    {
        var count = 0;

        for (var i = start + 1; i < end; i++)
        {
            var prev = work[i - 1];
            var cur  = work[i];
            var next = work[i + 1];

            if (prev < cur && cur >= next)
            {
                extremaIdx[count]   = i;
                extremaIsMax[count] = true;
                count++;
            }
            else if (prev > cur && cur <= next)
            {
                extremaIdx[count]   = i;
                extremaIsMax[count] = false;
                count++;
            }
        }

        return count;
    }

    private static bool TryAccumulateContribution(
        float[] spectrum,
        int sampleRate,
        ExtremaSpectrumOptions opts,
        ExtremaExperimentVariant variant,
        int inputSampleCount,
        int periodSamples,
        float amplitude)
    {
        var freqHz = (float)sampleRate / periodSamples;
        var binWidth = (opts.MaxFrequencyHz - opts.MinFrequencyHz) / opts.BinCount;
        var binIndex = (int)MathF.Floor((freqHz - opts.MinFrequencyHz) / binWidth);
        if (binIndex < 0 || binIndex >= opts.BinCount)
            return false;

        var contribution = opts.AccumulationMode == AccumulationMode.Energy
            ? amplitude * amplitude
            : amplitude;

        if (variant == ExtremaExperimentVariant.HardGapPeriodNormalized)
            contribution *= (float)periodSamples / inputSampleCount;

        spectrum[binIndex] += contribution;
        return true;
    }

    private static void AddSpectrum(float[] target, float[] source)
    {
        for (var i = 0; i < target.Length; i++)
            target[i] += source[i];
    }

    private static void TryAddSegment(List<ActiveSegment> segments, int start, int end)
    {
        if (end - start >= 2)
            segments.Add(new ActiveSegment(start, end));
    }

    // -----------------------------------------------------------------------
    // Linear smoothing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Replaces all values in <paramref name="work"/> between
    /// <paramref name="left"/> and <paramref name="right"/> (inclusive endpoints)
    /// with a linear interpolation from <c>work[left]</c> to <c>work[right]</c>.
    /// </summary>
    private static void Linearize(float[] work, int left, int right)
    {
        var vLeft  = work[left];
        var vRight = work[right];
        var   span   = right - left;

        // Endpoints are not touched (they keep their values for the next pass).
        for (var i = left + 1; i < right; i++)
        {
            var t = (float)(i - left) / span;
            work[i] = vLeft + t * (vRight - vLeft);
        }
    }
}

internal sealed class ExtremaEngineExecutionResult
{
    public required float[] TotalSpectrum { get; init; }

    public required IReadOnlyList<float[]> PassSpectra { get; init; }

    public required IReadOnlyList<int> OscillationsPerPass { get; init; }

    public required int PassesPerformed { get; init; }

    public required int OscillationsDetected { get; init; }
}
