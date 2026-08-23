using System.ComponentModel;
using System.Windows;
using PrintShopImageConverter.Conversion;
using PrintShopImageConverter.Infrastructure;
using PrintShopImageConverter.Settings;
using PrintShopImageConverter.ViewModels;

namespace PrintShopImageConverter;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new FileIntakeService(),
            new ImageInspector(),
            new ImageConversionService(),
            new JsonSettingsStore(),
            new WindowsDialogService());
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await ViewModel.AddPathsAsync(paths);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        await ViewModel.SaveAsync();
        _allowClose = true;
        Close();
    }
}
