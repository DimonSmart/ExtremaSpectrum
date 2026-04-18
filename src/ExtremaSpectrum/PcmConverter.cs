using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ExtremaSpectrum;

/// <summary>
/// Converts raw PCM byte buffers to normalised mono float arrays.
/// This is format adaptation, not signal processing.
/// </summary>
internal static class PcmConverter
{
    /// <summary>
    /// Converts a PCM-16 interleaved or planar buffer to a normalised mono float array.
    /// </summary>
    internal static float[] ToMonoFloat(ReadOnlySpan<byte> buffer, AudioBufferFormat fmt)
    {
        if (fmt.BitsPerSample != 16)
            throw new ArgumentException("AnalyzePcm16 requires BitsPerSample == 16.", nameof(fmt));

        ValidateByteLength(buffer.Length, sizeof(short), fmt.Channels, fmt.BitsPerSample);

        var bytesPerSample = sizeof(short); // 2
        var totalSamples   = buffer.Length / bytesPerSample;
        var channels       = fmt.Channels;
        var frames         = totalSamples / channels;

        var mono = new float[frames];

        if (fmt.Interleaved)
        {
            ToMonoInterleaved16(buffer, mono, channels, fmt.ChannelMixMode, fmt.PreferredChannel);
        }
        else
        {
            ToMonoPlanar16(buffer, mono, channels, frames, fmt.ChannelMixMode, fmt.PreferredChannel);
        }

        return mono;
    }

    /// <summary>
    /// Converts a 32-bit float interleaved or planar buffer to a normalised mono float array.
    /// </summary>
    internal static float[] ToMonoFloatFrom32(ReadOnlySpan<byte> buffer, AudioBufferFormat fmt)
    {
        if (fmt.BitsPerSample != 32)
            throw new ArgumentException("AnalyzeFloat32 requires BitsPerSample == 32.", nameof(fmt));

        ValidateByteLength(buffer.Length, sizeof(float), fmt.Channels, fmt.BitsPerSample);

        var bytesPerSample = sizeof(float); // 4
        var totalSamples   = buffer.Length / bytesPerSample;
        var channels       = fmt.Channels;
        var frames         = totalSamples / channels;

        var mono = new float[frames];

        if (fmt.Interleaved)
        {
            ToMonoInterleaved32(buffer, mono, channels, fmt.ChannelMixMode, fmt.PreferredChannel);
        }
        else
        {
            ToMonoPlanar32(buffer, mono, channels, frames, fmt.ChannelMixMode, fmt.PreferredChannel);
        }

        return mono;
    }

    // -----------------------------------------------------------------------
    // PCM-16 interleaved
    // -----------------------------------------------------------------------

    private static void ToMonoInterleaved16(
        ReadOnlySpan<byte> buffer,
        float[] mono,
        int channels,
        ChannelMixMode mode,
        int preferredChannel)
    {
        const float scale = 1f / 32768f;
        var frames = mono.Length;

        if (channels == 1)
        {
            for (var f = 0; f < frames; f++)
                mono[f] = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(f * 2, 2)) * scale;
            return;
        }

        var blockBytes = channels * sizeof(short);

        switch (mode)
        {
            case ChannelMixMode.FirstChannel:
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(f * blockBytes, 2)) * scale;
                break;

            case ChannelMixMode.PreferredChannel:
                var chOffset = preferredChannel * sizeof(short);
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadInt16LittleEndian(
                        buffer.Slice(f * blockBytes + chOffset, 2)) * scale;
                break;

            case ChannelMixMode.AverageAllChannels:
                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    var   frameOff = f * blockBytes;
                    for (var c = 0; c < channels; c++)
                        sum += BinaryPrimitives.ReadInt16LittleEndian(
                            buffer.Slice(frameOff + c * sizeof(short), 2));
                    mono[f] = sum / channels * scale;
                }
                break;
        }
    }

    // -----------------------------------------------------------------------
    // PCM-16 planar
    // -----------------------------------------------------------------------

    private static void ToMonoPlanar16(
        ReadOnlySpan<byte> buffer,
        float[] mono,
        int channels,
        int frames,
        ChannelMixMode mode,
        int preferredChannel)
    {
        const float scale = 1f / 32768f;
        var channelBytes = frames * sizeof(short);

        switch (mode)
        {
            case ChannelMixMode.FirstChannel:
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(f * 2, 2)) * scale;
                break;

            case ChannelMixMode.PreferredChannel:
                var chByteOff = preferredChannel * channelBytes;
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadInt16LittleEndian(
                        buffer.Slice(chByteOff + f * sizeof(short), 2)) * scale;
                break;

            case ChannelMixMode.AverageAllChannels:
                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                        sum += BinaryPrimitives.ReadInt16LittleEndian(
                            buffer.Slice(c * channelBytes + f * sizeof(short), 2));
                    mono[f] = sum / channels * scale;
                }
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Float32 interleaved
    // -----------------------------------------------------------------------

    private static void ToMonoInterleaved32(
        ReadOnlySpan<byte> buffer,
        float[] mono,
        int channels,
        ChannelMixMode mode,
        int preferredChannel)
    {
        var frames = mono.Length;

        if (channels == 1)
        {
            ReadOnlySpan<float> floats = MemoryMarshal.Cast<byte, float>(buffer);
            floats.Slice(0, frames).CopyTo(mono);
            return;
        }

        var blockFloats = channels;

        switch (mode)
        {
            case ChannelMixMode.FirstChannel:
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadSingleLittleEndian(
                        buffer.Slice(f * blockFloats * 4, 4));
                break;

            case ChannelMixMode.PreferredChannel:
                var chOff = preferredChannel * 4;
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadSingleLittleEndian(
                        buffer.Slice(f * blockFloats * 4 + chOff, 4));
                break;

            case ChannelMixMode.AverageAllChannels:
                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    var   frameOff = f * blockFloats * 4;
                    for (var c = 0; c < channels; c++)
                        sum += BinaryPrimitives.ReadSingleLittleEndian(
                            buffer.Slice(frameOff + c * 4, 4));
                    mono[f] = sum / channels;
                }
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Float32 planar
    // -----------------------------------------------------------------------

    private static void ToMonoPlanar32(
        ReadOnlySpan<byte> buffer,
        float[] mono,
        int channels,
        int frames,
        ChannelMixMode mode,
        int preferredChannel)
    {
        var channelBytes = frames * sizeof(float);

        switch (mode)
        {
            case ChannelMixMode.FirstChannel:
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(f * 4, 4));
                break;

            case ChannelMixMode.PreferredChannel:
                var chByteOff = preferredChannel * channelBytes;
                for (var f = 0; f < frames; f++)
                    mono[f] = BinaryPrimitives.ReadSingleLittleEndian(
                        buffer.Slice(chByteOff + f * 4, 4));
                break;

            case ChannelMixMode.AverageAllChannels:
                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                        sum += BinaryPrimitives.ReadSingleLittleEndian(
                            buffer.Slice(c * channelBytes + f * 4, 4));
                    mono[f] = sum / channels;
                }
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    private static void ValidateByteLength(int byteLength, int bytesPerSample, int channels, int bitsPerSample)
    {
        var blockAlign = bytesPerSample * channels;
        if (byteLength % blockAlign != 0)
            throw new ArgumentException(
                $"Buffer length ({byteLength} bytes) is not a multiple of blockAlign " +
                $"({blockAlign} bytes = {bitsPerSample} bits × {channels} channels).");
    }
}
