using ImageMagick;
using PrintShopImageConverter.Conversion;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class ImageInspectorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Inspect_returns_queue_metadata_and_thumbnail()
    {
        var path = PathFor("sample.png");
        using (var image = new MagickImage(MagickColors.CornflowerBlue, 320, 200))
        {
            image.Write(path, MagickFormat.Png);
        }

        var result = await new ImageInspector().InspectAsync(path);

        Assert.Equal(SourceStatus.Ready, result.Status);
        Assert.Equal("PNG", result.Format);
        Assert.Equal(320u, result.Width);
        Assert.Equal(200u, result.Height);
        Assert.Equal(1, result.FrameCount);
        Assert.NotEmpty(result.ThumbnailPng!);
    }

    [Fact]
    public async Task Inspect_reports_every_frame_in_an_animated_image()
    {
        var path = PathFor("animation.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 10, 10));
            frames.Add(new MagickImage(MagickColors.Blue, 10, 10));
            frames.Write(path, MagickFormat.Gif);
        }

        var result = await new ImageInspector().InspectAsync(path);

        Assert.Equal(SourceStatus.Ready, result.Status);
        Assert.Equal(2, result.FrameCount);
    }

    [Fact]
    public async Task Inspect_keeps_corrupt_files_in_the_queue_with_an_error()
    {
        var path = PathFor("broken.webp");
        await File.WriteAllTextAsync(path, "not an image");

        var result = await new ImageInspector().InspectAsync(path);

        Assert.Equal(SourceStatus.Failed, result.Status);
        Assert.NotEmpty(result.Diagnostic);
        Assert.Equal(0, result.FrameCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string PathFor(string filename)
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, filename);
    }
}
