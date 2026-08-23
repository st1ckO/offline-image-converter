namespace PrintShopImageConverter.Conversion;

public interface IOutputPathResolver
{
    string GetUniquePath(string directory, string filenameWithoutExtension, string extension);
}

public sealed class OutputPathResolver : IOutputPathResolver
{
    public string GetUniquePath(string directory, string filenameWithoutExtension, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filenameWithoutExtension);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalizedExtension = extension.StartsWith('.') ? extension : "." + extension;
        var candidate = Path.Combine(directory, filenameWithoutExtension + normalizedExtension);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            candidate = Path.Combine(
                directory,
                $"{filenameWithoutExtension}_{suffix}{normalizedExtension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No available output filename could be found.");
    }
}
