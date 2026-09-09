namespace PrintShopImageConverter.Conversion;

public sealed record RejectedSource(string Path, string Reason);

public sealed record IntakeResult(
    IReadOnlyList<string> AcceptedPaths,
    IReadOnlyList<RejectedSource> RejectedSources);

public interface IFileIntakeService
{
    IntakeResult Collect(IEnumerable<string> droppedPaths, IEnumerable<string>? existingPaths = null);
}

public sealed class FileIntakeService : IFileIntakeService
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".heic", ".heif", ".avif", ".webp", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".gif", ".pdf"],
        StringComparer.OrdinalIgnoreCase);

    public IntakeResult Collect(IEnumerable<string> droppedPaths, IEnumerable<string>? existingPaths = null)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existingPath in existingPaths ?? [])
        {
            TryAddNormalized(existingPath, seen);
        }

        var accepted = new List<string>();
        var rejected = new List<RejectedSource>();

        foreach (var rawPath in droppedPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                AddFile(path, seen, accepted, rejected);
                continue;
            }

            if (Directory.Exists(path))
            {
                AddDirectory(path, seen, accepted, rejected);
                continue;
            }

            rejected.Add(new RejectedSource(path, "The file or folder does not exist."));
        }

        return new IntakeResult(accepted, rejected);
    }

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static void AddDirectory(
        string directory,
        HashSet<string> seen,
        List<string> accepted,
        List<RejectedSource> rejected)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AddFile(file, seen, accepted, rejected);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            rejected.Add(new RejectedSource(directory, "The folder could not be read."));
        }
    }

    private static void AddFile(
        string path,
        HashSet<string> seen,
        List<string> accepted,
        List<RejectedSource> rejected)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (!IsSupported(normalizedPath))
        {
            rejected.Add(new RejectedSource(normalizedPath, "Unsupported file format."));
            return;
        }

        if (seen.Add(normalizedPath))
        {
            accepted.Add(normalizedPath);
        }
    }

    private static void TryAddNormalized(string path, HashSet<string> seen)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            seen.Add(Path.GetFullPath(path));
        }
    }
}
