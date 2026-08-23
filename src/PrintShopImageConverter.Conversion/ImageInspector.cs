using ImageMagick;

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
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or InvalidDataException)
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

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "The image cannot be opened because access was denied.",
        IOException => "The image could not be read from disk.",
        _ => "The image is corrupt or uses an unsupported codec."
    };
}
