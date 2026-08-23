using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PrintShopImageConverter.Infrastructure;

public interface IWindowsDialogService
{
    IReadOnlyList<string> SelectImageFiles(string initialDirectory);

    string? SelectFolder(string initialDirectory, string title);

    void OpenFolder(string path);
}

public sealed class WindowsDialogService : IWindowsDialogService
{
    private const string ImageFilter =
        "Supported images|*.heic;*.heif;*.avif;*.webp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.gif|All files|*.*";

    public IReadOnlyList<string> SelectImageFiles(string initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add customer images",
            Filter = ImageFilter,
            Multiselect = true,
            CheckFileExists = true
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? SelectFolder(string initialDirectory, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException("The output folder no longer exists.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
