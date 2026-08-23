using PrintShopImageConverter.Conversion;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class OutputPathResolverTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetUniquePath_uses_the_original_name_when_available()
    {
        Directory.CreateDirectory(_directory);

        var result = new OutputPathResolver().GetUniquePath(_directory, "photo", ".jpg");

        Assert.Equal(Path.Combine(_directory, "photo.jpg"), result);
    }

    [Fact]
    public void GetUniquePath_adds_the_first_available_numeric_suffix()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "photo.jpg"), []);
        File.WriteAllBytes(Path.Combine(_directory, "photo_2.jpg"), []);

        var result = new OutputPathResolver().GetUniquePath(_directory, "photo", "jpg");

        Assert.Equal(Path.Combine(_directory, "photo_3.jpg"), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
