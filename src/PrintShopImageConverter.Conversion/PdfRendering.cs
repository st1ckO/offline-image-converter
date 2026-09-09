using PDFtoImage;
using SkiaSharp;

namespace PrintShopImageConverter.Conversion;

internal static class PdfRendering
{
    public const int OutputDpi = 300;

    public static bool IsPdf(string path) =>
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public static RenderOptions CreateOutputOptions() => new()
    {
        Dpi = OutputDpi,
        WithAnnotations = true,
        WithFormFill = true,
        BackgroundColor = SKColors.White
    };

    public static RenderOptions CreateThumbnailOptions() => new()
    {
        Width = 160,
        Height = 120,
        WithAspectRatio = true,
        WithAnnotations = true,
        WithFormFill = true,
        BackgroundColor = SKColors.White
    };

    public static uint ToPixelDimension(float points) =>
        checked((uint)Math.Max(1, Math.Round(points * OutputDpi / 72d)));
}
