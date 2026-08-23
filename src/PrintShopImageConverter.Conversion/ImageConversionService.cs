using ImageMagick;

namespace PrintShopImageConverter.Conversion;

public sealed record ConversionResult(
    string SourcePath,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(
        string sourcePath,
        ConversionOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class ImageConversionService : IConversionService
{
    public Task<ConversionResult> ConvertAsync(
        string sourcePath,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Convert(sourcePath, options.Normalize(), cancellationToken), cancellationToken);
    }

    private static ConversionResult Convert(
        string sourcePath,
        ConversionOptions options,
        CancellationToken cancellationToken)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(options.DestinationDirectory))
            {
                throw new InvalidOperationException("Choose an output folder before converting.");
            }

            Directory.CreateDirectory(options.DestinationDirectory);
            using var image = new MagickImage(fullSourcePath);
            cancellationToken.ThrowIfCancellationRequested();
            image.AutoOrient();

            var extension = options.OutputFormat == OutputFormat.Jpeg ? ".jpg" : ".png";
            var outputPath = Path.Combine(
                options.DestinationDirectory,
                Path.GetFileNameWithoutExtension(fullSourcePath) + extension);

            ConfigureOutput(image, options);
            image.Write(outputPath);

            return new ConversionResult(fullSourcePath, [outputPath], []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ConversionResult(fullSourcePath, [], [], ToFriendlyMessage(exception));
        }
    }

    private static void ConfigureOutput(MagickImage image, ConversionOptions options)
    {
        if (options.OutputFormat == OutputFormat.Jpeg)
        {
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)options.JpegQuality;
            image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", true);
            image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", "4:4:4");
            image.Depth = 8;
            return;
        }

        image.Format = MagickFormat.Png;
        image.Settings.Compression = CompressionMethod.Zip;
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "The source or destination folder could not be accessed.",
        IOException => "The image could not be read or written.",
        InvalidOperationException => exception.Message,
        _ => "The image could not be converted. It may be corrupt or use an unsupported codec."
    };
}
