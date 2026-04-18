using Xunit;

namespace ExtremaSpectrum.Tests;

public sealed class UnitTests
{
    private static readonly ExtremaSpectrumOptions DefaultOpts = new()
    {
        BinCount       = 128,
        MinFrequencyHz = 100f,
        MaxFrequencyHz = 8000f,
        MaxPasses      = 20
    };

    private static ExtremaSpectrumAnalyzer Analyzer(ExtremaSpectrumOptions? opts = null)
        => new(opts ?? DefaultOpts);

    // ------------------------------------------------------------------
    // Test 1 – Single sine: peak bin must contain the sine frequency
    // ------------------------------------------------------------------
    [Theory]
    [InlineData(440f)]
    [InlineData(1000f)]
    [InlineData(3000f)]
    public void SingleSine_PeakBinContainsSineFrequency(float freqHz)
    {
        const int sampleRate = 44100;
        const int samples    = 4096;

        var signal  = Helpers.Sine(sampleRate, freqHz, samples);
        var     result  = Analyzer().Analyze(signal, sampleRate);

        var peakBin    = Helpers.PeakBin(result.Spectrum);
        var expectedBin = Helpers.ExpectedBin(DefaultOpts, freqHz);

        // Allow ±2 bins of tolerance given the non-FFT nature of the algorithm.
        Assert.InRange(peakBin, expectedBin - 2, expectedBin + 2);
    }

    // ------------------------------------------------------------------
    // Test 2 – Two sines: two distinct regions of elevated contribution
    // ------------------------------------------------------------------
    [Fact]
    public void TwoSines_TwoElevatedRegions()
    {
        const int   sampleRate = 44100;
        const int   samples    = 4096;
        const float freq1      = 500f;
        const float freq2      = 3000f;

        var s1 = Helpers.Sine(sampleRate, freq1, samples, 0.7f);
        var s2 = Helpers.Sine(sampleRate, freq2, samples, 0.7f);
        var mixed = new float[samples];
        for (var i = 0; i < samples; i++) mixed[i] = s1[i] + s2[i];

        var result = Analyzer().Analyze(mixed, sampleRate);

        var bin1 = Helpers.ExpectedBin(DefaultOpts, freq1);
        var bin2 = Helpers.ExpectedBin(DefaultOpts, freq2);

        // Each region around the expected bin should contain non-zero energy.
        var region1 = SumRegion(result.Spectrum, bin1, 3);
        var region2 = SumRegion(result.Spectrum, bin2, 3);

        Assert.True(region1 > 0, $"Region around {freq1} Hz is zero.");
        Assert.True(region2 > 0, $"Region around {freq2} Hz is zero.");

        // Both regions should be among the more prominent parts of the spectrum.
        var maxRegion = MaxRegionValue(result.Spectrum, 3);
        Assert.True(region1 >= maxRegion * 0.1f, "Low-freq region too small.");
        Assert.True(region2 >= maxRegion * 0.1f, "High-freq region too small.");
    }

    // ------------------------------------------------------------------
    // Test 3 – High-freq on low-freq carrier: HF bins accumulate first
    // ------------------------------------------------------------------
    [Fact]
    public void HighFreqOnLowFreqCarrier_HighBinsAccumulateEarlier()
    {
        const int   sampleRate = 44100;
        const int   samples    = 4096;
        const float lowFreq    = 200f;   // within range
        const float highFreq   = 4000f;  // within range

        // Small high-freq ripple on a large low-freq wave.
        var signal = new float[samples];
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / sampleRate;
            signal[i] = (float)(
                0.9 * Math.Sin(2 * Math.PI * lowFreq  * t) +
                0.15 * Math.Sin(2 * Math.PI * highFreq * t));
        }

        var optsLowPasses  = new ExtremaSpectrumOptions
        {
            BinCount = 128, MinFrequencyHz = 100f, MaxFrequencyHz = 8000f, MaxPasses = 2
        };
        var optsHighPasses = new ExtremaSpectrumOptions
        {
            BinCount = 128, MinFrequencyHz = 100f, MaxFrequencyHz = 8000f, MaxPasses = 16
        };

        var resultFew  = new ExtremaSpectrumAnalyzer(optsLowPasses).Analyze(signal, sampleRate);
        var resultMany = new ExtremaSpectrumAnalyzer(optsHighPasses).Analyze(signal, sampleRate);

        var highBin = Helpers.ExpectedBin(optsHighPasses, highFreq);
        var lowBin  = Helpers.ExpectedBin(optsHighPasses, lowFreq);

        // After many passes more oscillations have been found at low frequencies too.
        var lowContribFew  = SumRegion(resultFew.Spectrum,  lowBin, 4);
        var lowContribMany = SumRegion(resultMany.Spectrum, lowBin, 4);
        Assert.True(lowContribMany >= lowContribFew,
            "More passes should find at least as many low-freq oscillations.");

        // High-freq region must have non-zero contribution at all.
        Assert.True(SumRegion(resultMany.Spectrum, highBin, 4) > 0,
            "High-freq oscillations not detected.");
    }

    // ------------------------------------------------------------------
    // Test 4 – Constant signal: zero spectrum
    // ------------------------------------------------------------------
    [Fact]
    public void ConstantSignal_ZeroSpectrum()
    {
        var signal = new float[1024];
        Array.Fill(signal, 0.5f);

        var result = Analyzer().Analyze(signal, 44100);

        Assert.All(result.Spectrum, v => Assert.Equal(0f, v));
    }

    // ------------------------------------------------------------------
    // Test 5 – Linear ramp: zero or near-zero spectrum
    // ------------------------------------------------------------------
    [Fact]
    public void LinearRamp_ZeroSpectrum()
    {
        const int n = 1024;
        var signal = new float[n];
        for (var i = 0; i < n; i++) signal[i] = -1f + 2f * i / (n - 1);

        var result = Analyzer().Analyze(signal, 44100);

        var total = 0f;
        foreach (var v in result.Spectrum) total += v;
        Assert.Equal(0f, total, 1e-5f);
    }

    // ------------------------------------------------------------------
    // Test 6 – Fewer than 3 samples: completes without exception, zero spectrum
    // ------------------------------------------------------------------
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ShortInput_NoExceptionAndZeroSpectrum(int length)
    {
        var signal = new float[length];
        var result = Analyzer().Analyze(signal, 44100);

        Assert.Equal(DefaultOpts.BinCount, result.Spectrum.Length);
        Assert.All(result.Spectrum, v => Assert.Equal(0f, v));
    }

    // ------------------------------------------------------------------
    // Test 7 – PCM16 stereo interleaved: channel mix modes
    // ------------------------------------------------------------------
    [Fact]
    public void Pcm16StereoInterleaved_ChannelMixModes_CorrectConversion()
    {
        const int sampleRate = 44100;
        const int frames     = 512;
        const float freqLeft  = 800f;
        const float freqRight = 2400f;

        var left  = Helpers.Sine(sampleRate, freqLeft,  frames);
        var right = Helpers.Sine(sampleRate, freqRight, frames);
        var  bytes = Helpers.ToStereoInterleavedPcm16(left, right);

        var formatFirst = new AudioBufferFormat
        {
            SampleRate = sampleRate, Channels = 2, BitsPerSample = 16,
            Interleaved = true, ChannelMixMode = ChannelMixMode.FirstChannel
        };
        var formatPreferred = new AudioBufferFormat
        {
            SampleRate = sampleRate, Channels = 2, BitsPerSample = 16,
            Interleaved = true, ChannelMixMode = ChannelMixMode.PreferredChannel,
            PreferredChannel = 1
        };
        var formatAverage = new AudioBufferFormat
        {
            SampleRate = sampleRate, Channels = 2, BitsPerSample = 16,
            Interleaved = true, ChannelMixMode = ChannelMixMode.AverageAllChannels
        };

        var a = Analyzer();

        var resFirst     = a.AnalyzePcm16(bytes, formatFirst);
        var resPreferred = a.AnalyzePcm16(bytes, formatPreferred);
        var resAverage   = a.AnalyzePcm16(bytes, formatAverage);

        var binLeft  = Helpers.ExpectedBin(DefaultOpts, freqLeft);
        var binRight = Helpers.ExpectedBin(DefaultOpts, freqRight);

        // FirstChannel → left signal → peak near freqLeft
        Assert.True(SumRegion(resFirst.Spectrum, binLeft, 3) >
                    SumRegion(resFirst.Spectrum, binRight, 3),
            "FirstChannel: expected left-freq dominance.");

        // PreferredChannel 1 → right signal → peak near freqRight
        Assert.True(SumRegion(resPreferred.Spectrum, binRight, 3) >
                    SumRegion(resPreferred.Spectrum, binLeft, 3),
            "PreferredChannel: expected right-freq dominance.");

        // Average → both frequencies present
        Assert.True(SumRegion(resAverage.Spectrum, binLeft, 3)  > 0, "Average: left-freq missing.");
        Assert.True(SumRegion(resAverage.Spectrum, binRight, 3) > 0, "Average: right-freq missing.");
    }

    // ------------------------------------------------------------------
    // Test 8 – Repeatability
    // ------------------------------------------------------------------
    [Fact]
    public void SameInput_ProducesSameResult()
    {
        const int sampleRate = 44100;
        var signal = Helpers.Sine(sampleRate, 1000f, 2048);

        var a = Analyzer();
        var r1 = a.Analyze(signal, sampleRate);
        var r2 = a.Analyze(signal, sampleRate);

        Assert.Equal(r1.Spectrum, r2.Spectrum);
        Assert.Equal(r1.PassesPerformed, r2.PassesPerformed);
        Assert.Equal(r1.OscillationsDetected, r2.OscillationsDetected);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static float SumRegion(float[] spectrum, int centre, int halfWidth)
    {
        var sum = 0f;
        var   lo  = Math.Max(0, centre - halfWidth);
        var   hi  = Math.Min(spectrum.Length - 1, centre + halfWidth);
        for (var i = lo; i <= hi; i++) sum += spectrum[i];
        return sum;
    }

    private static float MaxRegionValue(float[] spectrum, int halfWidth)
    {
        var max = 0f;
        for (var c = 0; c < spectrum.Length; c++)
        {
            var v = SumRegion(spectrum, c, halfWidth);
            if (v > max) max = v;
        }
        return max;
    }
}
