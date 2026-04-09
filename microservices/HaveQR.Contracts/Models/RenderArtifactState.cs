namespace HaveQR.Contracts.Models;

public sealed record RenderArtifactState
{
    public string Format { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long SizeBytes { get; init; }
}