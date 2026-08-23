using PrintShopImageConverter.Conversion;
using PrintShopImageConverter.Infrastructure;
using PrintShopImageConverter.Settings;
using PrintShopImageConverter.ViewModels;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PrintShopImageConverterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_restores_remembered_conversion_choices()
    {
        var settings = new MemorySettingsStore(new AppSettings
        {
            LastSourceDirectory = @"C:\Incoming",
            Conversion = new ConversionOptions
            {
                OutputFormat = OutputFormat.Png,
                JpegQuality = 87,
                ColorHandling = ColorHandling.PreserveSourceProfile,
                DestinationDirectory = @"C:\Converted"
            }
        });
        var viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.Equal(OutputFormat.Png, viewModel.OutputFormat);
        Assert.Equal(87, viewModel.JpegQuality);
        Assert.Equal(ColorHandling.PreserveSourceProfile, viewModel.ColorHandling);
        Assert.Equal(@"C:\Converted", viewModel.DestinationDirectory);
    }

    [Fact]
    public async Task AddPaths_puts_inspected_images_in_the_queue()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "customer.webp");
        await File.WriteAllBytesAsync(path, []);
        var viewModel = CreateViewModel(new MemorySettingsStore(new AppSettings()));

        await viewModel.AddPathsAsync([path]);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("customer.webp", item.DisplayName);
        Assert.Equal(SourceStatus.Ready, item.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static MainViewModel CreateViewModel(ISettingsStore settingsStore) => new(
        new FileIntakeService(),
        new StubInspector(),
        new StubConverter(),
        settingsStore,
        new StubDialogs());

    private sealed class StubInspector : IImageInspector
    {
        public Task<SourceItem> InspectAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceItem
            {
                Path = path,
                DisplayName = Path.GetFileName(path),
                Format = "WEBP",
                Width = 20,
                Height = 10,
                FrameCount = 1
            });

        public IReadOnlyList<string> GetMissingRequiredFormats() => [];
    }

    private sealed class StubConverter : IConversionService
    {
        public Task<ConversionResult> ConvertAsync(
            string sourcePath,
            ConversionOptions options,
            IProgress<ConversionProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConversionResult(sourcePath, [], []));
    }

    private sealed class MemorySettingsStore(AppSettings settings) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(AppSettings value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubDialogs : IWindowsDialogService
    {
        public IReadOnlyList<string> SelectImageFiles(string initialDirectory) => [];

        public string? SelectFolder(string initialDirectory, string title) => null;

        public void OpenFolder(string path)
        {
        }
    }
}
