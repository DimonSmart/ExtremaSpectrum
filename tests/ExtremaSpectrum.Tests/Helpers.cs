namespace ExtremaSpectrum.Tests;

internal static class Helpers
{
    /// <summary>Generates a mono sine wave.</summary>
    internal static float[] Sine(int sampleRate, float freqHz, int samples, float amplitude = 1f)
    {
        var buf = new float[samples];
        var twoPiF = 2.0 * Math.PI * freqHz / sampleRate;
        for (var i = 0; i < samples; i++)
            buf[i] = amplitude * (float)Math.Sin(twoPiF * i);
        return buf;
    }

    /// <summary>Returns the index of the bin with the highest value.</summary>
    internal static int PeakBin(float[] spectrum)
    {
        var peak = 0;
        for (var i = 1; i < spectrum.Length; i++)
            if (spectrum[i] > spectrum[peak]) peak = i;
        return peak;
    }

    /// <summary>
    /// Returns the bin index that would contain <paramref name="freqHz"/>
    /// given linear bin layout.
    /// </summary>
    internal static int ExpectedBin(ExtremaSpectrumOptions opts, float freqHz)
    {
        var binWidth = (opts.MaxFrequencyHz - opts.MinFrequencyHz) / opts.BinCount;
        return (int)MathF.Floor((freqHz - opts.MinFrequencyHz) / binWidth);
    }

    internal static float SumRegion(float[] spectrum, int centre, int halfWidth)
    {
        var sum = 0f;
        var lo = Math.Max(0, centre - halfWidth);
        var hi = Math.Min(spectrum.Length - 1, centre + halfWidth);
        for (var i = lo; i <= hi; i++)
            sum += spectrum[i];
        return sum;
    }

    internal static float MaxRegionValue(float[] spectrum, int halfWidth)
    {
        var max = 0f;
        for (var centre = 0; centre < spectrum.Length; centre++)
        {
            var value = SumRegion(spectrum, centre, halfWidth);
            if (value > max)
                max = value;
        }

        return max;
    }

    /// <summary>Encodes a mono float array as PCM-16 little-endian bytes.</summary>
    internal static byte[] ToPcm16Bytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var s = (short)Math.Clamp((int)(samples[i] * 32767f), short.MinValue, short.MaxValue);
            bytes[i * 2]     = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }

    /// <summary>Encodes a stereo interleaved PCM-16 buffer where L=left, R=right.</summary>
    internal static byte[] ToStereoInterleavedPcm16(float[] left, float[] right)
    {
        var frames = left.Length;
        var bytes = new byte[frames * 4]; // 2 channels × 2 bytes
        for (var i = 0; i < frames; i++)
        {
            var sl = (short)Math.Clamp((int)(left[i]  * 32767f), short.MinValue, short.MaxValue);
            var sr = (short)Math.Clamp((int)(right[i] * 32767f), short.MinValue, short.MaxValue);
            bytes[i * 4]     = (byte)(sl & 0xFF);
            bytes[i * 4 + 1] = (byte)((sl >> 8) & 0xFF);
            bytes[i * 4 + 2] = (byte)(sr & 0xFF);
            bytes[i * 4 + 3] = (byte)((sr >> 8) & 0xFF);
        }
        return bytes;
    }
}
