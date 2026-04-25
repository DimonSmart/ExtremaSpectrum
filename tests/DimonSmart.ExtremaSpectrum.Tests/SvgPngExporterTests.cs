using SkiaSharp;
using DimonSmart.ExtremaSpectrum.Demo;

namespace DimonSmart.ExtremaSpectrum.Tests;

public sealed class SvgPngExporterTests
{
    [Fact]
    public void Export_RespectsCropAndDpi()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ExtremaSpectrumTests", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(outputDirectory);
            var inputPath = Path.Combine(outputDirectory, "input.svg");
            var outputPath = Path.Combine(outputDirectory, "output.png");

            File.WriteAllText(
                inputPath,
                """
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="50" viewBox="0 0 100 50">
                  <rect width="100" height="50" fill="#ffffff"/>
                  <rect x="0" y="0" width="50" height="50" fill="#ff0000"/>
                  <rect x="50" y="0" width="50" height="50" fill="#0000ff"/>
                </svg>
                """);

            SvgPngExporter.Export(
                new SvgRasterizerOptions
                {
                    InputPath = inputPath,
                    OutputPath = outputPath,
                    CropRightPercent = 50d,
                    Dpi = 192d
                });

            Assert.True(File.Exists(outputPath));

            using var bitmap = SKBitmap.Decode(outputPath);
            Assert.NotNull(bitmap);
            Assert.Equal(100, bitmap.Width);
            Assert.Equal(100, bitmap.Height);

            var centerPixel = bitmap.GetPixel(50, 50);
            Assert.True(centerPixel.Red > 200);
            Assert.True(centerPixel.Blue < 50);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Export_RejectsCropThatRemovesTheWholeWidth()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ExtremaSpectrumTests", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(outputDirectory);
            var inputPath = Path.Combine(outputDirectory, "input.svg");

            File.WriteAllText(
                inputPath,
                """
                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                  <rect width="10" height="10" fill="#ffffff"/>
                </svg>
                """);

            var exception = Assert.Throws<ArgumentException>(
                () => SvgPngExporter.Export(
                    new SvgRasterizerOptions
                    {
                        InputPath = inputPath,
                        OutputPath = Path.Combine(outputDirectory, "output.png"),
                        CropLeftPercent = 50d,
                        CropRightPercent = 50d
                    }));

            Assert.Contains("positive output width", exception.Message);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
