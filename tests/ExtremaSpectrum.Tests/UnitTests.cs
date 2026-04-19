using Xunit;

namespace ExtremaSpectrum.Tests;

public sealed class UnitTests
{
    private static readonly ExtremaSpectrumOptions DefaultOpts = new()
    {
        BinCount = 128,
        MinFrequencyHz = 100f,
        MaxFrequencyHz = 8000f,
        MaxPasses = 20
    };

    private static ExtremaSpectrumAnalyzer Analyzer(ExtremaSpectrumOptions? opts = null)
        => new(opts ?? DefaultOpts);

    [Theory]
    [InlineData(440f)]
    [InlineData(1000f)]
    [InlineData(3000f)]
    public void SingleSine_PeakBinContainsSineFrequency(float freqHz)
    {
        const int sampleRate = 44100;
        const int samples = 4096;

        var signal = Helpers.Sine(sampleRate, freqHz, samples);
        var result = Analyzer().Analyze(signal, sampleRate);

        var peakBin = Helpers.PeakBin(result.Spectrum);
        var expectedBin = Helpers.ExpectedBin(DefaultOpts, freqHz);

        Assert.InRange(peakBin, expectedBin - 2, expectedBin + 2);
    }

    [Fact]
    public void TwoSines_MixedSignalKeepsExpectedRegionProminent()
    {
        const int sampleRate = 44100;
        const int samples = 4096;
        const float lowFrequencyHz = 500f;
        const float highFrequencyHz = 3000f;

        var low = Helpers.Sine(sampleRate, lowFrequencyHz, samples, 0.7f);
        var high = Helpers.Sine(sampleRate, highFrequencyHz, samples, 0.7f);
        var mixed = new float[samples];

        for (var i = 0; i < samples; i++)
            mixed[i] = low[i] + high[i];

        var result = Analyzer().Analyze(mixed, sampleRate);

        var lowBin = Helpers.ExpectedBin(DefaultOpts, lowFrequencyHz);
        var highBin = Helpers.ExpectedBin(DefaultOpts, highFrequencyHz);
        var lowRegion = SumRegion(result.Spectrum, lowBin, 3);
        var highRegion = SumRegion(result.Spectrum, highBin, 3);
        var strongestExpectedRegion = Math.Max(lowRegion, highRegion);
        var maxRegion = MaxRegionValue(result.Spectrum, 3);

        Assert.True(result.OscillationsDetected > 0, "Mixed signal produced no oscillations.");
        Assert.True(
            strongestExpectedRegion > 0f,
            $"Neither expected region was detected. low={lowRegion}, high={highRegion}.");
        Assert.True(
            strongestExpectedRegion >= maxRegion * 0.1f,
            $"Expected regions are too small. low={lowRegion}, high={highRegion}, max={maxRegion}.");
    }

    [Fact]
    public void HighFreqOnLowFreqCarrier_HighBinsAccumulateEarlier()
    {
        const int sampleRate = 44100;
        const int samples = 4096;
        const float lowFreq = 200f;
        const float highFreq = 4000f;

        var signal = new float[samples];
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / sampleRate;
            signal[i] = (float)(
                0.9 * Math.Sin(2 * Math.PI * lowFreq * t) +
                0.15 * Math.Sin(2 * Math.PI * highFreq * t));
        }

        var optsLowPasses = new ExtremaSpectrumOptions
        {
            BinCount = 128,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = 8000f,
            MaxPasses = 2
        };
        var optsHighPasses = new ExtremaSpectrumOptions
        {
            BinCount = 128,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = 8000f,
            MaxPasses = 16
        };

        var resultFew = new ExtremaSpectrumAnalyzer(optsLowPasses).Analyze(signal, sampleRate);
        var resultMany = new ExtremaSpectrumAnalyzer(optsHighPasses).Analyze(signal, sampleRate);

        var highBin = Helpers.ExpectedBin(optsHighPasses, highFreq);
        var lowBin = Helpers.ExpectedBin(optsHighPasses, lowFreq);
        var lowContribFew = SumRegion(resultFew.Spectrum, lowBin, 4);
        var lowContribMany = SumRegion(resultMany.Spectrum, lowBin, 4);

        Assert.True(
            lowContribMany >= lowContribFew,
            "More passes should find at least as many low-freq oscillations.");
        Assert.True(
            SumRegion(resultMany.Spectrum, highBin, 4) > 0,
            "High-freq oscillations not detected.");
    }

    [Fact]
    public void ConstantSignal_ZeroSpectrum()
    {
        var signal = new float[1024];
        Array.Fill(signal, 0.5f);

        var result = Analyzer().Analyze(signal, 44100);

        Assert.All(result.Spectrum, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void LinearRamp_ZeroSpectrum()
    {
        const int sampleCount = 1024;
        var signal = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
            signal[i] = -1f + 2f * i / (sampleCount - 1);

        var result = Analyzer().Analyze(signal, 44100);

        var total = 0f;
        foreach (var value in result.Spectrum)
            total += value;

        Assert.Equal(0f, total, 1e-5f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ShortInput_NoExceptionAndZeroSpectrum(int length)
    {
        var signal = new float[length];
        var result = Analyzer().Analyze(signal, 44100);

        Assert.Equal(DefaultOpts.BinCount, result.Spectrum.Length);
        Assert.All(result.Spectrum, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Pcm16StereoInterleaved_ChannelMixModes_CorrectConversion()
    {
        const int sampleRate = 44100;
        const int frames = 512;
        const float leftFrequencyHz = 800f;
        const float rightFrequencyHz = 2400f;

        var left = Helpers.Sine(sampleRate, leftFrequencyHz, frames);
        var right = Helpers.Sine(sampleRate, rightFrequencyHz, frames);
        var bytes = Helpers.ToStereoInterleavedPcm16(left, right);

        var formatFirst = new AudioBufferFormat
        {
            SampleRate = sampleRate,
            Channels = 2,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.FirstChannel
        };
        var formatPreferred = new AudioBufferFormat
        {
            SampleRate = sampleRate,
            Channels = 2,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.PreferredChannel,
            PreferredChannel = 1
        };
        var formatAverage = new AudioBufferFormat
        {
            SampleRate = sampleRate,
            Channels = 2,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.AverageAllChannels
        };

        var analyzer = Analyzer();
        var firstResult = analyzer.AnalyzePcm16(bytes, formatFirst);
        var preferredResult = analyzer.AnalyzePcm16(bytes, formatPreferred);
        var averageResult = analyzer.AnalyzePcm16(bytes, formatAverage);

        var leftBin = Helpers.ExpectedBin(DefaultOpts, leftFrequencyHz);
        var rightBin = Helpers.ExpectedBin(DefaultOpts, rightFrequencyHz);

        Assert.True(
            SumRegion(firstResult.Spectrum, leftBin, 3) >
            SumRegion(firstResult.Spectrum, rightBin, 3),
            "FirstChannel: expected left-freq dominance.");
        Assert.True(
            SumRegion(preferredResult.Spectrum, rightBin, 3) >
            SumRegion(preferredResult.Spectrum, leftBin, 3),
            "PreferredChannel: expected right-freq dominance.");

        var monoAverage = PcmConverter.ToMonoFloat(bytes, formatAverage);
        Assert.Equal(frames, monoAverage.Length);

        for (var i = 0; i < frames; i++)
        {
            var expected = (left[i] + right[i]) * 0.5f;
            Assert.InRange(monoAverage[i], expected - 0.0001f, expected + 0.0001f);
        }

        Assert.True(averageResult.OscillationsDetected > 0, "Average: analysis produced no oscillations.");
    }

    [Fact]
    public void AnalyzePcm16_PreferredChannelOutOfRange_ThrowsClearException()
    {
        var analyzer = Analyzer();
        var format = new AudioBufferFormat
        {
            SampleRate = 44100,
            Channels = 2,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.PreferredChannel,
            PreferredChannel = 2
        };

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            analyzer.AnalyzePcm16(new byte[4], format));

        Assert.Equal(nameof(AudioBufferFormat.PreferredChannel), error.ParamName);
    }

    [Fact]
    public void SameInput_ProducesSameResult()
    {
        const int sampleRate = 44100;
        var signal = Helpers.Sine(sampleRate, 1000f, 2048);

        var analyzer = Analyzer();
        var first = analyzer.Analyze(signal, sampleRate);
        var second = analyzer.Analyze(signal, sampleRate);

        Assert.Equal(first.Spectrum, second.Spectrum);
        Assert.Equal(first.PassesPerformed, second.PassesPerformed);
        Assert.Equal(first.OscillationsDetected, second.OscillationsDetected);
    }

    private static float SumRegion(float[] spectrum, int centre, int halfWidth)
    {
        var sum = 0f;
        var lowIndex = Math.Max(0, centre - halfWidth);
        var highIndex = Math.Min(spectrum.Length - 1, centre + halfWidth);

        for (var i = lowIndex; i <= highIndex; i++)
            sum += spectrum[i];

        return sum;
    }

    private static float MaxRegionValue(float[] spectrum, int halfWidth)
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
}
