using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PrintShopImageConverter.Conversion;
using PrintShopImageConverter.Infrastructure;
using PrintShopImageConverter.Settings;

namespace PrintShopImageConverter.ViewModels;

public sealed record SelectionOption<T>(T Value, string Label);

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IFileIntakeService _intakeService;
    private readonly IImageInspector _imageInspector;
    private readonly IConversionService _conversionService;
    private readonly ISettingsStore _settingsStore;
    private readonly IWindowsDialogService _dialogService;
    private CancellationTokenSource? _conversionCancellation;
    private OutputFormat _outputFormat = OutputFormat.Jpeg;
    private ColorHandling _colorHandling = ColorHandling.ConvertToSrgb;
    private int _jpegQuality = 95;
    private string _destinationDirectory = string.Empty;
    private string _lastSourceDirectory = string.Empty;
    private string _statusMessage = "Drop customer images here to get started.";
    private bool _isBusy;
    private double _progressPercentage;

    public MainViewModel(
        IFileIntakeService intakeService,
        IImageInspector imageInspector,
        IConversionService conversionService,
        ISettingsStore settingsStore,
        IWindowsDialogService dialogService)
    {
        _intakeService = intakeService;
        _imageInspector = imageInspector;
        _conversionService = conversionService;
        _settingsStore = settingsStore;
        _dialogService = dialogService;

        AddFilesCommand = new AsyncRelayCommand(AddFilesFromDialogAsync, () => !IsBusy);
        AddFolderCommand = new AsyncRelayCommand(AddFolderFromDialogAsync, () => !IsBusy);
        ChooseDestinationCommand = new RelayCommand(_ => ChooseDestination(), _ => !IsBusy);
        ConvertCommand = new AsyncRelayCommand(ConvertAllAsync, CanConvert);
        CancelCommand = new RelayCommand(_ => _conversionCancellation?.Cancel(), _ => IsBusy);
        ClearCommand = new RelayCommand(_ => Clear(), _ => !IsBusy && Items.Count > 0);
        RemoveCommand = new RelayCommand(Remove, item => !IsBusy && item is QueueItemViewModel);
        OpenDestinationCommand = new RelayCommand(_ => OpenDestination(), _ => Directory.Exists(DestinationDirectory));
    }

    public ObservableCollection<QueueItemViewModel> Items { get; } = [];

    public IReadOnlyList<SelectionOption<OutputFormat>> OutputFormats { get; } =
    [
        new(OutputFormat.Jpeg, "JPG"),
        new(OutputFormat.Png, "PNG")
    ];

    public IReadOnlyList<SelectionOption<ColorHandling>> ColorOptions { get; } =
    [
        new(ColorHandling.ConvertToSrgb, "Convert to sRGB"),
        new(ColorHandling.PreserveSourceProfile, "Preserve source profile")
    ];

    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            if (SetField(ref _outputFormat, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsJpegQualityEnabled)));
            }
        }
    }

    public bool IsJpegQualityEnabled => OutputFormat == OutputFormat.Jpeg;

    public ColorHandling ColorHandling
    {
        get => _colorHandling;
        set => SetField(ref _colorHandling, value);
    }

    public int JpegQuality
    {
        get => _jpegQuality;
        set => SetField(ref _jpegQuality, Math.Clamp(value, 1, 100));
    }

    public string DestinationDirectory
    {
        get => _destinationDirectory;
        set
        {
            if (SetField(ref _destinationDirectory, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetField(ref _progressPercentage, value);
    }

    public ICommand AddFilesCommand { get; }

    public ICommand AddFolderCommand { get; }

    public ICommand ChooseDestinationCommand { get; }

    public ICommand ConvertCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand RemoveCommand { get; }

    public ICommand OpenDestinationCommand { get; }

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        OutputFormat = settings.Conversion.OutputFormat;
        ColorHandling = settings.Conversion.ColorHandling;
        JpegQuality = settings.Conversion.JpegQuality;
        DestinationDirectory = settings.Conversion.DestinationDirectory;
        _lastSourceDirectory = settings.LastSourceDirectory;

        var missingFormats = _imageInspector.GetMissingRequiredFormats();
        StatusMessage = missingFormats.Count == 0
            ? "Ready. Drop files or use Add files."
            : $"Codec warning: {string.Join(", ", missingFormats)} support is unavailable in this build.";
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        if (IsBusy)
        {
            return;
        }

        var pathList = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (pathList.Length == 0)
        {
            return;
        }

        var existingPaths = Items.Select(item => item.Path);
        var result = _intakeService.Collect(pathList, existingPaths);
        foreach (var rejected in result.RejectedSources)
        {
            Items.Add(new QueueItemViewModel(new SourceItem
            {
                Path = rejected.Path,
                DisplayName = Path.GetFileName(rejected.Path),
                Format = Path.GetExtension(rejected.Path).TrimStart('.').ToUpperInvariant(),
                Status = SourceStatus.Failed,
                Diagnostic = rejected.Reason
            }));
        }

        foreach (var path in result.AcceptedPaths)
        {
            StatusMessage = $"Inspecting {Path.GetFileName(path)}…";
            Items.Add(new QueueItemViewModel(await _imageInspector.InspectAsync(path)));
        }

        var firstPath = pathList[0];
        _lastSourceDirectory = Directory.Exists(firstPath)
            ? firstPath
            : Path.GetDirectoryName(firstPath) ?? _lastSourceDirectory;
        StatusMessage = result.AcceptedPaths.Count == 0 && result.RejectedSources.Count == 0
            ? "Those files are already in the queue."
            : $"Added {result.AcceptedPaths.Count} image{(result.AcceptedPaths.Count == 1 ? string.Empty : "s")}.";
        CommandManager.InvalidateRequerySuggested();
    }

    public async Task SaveAsync()
    {
        await _settingsStore.SaveAsync(new AppSettings
        {
            LastSourceDirectory = _lastSourceDirectory,
            Conversion = CreateOptions()
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task AddFilesFromDialogAsync()
    {
        var files = _dialogService.SelectImageFiles(_lastSourceDirectory);
        await AddPathsAsync(files);
    }

    private async Task AddFolderFromDialogAsync()
    {
        var folder = _dialogService.SelectFolder(_lastSourceDirectory, "Add a customer folder");
        if (folder is not null)
        {
            await AddPathsAsync([folder]);
        }
    }

    private void ChooseDestination()
    {
        var folder = _dialogService.SelectFolder(DestinationDirectory, "Choose the converted-files folder");
        if (folder is not null)
        {
            DestinationDirectory = folder;
            StatusMessage = "Output folder selected.";
        }
    }

    private async Task ConvertAllAsync()
    {
        var candidates = Items
            .Where(item => item.Status is not SourceStatus.Failed and not SourceStatus.Completed)
            .ToArray();
        if (candidates.Length == 0 || string.IsNullOrWhiteSpace(DestinationDirectory))
        {
            StatusMessage = "Add at least one readable image and choose an output folder.";
            return;
        }

        IsBusy = true;
        ProgressPercentage = 0;
        _conversionCancellation = new CancellationTokenSource();
        var completed = 0;

        try
        {
            for (var itemIndex = 0; itemIndex < candidates.Length; itemIndex++)
            {
                var currentItemIndex = itemIndex;
                var item = candidates[itemIndex];
                item.Status = SourceStatus.Converting;
                item.Diagnostic = string.Empty;

                var progress = new Progress<ConversionProgress>(update =>
                {
                    var frameProgress = update.TotalFrames == 0
                        ? 0
                        : (double)update.CompletedFrames / update.TotalFrames;
                    ProgressPercentage = ((currentItemIndex + frameProgress) / candidates.Length) * 100;
                    StatusMessage = $"{item.DisplayName}: {update.Message}";
                });

                var result = await _conversionService.ConvertAsync(
                    item.Path,
                    CreateOptions(),
                    progress,
                    _conversionCancellation.Token);

                if (result.Succeeded)
                {
                    completed++;
                    item.Status = result.Warnings.Count == 0
                        ? SourceStatus.Completed
                        : SourceStatus.Warning;
                    item.Diagnostic = result.Warnings.Count == 0
                        ? $"Created {result.OutputPaths.Count} file{(result.OutputPaths.Count == 1 ? string.Empty : "s")}."
                        : string.Join(" ", result.Warnings);
                }
                else
                {
                    item.Status = SourceStatus.Failed;
                    item.Diagnostic = result.Error ?? "Conversion failed.";
                }
            }

            ProgressPercentage = 100;
            StatusMessage = $"Finished: {completed} of {candidates.Length} image{(candidates.Length == 1 ? string.Empty : "s")} converted.";
            await SaveAsync();
        }
        catch (OperationCanceledException)
        {
            var converting = candidates.FirstOrDefault(item => item.Status == SourceStatus.Converting);
            if (converting is not null)
            {
                converting.Status = SourceStatus.Cancelled;
                converting.Diagnostic = "Conversion was cancelled; completed outputs were kept.";
            }

            StatusMessage = "Conversion cancelled. Completed files were kept.";
        }
        finally
        {
            _conversionCancellation.Dispose();
            _conversionCancellation = null;
            IsBusy = false;
        }
    }

    private bool CanConvert() =>
        !IsBusy &&
        Items.Any(item => item.Status is not SourceStatus.Failed and not SourceStatus.Completed) &&
        !string.IsNullOrWhiteSpace(DestinationDirectory);

    private ConversionOptions CreateOptions() => new()
    {
        OutputFormat = OutputFormat,
        JpegQuality = JpegQuality,
        ColorHandling = ColorHandling,
        BackgroundColor = "#FFFFFF",
        DestinationDirectory = DestinationDirectory
    };

    private void Clear()
    {
        Items.Clear();
        ProgressPercentage = 0;
        StatusMessage = "Queue cleared.";
        CommandManager.InvalidateRequerySuggested();
    }

    private void Remove(object? parameter)
    {
        if (parameter is QueueItemViewModel item)
        {
            Items.Remove(item);
            StatusMessage = "Image removed from the queue.";
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OpenDestination()
    {
        try
        {
            _dialogService.OpenFolder(DestinationDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = exception.Message;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
