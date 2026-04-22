namespace HalfQR.Contracts.Responses;

public sealed record RenderArtifactDescriptor
{
    public string Format { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;
}