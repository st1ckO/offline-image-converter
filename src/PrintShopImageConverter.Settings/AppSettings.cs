using PrintShopImageConverter.Conversion;

namespace PrintShopImageConverter.Settings;

public sealed record AppSettings
{
    public ConversionOptions Conversion { get; init; } = new();

    public string LastSourceDirectory { get; init; } = string.Empty;

    public AppSettings Normalize() => this with
    {
        Conversion = (Conversion ?? new ConversionOptions()).Normalize(),
        LastSourceDirectory = LastSourceDirectory?.Trim() ?? string.Empty
    };
}
