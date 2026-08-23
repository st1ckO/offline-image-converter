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
            using var images = new MagickImageCollection(fullSourcePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (images.Count == 0)
            {
                throw new InvalidDataException("The image contains no readable frames.");
            }

            var extension = options.OutputFormat == OutputFormat.Jpeg ? ".jpg" : ".png";
            var baseName = Path.GetFileNameWithoutExtension(fullSourcePath);
            var outputPaths = new List<string>(images.Count);

            for (var index = 0; index < images.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = images[index];
                image.AutoOrient();
                image.Orientation = OrientationType.TopLeft;

                var numberedName = images.Count == 1
                    ? baseName
                    : $"{baseName}_{index + 1:000}";
                var outputPath = Path.Combine(
                    options.DestinationDirectory,
                    numberedName + extension);

                ConfigureOutput(image, options);
                image.Write(outputPath);
                outputPaths.Add(outputPath);
            }

            return new ConversionResult(fullSourcePath, outputPaths, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            return new ConversionResult(fullSourcePath, [], [], ToFriendlyMessage(exception));
        }
    }

    private static void ConfigureOutput(IMagickImage<ushort> image, ConversionOptions options)
    {
        ApplyColorHandling(image, options.ColorHandling);

        if (options.OutputFormat == OutputFormat.Jpeg)
        {
            if (image.HasAlpha)
            {
                image.BackgroundColor = new MagickColor(options.BackgroundColor);
                image.Alpha(AlphaOption.Remove);
                image.Alpha(AlphaOption.Off);
            }

            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)options.JpegQuality;
            image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", true);
            image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", "4:4:4");
            image.Depth = 8;
            return;
        }

        image.Format = MagickFormat.Png;
        image.Settings.Compression = CompressionMethod.Zip;
        image.Settings.SetDefine(MagickFormat.Png, "preserve-iCCP", true);
    }

    private static void ApplyColorHandling(IMagickImage<ushort> image, ColorHandling colorHandling)
    {
        if (colorHandling == ColorHandling.PreserveSourceProfile)
        {
            return;
        }

        var sourceProfile = image.GetColorProfile();
        if (sourceProfile is null)
        {
            image.ColorSpace = ColorSpace.sRGB;
            image.SetProfile(ColorProfiles.SRGB);
            return;
        }

        image.TransformColorSpace(ColorProfiles.SRGB);
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "The source or destination folder could not be accessed.",
        IOException => "The image could not be read or written.",
        InvalidOperationException => exception.Message,
        _ => "The image could not be converted. It may be corrupt or use an unsupported codec."
    };
}
