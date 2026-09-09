using ImageMagick;
using PrintShopImageConverter.Conversion;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class ImageConversionServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(OutputFormat.Jpeg, MagickFormat.Jpeg, ".jpg")]
    [InlineData(OutputFormat.Png, MagickFormat.Png, ".png")]
    public async Task Convert_exports_a_still_image(
        OutputFormat outputFormat,
        MagickFormat expectedFormat,
        string expectedExtension)
    {
        var source = CreateImage("source.webp", MagickFormat.WebP);
        var destination = Path.Combine(_directory, "converted");
        var service = new ImageConversionService();

        var result = await service.ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = outputFormat,
            DestinationDirectory = destination
        });

        Assert.True(result.Succeeded, result.Error);
        var output = Assert.Single(result.OutputPaths);
        Assert.EndsWith($"source_converted{expectedExtension}", output, StringComparison.OrdinalIgnoreCase);
        using var image = new MagickImage(output);
        Assert.Equal(expectedFormat, image.Format);
        Assert.Equal(48u, image.Width);
        Assert.Equal(32u, image.Height);
    }

    [Fact]
    public async Task Convert_reports_an_error_when_destination_is_missing()
    {
        var source = CreateImage("source.png", MagickFormat.Png);

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("output folder", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.OutputPaths);
    }

    [Fact]
    public async Task Convert_composites_transparency_over_white_for_jpeg()
    {
        var source = Path.Combine(CreateDirectory(), "transparent.png");
        using (var image = new MagickImage(MagickColors.Transparent, 8, 8))
        {
            image.Write(source, MagickFormat.Png);
        }

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = OutputFormat.Jpeg,
            DestinationDirectory = Path.Combine(_directory, "output")
        });

        Assert.True(result.Succeeded, result.Error);
        using var output = new MagickImage(Assert.Single(result.OutputPaths));
        Assert.False(output.HasAlpha);
        using var pixels = output.GetPixels();
        var color = pixels.GetPixel(0, 0).ToColor();
        Assert.NotNull(color);
        Assert.True(color.R > 60000 && color.G > 60000 && color.B > 60000);
    }

    [Fact]
    public async Task Convert_embeds_srgb_and_preserves_density_metadata()
    {
        var source = Path.Combine(CreateDirectory(), "metadata.png");
        using (var image = new MagickImage(MagickColors.Orange, 8, 8))
        {
            image.Density = new Density(300, 300, DensityUnit.PixelsPerInch);
            image.Write(source, MagickFormat.Png);
        }

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = OutputFormat.Png,
            ColorHandling = ColorHandling.ConvertToSrgb,
            DestinationDirectory = Path.Combine(_directory, "output")
        });

        Assert.True(result.Succeeded, result.Error);
        using var output = new MagickImage(Assert.Single(result.OutputPaths));
        var profile = output.GetColorProfile();
        Assert.NotNull(profile);
        var density = output.Density.ChangeUnits(DensityUnit.PixelsPerInch);
        Assert.InRange(density.X, 299.5, 300.5);
        Assert.InRange(density.Y, 299.5, 300.5);
    }

    [Fact]
    public async Task Convert_exports_every_frame_with_numbered_names()
    {
        var source = Path.Combine(CreateDirectory(), "customer-animation.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 12, 10));
            frames.Add(new MagickImage(MagickColors.Green, 12, 10));
            frames.Add(new MagickImage(MagickColors.Blue, 12, 10));
            frames.Write(source, MagickFormat.Gif);
        }

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = OutputFormat.Png,
            DestinationDirectory = Path.Combine(_directory, "output")
        });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3, result.OutputPaths.Count);
        Assert.Collection(
            result.OutputPaths,
            path => Assert.EndsWith("customer-animation_001_converted.png", path),
            path => Assert.EndsWith("customer-animation_002_converted.png", path),
            path => Assert.EndsWith("customer-animation_003_converted.png", path));
        Assert.All(result.OutputPaths, path => Assert.True(File.Exists(path)));
    }

    [Theory]
    [InlineData(OutputFormat.Jpeg, MagickFormat.Jpeg, ".jpg")]
    [InlineData(OutputFormat.Png, MagickFormat.Png, ".png")]
    public async Task Convert_exports_every_pdf_page_at_print_resolution(
        OutputFormat outputFormat,
        MagickFormat expectedFormat,
        string expectedExtension)
    {
        var source = TestPdfFactory.Write(
            Path.Combine(CreateDirectory(), "customer-proof.pdf"),
            "1 0 0 rg 0 0 72 36 re f",
            "0 0 1 rg 0 0 72 36 re f");

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = outputFormat,
            DestinationDirectory = Path.Combine(_directory, "output")
        });

        Assert.True(result.Succeeded, result.Error);
        Assert.Collection(
            result.OutputPaths,
            path => Assert.EndsWith($"customer-proof_001_converted{expectedExtension}", path),
            path => Assert.EndsWith($"customer-proof_002_converted{expectedExtension}", path));

        using var firstPage = new MagickImage(result.OutputPaths[0]);
        using var secondPage = new MagickImage(result.OutputPaths[1]);
        Assert.Equal(expectedFormat, firstPage.Format);
        Assert.Equal(expectedFormat, secondPage.Format);
        Assert.Equal(300u, firstPage.Width);
        Assert.Equal(150u, firstPage.Height);
        Assert.InRange(firstPage.Density.ChangeUnits(DensityUnit.PixelsPerInch).X, 299.5, 300.5);

        var firstColor = firstPage.GetPixels().GetPixel(150, 75).ToColor();
        var secondColor = secondPage.GetPixels().GetPixel(150, 75).ToColor();
        Assert.NotNull(firstColor);
        Assert.NotNull(secondColor);
        Assert.True(firstColor.R > firstColor.B);
        Assert.True(secondColor.B > secondColor.R);
    }

    [Fact]
    public async Task Convert_never_overwrites_an_existing_output()
    {
        var source = CreateImage("customer.png", MagickFormat.Png);
        var destination = Path.Combine(CreateDirectory(), "output");
        Directory.CreateDirectory(destination);
        var existing = Path.Combine(destination, "customer_converted.jpg");
        await File.WriteAllTextAsync(existing, "keep me");

        var result = await new ImageConversionService().ConvertAsync(source, new ConversionOptions
        {
            OutputFormat = OutputFormat.Jpeg,
            DestinationDirectory = destination
        });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("keep me", await File.ReadAllTextAsync(existing));
        Assert.EndsWith("customer_converted_2.jpg", Assert.Single(result.OutputPaths));
        Assert.Empty(Directory.EnumerateFiles(destination, "*.partial"));
    }

    [Fact]
    public async Task Convert_reports_progress_for_each_completed_frame()
    {
        var source = CreateImage("progress.png", MagickFormat.Png);
        var updates = new List<ConversionProgress>();

        var result = await new ImageConversionService().ConvertAsync(
            source,
            new ConversionOptions
            {
                OutputFormat = OutputFormat.Png,
                DestinationDirectory = Path.Combine(_directory, "output")
            },
            new InlineProgress<ConversionProgress>(updates.Add));

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(updates, update => update.CompletedFrames == 0 && update.TotalFrames == 1);
        Assert.Contains(updates, update => update.CompletedFrames == 1 && update.TotalFrames == 1);
    }

    [Fact]
    public async Task Convert_honors_cancellation_without_leaving_partial_files()
    {
        var source = CreateImage("cancel.png", MagickFormat.Png);
        var destination = Path.Combine(_directory, "output");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ImageConversionService().ConvertAsync(
                source,
                new ConversionOptions
                {
                    DestinationDirectory = destination
                },
                cancellationToken: cancellation.Token));

        Assert.False(Directory.Exists(destination) &&
                     Directory.EnumerateFiles(destination, "*.partial").Any());
    }

    [Fact]
    public async Task Convert_stops_a_pdf_between_pages_without_leaving_partial_files()
    {
        var source = TestPdfFactory.Write(
            Path.Combine(CreateDirectory(), "cancel-pages.pdf"),
            "1 0 0 rg 0 0 72 36 re f",
            "0 1 0 rg 0 0 72 36 re f",
            "0 0 1 rg 0 0 72 36 re f");
        var destination = Path.Combine(_directory, "output");
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<ConversionProgress>(update =>
        {
            if (update.CompletedFrames == 1)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ImageConversionService().ConvertAsync(
                source,
                new ConversionOptions
                {
                    OutputFormat = OutputFormat.Png,
                    DestinationDirectory = destination
                },
                progress,
                cancellation.Token));

        Assert.Single(Directory.EnumerateFiles(destination, "*.png"));
        Assert.Empty(Directory.EnumerateFiles(destination, "*.partial"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string CreateImage(string filename, MagickFormat format)
    {
        CreateDirectory();
        var path = Path.Combine(_directory, filename);
        using var image = new MagickImage(MagickColors.CornflowerBlue, 48, 32);
        image.Write(path, format);
        return path;
    }

    private string CreateDirectory()
    {
        Directory.CreateDirectory(_directory);
        return _directory;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
