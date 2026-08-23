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
        Assert.EndsWith(expectedExtension, output, StringComparison.OrdinalIgnoreCase);
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
}
