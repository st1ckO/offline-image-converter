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

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string CreateImage(string filename, MagickFormat format)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, filename);
        using var image = new MagickImage(MagickColors.CornflowerBlue, 48, 32);
        image.Write(path, format);
        return path;
    }
}
