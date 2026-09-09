using ImageMagick;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace PrintShopImageConverter.Conversion;

public interface IImageInspector
{
    Task<SourceItem> InspectAsync(string path, CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetMissingRequiredFormats();
}

public sealed class ImageInspector : IImageInspector
{
    private static readonly string[] RequiredFormats =
        ["HEIC", "HEIF", "AVIF", "WEBP", "JPEG", "PNG", "TIFF", "BMP", "GIF"];

    public Task<SourceItem> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(() => Inspect(path, cancellationToken), cancellationToken);
    }

    public IReadOnlyList<string> GetMissingRequiredFormats()
    {
        var readable = MagickNET.SupportedFormats
            .Where(format => format.SupportsReading)
            .Select(format => format.Format.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RequiredFormats.Where(format => !readable.Contains(format)).ToArray();
    }

    private static SourceItem Inspect(string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfRendering.IsPdf(fullPath))
            {
                return InspectPdf(fullPath, cancellationToken);
            }

            using var frames = new MagickImageCollection(fullPath);
            cancellationToken.ThrowIfCancellationRequested();

            if (frames.Count == 0)
            {
                throw new InvalidDataException("The image contains no readable frames.");
            }

            using var preview = (MagickImage)frames[0].Clone();
            preview.AutoOrient();
            preview.Thumbnail(160, 120);
            preview.Format = MagickFormat.Png;

            var first = frames[0];
            return new SourceItem
            {
                Path = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                Format = first.Format.ToString().ToUpperInvariant(),
                Width = first.Width,
                Height = first.Height,
                FrameCount = frames.Count,
                HasAlpha = first.HasAlpha,
                ThumbnailPng = preview.ToByteArray(),
                Status = SourceStatus.Ready
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MagickException or PdfException or IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            return new SourceItem
            {
                Path = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                Format = Path.GetExtension(fullPath).TrimStart('.').ToUpperInvariant(),
                FrameCount = 0,
                Status = SourceStatus.Failed,
                Diagnostic = ToFriendlyMessage(exception)
            };
        }
    }

    private static SourceItem InspectPdf(string fullPath, CancellationToken cancellationToken)
    {
        using var pdf = File.OpenRead(fullPath);
        var pageSizes = global::PDFtoImage.Conversion.GetPageSizes(pdf, leaveOpen: true);
        cancellationToken.ThrowIfCancellationRequested();
        if (pageSizes.Count == 0)
        {
            throw new InvalidDataException("The PDF contains no readable pages.");
        }

        pdf.Position = 0;
        using var preview = global::PDFtoImage.Conversion.ToImage(
            pdf,
            page: 0,
            leaveOpen: true,
            options: PdfRendering.CreateThumbnailOptions());
        cancellationToken.ThrowIfCancellationRequested();
        using var previewData = preview.Encode(SKEncodedImageFormat.Png, quality: 100);

        var firstPage = pageSizes[0];
        return new SourceItem
        {
            Path = fullPath,
            DisplayName = Path.GetFileName(fullPath),
            Format = "PDF",
            Width = PdfRendering.ToPixelDimension(firstPage.Width),
            Height = PdfRendering.ToPixelDimension(firstPage.Height),
            FrameCount = pageSizes.Count,
            HasAlpha = false,
            ThumbnailPng = previewData.ToArray(),
            Status = SourceStatus.Ready
        };
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "The file cannot be opened because access was denied.",
        IOException => "The file could not be read from disk.",
        PdfPasswordProtectedException => "Password-protected PDFs are not supported.",
        PdfUnsupportedSecuritySchemeException => "The PDF uses an unsupported security scheme.",
        PdfException => "The PDF is corrupt or could not be opened.",
        FormatException => "The PDF is corrupt or could not be opened.",
        _ => "The image is corrupt or uses an unsupported codec."
    };
}
