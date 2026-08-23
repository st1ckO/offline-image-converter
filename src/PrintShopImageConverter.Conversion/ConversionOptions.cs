namespace PrintShopImageConverter.Conversion;

public enum OutputFormat
{
    Jpeg,
    Png
}
public enum ColorHandling
{
    ConvertToSrgb,
    PreserveSourceProfile
}

public sealed record ConversionOptions
{
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Jpeg;

    public int JpegQuality { get; init; } = 95;

    public ColorHandling ColorHandling { get; init; } = ColorHandling.ConvertToSrgb;

    public string BackgroundColor { get; init; } = "#FFFFFF";

    public string DestinationDirectory { get; init; } = string.Empty;

    public ConversionOptions Normalize() => this with
    {
        JpegQuality = Math.Clamp(JpegQuality, 1, 100),
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? "#FFFFFF" : BackgroundColor,
        DestinationDirectory = DestinationDirectory?.Trim() ?? string.Empty
    };
}
