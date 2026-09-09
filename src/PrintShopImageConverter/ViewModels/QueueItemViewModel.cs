using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintShopImageConverter.Conversion;

namespace PrintShopImageConverter.ViewModels;

public sealed class QueueItemViewModel : INotifyPropertyChanged
{
    private SourceStatus _status;
    private string _diagnostic;

    public QueueItemViewModel(SourceItem source)
    {
        Source = source;
        _status = source.Status;
        _diagnostic = source.Diagnostic;
        Thumbnail = CreateThumbnail(source.ThumbnailPng);
    }

    public SourceItem Source { get; }

    public string Path => Source.Path;

    public string DisplayName => Source.DisplayName;

    public ImageSource? Thumbnail { get; }

    public string Details => Source.FrameCount > 0
        ? $"{Source.Width:N0} × {Source.Height:N0}  •  {Source.Format}  •  {Source.FrameCount} {ItemUnit}"
        : Source.Format;

    private string ItemUnit => Source.Format.Equals("PDF", StringComparison.OrdinalIgnoreCase)
        ? $"page{(Source.FrameCount == 1 ? string.Empty : "s")}"
        : $"frame{(Source.FrameCount == 1 ? string.Empty : "s")}";

    public SourceStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public string StatusText => Status switch
    {
        SourceStatus.Ready => "Ready",
        SourceStatus.Converting => "Converting",
        SourceStatus.Completed => "Completed",
        SourceStatus.Warning => "Completed with warning",
        SourceStatus.Failed => "Needs attention",
        SourceStatus.Cancelled => "Cancelled",
        _ => Status.ToString()
    };

    public Brush StatusBrush => Status switch
    {
        SourceStatus.Completed => Brushes.SeaGreen,
        SourceStatus.Warning => Brushes.DarkOrange,
        SourceStatus.Failed => Brushes.Firebrick,
        SourceStatus.Cancelled => Brushes.DimGray,
        SourceStatus.Converting => Brushes.RoyalBlue,
        _ => Brushes.SlateGray
    };

    public string Diagnostic
    {
        get => _diagnostic;
        set
        {
            if (_diagnostic == value)
            {
                return;
            }

            _diagnostic = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static ImageSource? CreateThumbnail(byte[]? png)
    {
        if (png is null || png.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
