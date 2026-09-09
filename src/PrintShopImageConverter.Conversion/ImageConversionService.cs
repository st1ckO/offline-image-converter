using ImageMagick;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace PrintShopImageConverter.Conversion;

public sealed record ConversionResult(
    string SourcePath,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

public sealed record ConversionProgress(
    string SourcePath,
    int CompletedFrames,
    int TotalFrames,
    string Message);

public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(
        string sourcePath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class ImageConversionService : IConversionService
{
    private readonly IOutputPathResolver _outputPathResolver;

    public ImageConversionService(IOutputPathResolver? outputPathResolver = null)
    {
        _outputPathResolver = outputPathResolver ?? new OutputPathResolver();
    }

    public Task<ConversionResult> ConvertAsync(
        string sourcePath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(
            () => ConvertCoreAsync(sourcePath, options.Normalize(), progress, cancellationToken),
            cancellationToken);
    }

    private async Task<ConversionResult> ConvertCoreAsync(
        string sourcePath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var outputPaths = new List<string>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(options.DestinationDirectory))
            {
                throw new InvalidOperationException("Choose an output folder before converting.");
            }

            Directory.CreateDirectory(options.DestinationDirectory);
            if (PdfRendering.IsPdf(fullSourcePath))
            {
                return await ConvertPdfAsync(
                    fullSourcePath,
                    options,
                    outputPaths,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            using var images = new MagickImageCollection(fullSourcePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (images.Count == 0)
            {
                throw new InvalidDataException("The image contains no readable frames.");
            }

            var extension = options.OutputFormat == OutputFormat.Jpeg ? ".jpg" : ".png";
            var baseName = Path.GetFileNameWithoutExtension(fullSourcePath);
            progress?.Report(new ConversionProgress(
                fullSourcePath,
                0,
                images.Count,
                $"Preparing {images.Count} frame{(images.Count == 1 ? string.Empty : "s")}."));
            for (var index = 0; index < images.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ConversionProgress(
                    fullSourcePath,
                    index,
                    images.Count,
                    $"Converting frame {index + 1} of {images.Count}."));
                var image = images[index];
                image.AutoOrient();
                image.Orientation = OrientationType.TopLeft;

                var numberedName = images.Count == 1
                    ? $"{baseName}_converted"
                    : $"{baseName}_{index + 1:000}_converted";
                var outputPath = _outputPathResolver.GetUniquePath(
                    options.DestinationDirectory,
                    numberedName,
                    extension);

                ConfigureOutput(image, options);
                WriteAtomically(image, outputPath, cancellationToken);
                outputPaths.Add(outputPath);
                progress?.Report(new ConversionProgress(
                    fullSourcePath,
                    index + 1,
                    images.Count,
                    $"Completed frame {index + 1} of {images.Count}."));
            }

            return new ConversionResult(fullSourcePath, outputPaths, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MagickException or PdfException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or FormatException)
        {
            return new ConversionResult(fullSourcePath, outputPaths, [], ToFriendlyMessage(exception));
        }
    }

    private async Task<ConversionResult> ConvertPdfAsync(
        string sourcePath,
        ConversionOptions options,
        List<string> outputPaths,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var pdf = File.OpenRead(sourcePath);
        var pageCount = global::PDFtoImage.Conversion.GetPageSizes(pdf, leaveOpen: true).Count;
        cancellationToken.ThrowIfCancellationRequested();
        if (pageCount == 0)
        {
            throw new InvalidDataException("The PDF contains no readable pages.");
        }

        var extension = options.OutputFormat == OutputFormat.Jpeg ? ".jpg" : ".png";
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        progress?.Report(new ConversionProgress(
            sourcePath,
            0,
            pageCount,
            $"Preparing {pageCount} page{(pageCount == 1 ? string.Empty : "s")}."));

        pdf.Position = 0;
        var pageIndex = 0;
        await foreach (var bitmap in global::PDFtoImage.Conversion.ToImagesAsync(
                           pdf,
                           leaveOpen: true,
                           options: PdfRendering.CreateOutputOptions(),
                           cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            using (bitmap)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ConversionProgress(
                    sourcePath,
                    pageIndex,
                    pageCount,
                    $"Converting page {pageIndex + 1} of {pageCount}."));

                var numberedName = pageCount == 1
                    ? $"{baseName}_converted"
                    : $"{baseName}_{pageIndex + 1:000}_converted";
                var outputPath = _outputPathResolver.GetUniquePath(
                    options.DestinationDirectory,
                    numberedName,
                    extension);

                using var encodedPage = bitmap.Encode(SKEncodedImageFormat.Png, quality: 100);
                using var image = new MagickImage(encodedPage.ToArray());
                image.Density = new Density(
                    PdfRendering.OutputDpi,
                    PdfRendering.OutputDpi,
                    DensityUnit.PixelsPerInch);
                ConfigureOutput(image, options);
                WriteAtomically(image, outputPath, cancellationToken);
                outputPaths.Add(outputPath);
            }

            pageIndex++;
            progress?.Report(new ConversionProgress(
                sourcePath,
                pageIndex,
                pageCount,
                $"Completed page {pageIndex} of {pageCount}."));
        }

        if (pageIndex != pageCount)
        {
            throw new InvalidDataException("The PDF did not return every expected page.");
        }

        return new ConversionResult(sourcePath, outputPaths, []);
    }

    private static void WriteAtomically(
        IMagickImage<ushort> image,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var temporaryPath = outputPath + $".{Guid.NewGuid():N}.partial";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            image.Write(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
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
        IOException => "The file could not be read or written.",
        PdfPasswordProtectedException => "Password-protected PDFs are not supported.",
        PdfUnsupportedSecuritySchemeException => "The PDF uses an unsupported security scheme.",
        PdfException => "The PDF is corrupt or could not be opened.",
        FormatException => "The PDF is corrupt or could not be opened.",
        InvalidOperationException => exception.Message,
        _ => "The image could not be converted. It may be corrupt or use an unsupported codec."
    };
}
