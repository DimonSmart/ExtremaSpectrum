using System.Buffers.Binary;
using System.Text;

namespace ExtremaSpectrum.Demo;

internal static class WaveFileReader
{
    private const short PcmFormatTag = 1;
    private const short Float32FormatTag = 3;

    public static WaveFile ReadMono(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var data = bytes.AsSpan();

        if (data.Length < 12)
            throw new InvalidDataException("WAV file is too short.");

        if (!HasChunkId(data, 0, "RIFF") || !HasChunkId(data, 8, "WAVE"))
            throw new InvalidDataException("Only RIFF/WAVE files are supported.");

        var offset = 12;
        WaveFormat? waveFormat = null;
        ReadOnlySpan<byte> audioData = default;

        while (offset + 8 <= data.Length)
        {
            var chunkId = Encoding.ASCII.GetString(data.Slice(offset, 4));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
            var chunkDataOffset = offset + 8;

            if (chunkSize < 0 || chunkDataOffset + chunkSize > data.Length)
                throw new InvalidDataException($"Chunk '{chunkId}' exceeds file length.");

            var chunk = data.Slice(chunkDataOffset, chunkSize);

            switch (chunkId)
            {
                case "fmt ":
                    waveFormat = ParseWaveFormat(chunk);
                    break;

                case "data":
                    audioData = chunk;
                    break;
            }

            offset = chunkDataOffset + chunkSize;
            if ((chunkSize & 1) != 0)
                offset++;
        }

        if (waveFormat is null)
            throw new InvalidDataException("Missing 'fmt ' chunk.");
        if (audioData.IsEmpty)
            throw new InvalidDataException("Missing 'data' chunk.");

        var format = waveFormat.Value;
        var samples = format.FormatTag switch
        {
            PcmFormatTag when format.BitsPerSample == 16 => DecodePcm16Mono(audioData, format),
            Float32FormatTag when format.BitsPerSample == 32 => DecodeFloat32Mono(audioData, format),
            _ => throw new NotSupportedException(
                $"Unsupported WAV format. FormatTag={format.FormatTag}, BitsPerSample={format.BitsPerSample}.")
        };

        return new WaveFile
        {
            Samples = samples,
            SampleRate = format.SampleRate,
            Channels = format.Channels,
            BitsPerSample = format.BitsPerSample,
            FormatTag = format.FormatTag
        };
    }

    private static bool HasChunkId(ReadOnlySpan<byte> data, int offset, string expected)
    {
        if (offset + 4 > data.Length)
            return false;

        return data[offset] == expected[0]
            && data[offset + 1] == expected[1]
            && data[offset + 2] == expected[2]
            && data[offset + 3] == expected[3];
    }

    private static WaveFormat ParseWaveFormat(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length < 16)
            throw new InvalidDataException("The 'fmt ' chunk is too short.");

        var formatTag = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(0, 2));
        var channels = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(2, 2));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk.Slice(4, 4));
        var blockAlign = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(12, 2));
        var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(14, 2));

        if (channels <= 0)
            throw new InvalidDataException("WAV file declares zero channels.");
        if (sampleRate <= 0)
            throw new InvalidDataException("WAV file declares an invalid sample rate.");
        if (blockAlign <= 0)
            throw new InvalidDataException("WAV file declares an invalid block alignment.");

        return new WaveFormat(formatTag, channels, sampleRate, blockAlign, bitsPerSample);
    }

    private static float[] DecodePcm16Mono(ReadOnlySpan<byte> audioData, WaveFormat format)
    {
        if (audioData.Length % format.BlockAlign != 0)
            throw new InvalidDataException("PCM16 data length is not aligned to full frames.");

        var frameCount = audioData.Length / format.BlockAlign;
        var mono = new float[frameCount];
        const float scale = 1f / 32768f;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var frameOffset = frame * format.BlockAlign;

            for (var channel = 0; channel < format.Channels; channel++)
            {
                var sampleOffset = frameOffset + channel * sizeof(short);
                sum += BinaryPrimitives.ReadInt16LittleEndian(audioData.Slice(sampleOffset, sizeof(short)));
            }

            mono[frame] = sum / format.Channels * scale;
        }

        return mono;
    }

    private static float[] DecodeFloat32Mono(ReadOnlySpan<byte> audioData, WaveFormat format)
    {
        if (audioData.Length % format.BlockAlign != 0)
            throw new InvalidDataException("Float32 data length is not aligned to full frames.");

        var frameCount = audioData.Length / format.BlockAlign;
        var mono = new float[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var frameOffset = frame * format.BlockAlign;

            for (var channel = 0; channel < format.Channels; channel++)
            {
                var sampleOffset = frameOffset + channel * sizeof(float);
                sum += BinaryPrimitives.ReadSingleLittleEndian(audioData.Slice(sampleOffset, sizeof(float)));
            }

            mono[frame] = sum / format.Channels;
        }

        return mono;
    }

    private readonly record struct WaveFormat(
        short FormatTag,
        short Channels,
        int SampleRate,
        short BlockAlign,
        short BitsPerSample);
}
