namespace PrintShopImageConverter.Conversion;

public enum SourceStatus
{
    Ready,
    Converting,
    Completed,
    Warning,
    Failed,
    Cancelled
}

public sealed record SourceItem
{
    public required string Path { get; init; }

    public required string DisplayName { get; init; }

    public required string Format { get; init; }

    public uint Width { get; init; }

    public uint Height { get; init; }

    public int FrameCount { get; init; }

    public bool HasAlpha { get; init; }

    public byte[]? ThumbnailPng { get; init; }

    public SourceStatus Status { get; init; } = SourceStatus.Ready;

    public string Diagnostic { get; init; } = string.Empty;
}
