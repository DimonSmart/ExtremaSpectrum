using SkiaSharp;
using Svg.Skia;

namespace DimonSmart.ExtremaSpectrum.Demo;

internal static class SvgPngExporter
{
    private const double DefaultDpi = 96d;

    internal static void Export(SvgRasterizerOptions options)
    {
        Validate(options);

        using var inputStream = File.OpenRead(options.InputPath);
        var svg = new SKSvg();
        var picture = svg.Load(inputStream)
            ?? throw new InvalidOperationException($"Unable to load SVG '{options.InputPath}'.");

        var sourceBounds = picture.CullRect;
        if (sourceBounds.Width <= 0 || sourceBounds.Height <= 0)
            throw new InvalidOperationException($"SVG '{options.InputPath}' does not define a positive canvas size.");

        var scale = (float)(options.Dpi / DefaultDpi);
        var rasterWidth = Math.Max(1, (int)Math.Round(sourceBounds.Width * scale, MidpointRounding.AwayFromZero));
        var rasterHeight = Math.Max(1, (int)Math.Round(sourceBounds.Height * scale, MidpointRounding.AwayFromZero));
        var cropArea = ComputeCropArea(rasterWidth, rasterHeight, options);
        var drawMatrix = SKMatrix.CreateScaleTranslation(
            scale,
            scale,
            -sourceBounds.Left * scale,
            -sourceBounds.Top * scale);

        using var fullBitmap = new SKBitmap(rasterWidth, rasterHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(fullBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(picture, ref drawMatrix, paint: null);
            canvas.Flush();
        }

        using var croppedBitmap = new SKBitmap(cropArea.Width, cropArea.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                fullBitmap,
                cropArea,
                new SKRect(0, 0, cropArea.Width, cropArea.Height));
            canvas.Flush();
        }

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        using var outputStream = File.Open(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(outputStream);
    }

    internal static SKRectI ComputeCropArea(int rasterWidth, int rasterHeight, SvgRasterizerOptions options)
    {
        var left = ToPixelOffset(rasterWidth, options.CropLeftPercent);
        var top = ToPixelOffset(rasterHeight, options.CropTopPercent);
        var right = ToPixelOffset(rasterWidth, options.CropRightPercent);
        var bottom = ToPixelOffset(rasterHeight, options.CropBottomPercent);
        var width = rasterWidth - left - right;
        var height = rasterHeight - top - bottom;

        if (width <= 0)
            throw new ArgumentException("Horizontal crop must leave a positive output width.");
        if (height <= 0)
            throw new ArgumentException("Vertical crop must leave a positive output height.");

        return new SKRectI(left, top, left + width, top + height);
    }

    private static int ToPixelOffset(int size, double percent)
    {
        return (int)Math.Round(size * percent / 100d, MidpointRounding.AwayFromZero);
    }

    private static void Validate(SvgRasterizerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
            throw new ArgumentException("An input SVG path is required.", nameof(options));
        if (!File.Exists(options.InputPath))
            throw new FileNotFoundException($"Input SVG '{options.InputPath}' was not found.", options.InputPath);
        if (string.IsNullOrWhiteSpace(options.OutputPath))
            throw new ArgumentException("An output image path is required.", nameof(options));
        if (!Path.GetExtension(options.OutputPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The output image path must use the .png extension.", nameof(options));
        if (options.Dpi <= 0d)
            throw new ArgumentOutOfRangeException(nameof(options), "DPI must be greater than zero.");

        ValidatePercent(options.CropLeftPercent, nameof(options.CropLeftPercent));
        ValidatePercent(options.CropTopPercent, nameof(options.CropTopPercent));
        ValidatePercent(options.CropRightPercent, nameof(options.CropRightPercent));
        ValidatePercent(options.CropBottomPercent, nameof(options.CropBottomPercent));

        if (options.CropLeftPercent + options.CropRightPercent >= 100d)
            throw new ArgumentException("Horizontal crop percentages must leave a positive output width.", nameof(options));
        if (options.CropTopPercent + options.CropBottomPercent >= 100d)
            throw new ArgumentException("Vertical crop percentages must leave a positive output height.", nameof(options));
    }

    private static void ValidatePercent(double percent, string propertyName)
    {
        if (percent < 0d || percent > 100d)
            throw new ArgumentOutOfRangeException(propertyName, "Crop percentages must be in the [0, 100] range.");
    }
}
