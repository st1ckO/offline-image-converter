using PrintShopImageConverter.Conversion;
using PrintShopImageConverter.Settings;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Load_returns_defaults_when_file_is_missing()
    {
        var store = CreateStore();

        var settings = await store.LoadAsync();

        Assert.Equal(OutputFormat.Jpeg, settings.Conversion.OutputFormat);
        Assert.Equal(95, settings.Conversion.JpegQuality);
        Assert.Equal(ColorHandling.ConvertToSrgb, settings.Conversion.ColorHandling);
    }

    [Fact]
    public async Task Save_round_trips_all_remembered_values()
    {
        var store = CreateStore();
        var expected = new AppSettings
        {
            LastSourceDirectory = @"C:\Jobs\Incoming",
            Conversion = new ConversionOptions
            {
                OutputFormat = OutputFormat.Png,
                JpegQuality = 88,
                ColorHandling = ColorHandling.PreserveSourceProfile,
                BackgroundColor = "#EFEFEF",
                DestinationDirectory = @"C:\Jobs\Converted"
            }
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_returns_defaults_when_json_is_corrupt()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SettingsPath, "{ definitely-not-json }");
        var store = CreateStore();

        var settings = await store.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task Save_normalizes_invalid_values()
    {
        var store = CreateStore();

        await store.SaveAsync(new AppSettings
        {
            LastSourceDirectory = "  ",
            Conversion = new ConversionOptions
            {
                JpegQuality = 500,
                BackgroundColor = "",
                DestinationDirectory = "  C:\\Output  "
            }
        });

        var settings = await store.LoadAsync();
        Assert.Equal(100, settings.Conversion.JpegQuality);
        Assert.Equal("#FFFFFF", settings.Conversion.BackgroundColor);
        Assert.Equal(@"C:\Output", settings.Conversion.DestinationDirectory);
        Assert.Empty(settings.LastSourceDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    private JsonSettingsStore CreateStore() => new(SettingsPath);
}
