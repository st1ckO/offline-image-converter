using PrintShopImageConverter.Conversion;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class FileIntakeServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Collect_accepts_supported_files_and_rejects_other_extensions()
    {
        var image = CreateFile("customer.HEIC");
        var document = CreateFile("notes.txt");

        var result = new FileIntakeService().Collect([image, document]);

        Assert.Equal([image], result.AcceptedPaths);
        var rejection = Assert.Single(result.RejectedSources);
        Assert.Equal(document, rejection.Path);
        Assert.Equal("Unsupported file format.", rejection.Reason);
    }

    [Fact]
    public void Collect_scans_only_the_top_level_of_a_folder()
    {
        var topLevel = CreateFile("top.webp");
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(_directory, "nested")).FullName;
        var nested = Path.Combine(nestedDirectory, "nested.png");
        File.WriteAllBytes(nested, []);

        var result = new FileIntakeService().Collect([_directory]);

        Assert.Equal([topLevel], result.AcceptedPaths);
        Assert.DoesNotContain(nested, result.AcceptedPaths);
    }

    [Fact]
    public void Collect_skips_paths_already_in_the_queue()
    {
        var image = CreateFile("photo.avif");

        var result = new FileIntakeService().Collect([image, image], [image]);

        Assert.Empty(result.AcceptedPaths);
        Assert.Empty(result.RejectedSources);
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.tiff")]
    [InlineData("photo.bmp")]
    [InlineData("photo.gif")]
    [InlineData("photo.heif")]
    [InlineData("artwork.pdf")]
    public void IsSupported_recognizes_the_documented_input_set(string filename)
    {
        Assert.True(FileIntakeService.IsSupported(filename));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, []);
        return path;
    }
}
