using ExtremaSpectrum.Demo;
using System.Globalization;

namespace ExtremaSpectrum.Tests;

public sealed class LiveSpectrumAnalyzerTests
{
    private const int SampleRate = 16000;
    private const int Seconds = 2;
    private const int SampleCount = SampleRate * Seconds;

    [Fact]
    public void PushPcm16_ProducesFrame_WithExpectedPeak()
    {
        const float frequencyHz = 1000f;
        var analyzer = CreateAnalyzer();
        var pcm = Helpers.ToPcm16Bytes(Helpers.Sine(SampleRate, frequencyHz, SampleCount, amplitude: 0.8f));
        var fired = analyzer.PushPcm16(pcm, CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);
        Assert.True(frame!.Rms > 0f);

        var peakBin = Helpers.PeakBin(frame.Result.Spectrum);
        var expectedBin = Helpers.ExpectedBin(CreateOptions(), frequencyHz);
        Assert.InRange(peakBin, Math.Max(0, expectedBin - 2), Math.Min(CreateOptions().BinCount - 1, expectedBin + 2));
    }

    [Fact]
    public void PushPcm16_ExactZeroInput_ProducesZeroSpectrum()
    {
        var analyzer = CreateAnalyzer();
        var pcm = new byte[SampleCount * sizeof(short)];

        var fired = analyzer.PushPcm16(pcm, CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);
        Assert.Equal(0f, frame!.Rms);
        Assert.Equal(0, frame.Result.PassesPerformed);
        Assert.Equal(0, frame.Result.OscillationsDetected);
        Assert.All(frame.Result.Spectrum, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void PushPcm16_ConstantOffsetInput_ProducesZeroSpectrum()
    {
        var analyzer = CreateAnalyzer();
        var samples = new float[SampleCount];
        Array.Fill(samples, 0.25f);

        var fired = analyzer.PushPcm16(Helpers.ToPcm16Bytes(samples), CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);
        Assert.InRange(frame!.Rms, 0.24f, 0.26f);
        Assert.Equal(0, frame.Result.PassesPerformed);
        Assert.Equal(0, frame.Result.OscillationsDetected);
        Assert.All(frame.Result.Spectrum, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void PushPcm16_TinyQuantizedNoise_RmsRoundsToZeroButSpectrumIsNotZero()
    {
        var analyzer = CreateAnalyzer();
        var pcm = CreateTinyNoisePcm16();

        var fired = analyzer.PushPcm16(pcm, CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);
        Assert.True(frame!.Rms > 0f);
        Assert.True(frame.Rms < 0.0005f);
        Assert.Equal("0.000", frame.Rms.ToString("0.000", CultureInfo.InvariantCulture));
        Assert.True(frame.Result.OscillationsDetected > 0);
        Assert.True(Max(frame.Result.Spectrum) > 0f);
    }

    [Fact]
    public void MicrophoneSilenceGate_BelowThreshold_ZeroesSpectrum()
    {
        var analyzer = CreateAnalyzer();
        var fired = analyzer.PushPcm16(CreateTinyNoisePcm16(), CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);

        var gated = MicrophoneSilenceGate.Apply(frame!, 0.0005f);

        Assert.NotSame(frame, gated);
        Assert.Equal(frame.Rms, gated.Rms);
        Assert.Equal(0, gated.Result.PassesPerformed);
        Assert.Equal(0, gated.Result.OscillationsDetected);
        Assert.All(gated.Result.Spectrum, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void MicrophoneSilenceGate_AboveThreshold_LeavesFrameUntouched()
    {
        var analyzer = CreateAnalyzer();
        var pcm = Helpers.ToPcm16Bytes(Helpers.Sine(SampleRate, 1000f, SampleCount, amplitude: 0.8f));
        var fired = analyzer.PushPcm16(pcm, CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);

        var gated = MicrophoneSilenceGate.Apply(frame!, 0.0005f);
        Assert.Same(frame, gated);
    }

    [Fact]
    public void QuietFrame_UsesLowDisplayScale_ForShortBars()
    {
        var analyzer = CreateAnalyzer();
        var pcm = Helpers.ToPcm16Bytes(Helpers.Sine(SampleRate, 1000f, SampleCount, amplitude: 0.001f));

        var fired = analyzer.PushPcm16(pcm, CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);
        Assert.InRange(frame!.Rms, 0.0005f, 0.01f);

        var loudnessScale = Math.Clamp(frame.Rms / 0.01f, 0f, 1f);
        var heights = SpectrumConsoleRenderer.ComputeHeights(frame.Result.Spectrum, chartHeight: 12, overallScale: loudnessScale);

        Assert.True(Max(heights) > 0);
        Assert.True(Max(heights) < 12);
    }

    [Fact]
    public void MicrophoneSilenceGate_BelowThreshold_ProducesEmptyDisplay()
    {
        var analyzer = CreateAnalyzer();
        var fired = analyzer.PushPcm16(CreateTinyNoisePcm16(), CreateMonoPcm16Format(), out var frame);

        Assert.True(fired);
        Assert.NotNull(frame);

        var gated = MicrophoneSilenceGate.Apply(frame!, 0.0005f);
        var heights = SpectrumConsoleRenderer.ComputeHeights(gated.Result.Spectrum, chartHeight: 12, overallScale: 1f);

        Assert.All(heights, value => Assert.Equal(0, value));
    }

    private static LiveSpectrumAnalyzer CreateAnalyzer()
    {
        return new LiveSpectrumAnalyzer(
            CreateOptions(),
            analysisWindowSamples: SampleCount,
            hopSamples: SampleCount);
    }

    private static ExtremaSpectrumOptions CreateOptions()
    {
        return new ExtremaSpectrumOptions
        {
            BinCount = 20,
            MinFrequencyHz = 100f,
            MaxFrequencyHz = SampleRate / 2f,
            MaxPasses = 12
        };
    }

    private static AudioBufferFormat CreateMonoPcm16Format()
    {
        return new AudioBufferFormat
        {
            SampleRate = SampleRate,
            Channels = 1,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.FirstChannel
        };
    }

    private static byte[] CreateTinyNoisePcm16()
    {
        var samples = new float[SampleCount];
        var random = new Random(12345);

        for (var i = 0; i < samples.Length; i++)
        {
            var quantized = random.Next(-1, 2);
            samples[i] = quantized / 32767f;
        }

        return Helpers.ToPcm16Bytes(samples);
    }

    private static float Max(float[] values)
    {
        var max = 0f;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
                max = values[i];
        }

        return max;
    }

    private static int Max(int[] values)
    {
        var max = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
                max = values[i];
        }

        return max;
    }
}
