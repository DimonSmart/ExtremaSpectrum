using Xunit;

namespace ExtremaSpectrum.Tests;

public sealed class IntegrationTests
{
    private static readonly ExtremaSpectrumOptions Opts = new()
    {
        BinCount       = 64,
        MinFrequencyHz = 100f,
        MaxFrequencyHz = 8000f,
        MaxPasses      = 16
    };

    // ------------------------------------------------------------------
    // Integration 1 – Push a sequence of blocks, check analysis fires
    // ------------------------------------------------------------------
    [Fact]
    public void PushSequence_AnalysisFires_WhenWindowFilled()
    {
        const int sampleRate       = 44100;
        const int windowSamples    = 2048;
        const int hopSamples       = 512;
        const int blockSize        = 256; // smaller than window

        var analyzer = new StreamingExtremaSpectrumAnalyzer(Opts, windowSamples, hopSamples);
        var block = Helpers.Sine(sampleRate, 1000f, blockSize);

        var analysisCount = 0;
        var pushCount     = 0;

        // Keep pushing until we have triggered at least one analysis.
        while (analysisCount == 0 && pushCount < 1000)
        {
            var fired = analyzer.Push(block, sampleRate, out var result);
            if (fired)
            {
                analysisCount++;
                Assert.NotNull(result);
                Assert.Equal(Opts.BinCount, result!.Spectrum.Length);
            }
            pushCount++;
        }

        Assert.True(analysisCount > 0, "Analysis never fired after many pushes.");
    }

    // ------------------------------------------------------------------
    // Integration 2 – Window accumulation threshold is respected
    // ------------------------------------------------------------------
    [Fact]
    public void Push_NoAnalysis_BeforeWindowFilled()
    {
        const int sampleRate    = 44100;
        const int windowSamples = 2048;
        const int hopSamples    = 512;
        const int blockSize     = 100; // push less than window

        var analyzer = new StreamingExtremaSpectrumAnalyzer(Opts, windowSamples, hopSamples);
        var block = Helpers.Sine(sampleRate, 1000f, blockSize);

        // Push just under a full window.
        var totalPushed = 0;
        while (totalPushed + blockSize < windowSamples)
        {
            var fired = analyzer.Push(block, sampleRate, out _);
            Assert.False(fired, $"Analysis fired prematurely after {totalPushed + blockSize} samples.");
            totalPushed += blockSize;
        }
    }

    // ------------------------------------------------------------------
    // Integration 3 – Window slides by exactly hopSamples
    // ------------------------------------------------------------------
    [Fact]
    public void Push_WindowSlides_ByHopSamples()
    {
        const int sampleRate    = 44100;
        const int windowSamples = 1024;
        const int hopSamples    = 256;

        var analyzer = new StreamingExtremaSpectrumAnalyzer(Opts, windowSamples, hopSamples);

        // Fill exactly one window.
        var fullWindow = Helpers.Sine(sampleRate, 800f, windowSamples);
        var firstFired = analyzer.Push(fullWindow, sampleRate, out var firstResult);
        Assert.True(firstFired, "First analysis should fire after filling exactly one window.");
        Assert.NotNull(firstResult);

        // After hopSamples more, the second window should fire.
        var hop = Helpers.Sine(sampleRate, 800f, hopSamples);
        var secondFired = analyzer.Push(hop, sampleRate, out var secondResult);
        Assert.True(secondFired, "Second analysis should fire after one more hop.");
        Assert.NotNull(secondResult);

        // Both results should be non-trivially filled (sine has oscillations).
        float sum1 = 0f, sum2 = 0f;
        foreach (var v in firstResult!.Spectrum)  sum1 += v;
        foreach (var v in secondResult!.Spectrum) sum2 += v;
        Assert.True(sum1 > 0, "First window spectrum is empty.");
        Assert.True(sum2 > 0, "Second window spectrum is empty.");
    }

    // ------------------------------------------------------------------
    // Integration 4 – PCM16 streaming path works end-to-end
    // ------------------------------------------------------------------
    [Fact]
    public void PushPcm16_StreamingPath_WorksEndToEnd()
    {
        const int sampleRate    = 44100;
        const int windowSamples = 2048;
        const int hopSamples    = 1024;
        const int blockFrames   = 512;

        var analyzer = new StreamingExtremaSpectrumAnalyzer(Opts, windowSamples, hopSamples);
        var format = new AudioBufferFormat
        {
            SampleRate    = sampleRate,
            Channels      = 1,
            BitsPerSample = 16,
            Interleaved   = true,
            ChannelMixMode = ChannelMixMode.FirstChannel
        };

        var floatBlock = Helpers.Sine(sampleRate, 1000f, blockFrames);
        var  pcmBlock   = Helpers.ToPcm16Bytes(floatBlock);

        var anyFired = false;
        for (var push = 0; push < 20; push++)
        {
            if (analyzer.PushPcm16(pcmBlock, format, out var result))
            {
                anyFired = true;
                Assert.NotNull(result);
                break;
            }
        }

        Assert.True(anyFired, "PCM16 streaming analysis never fired.");
    }
}
