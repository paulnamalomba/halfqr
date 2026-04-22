namespace HalfQR.Contracts.Models;

public sealed record QrOutputOptions
{
    public string Format { get; init; } = "png";

    public int SizePx { get; init; } = 1024;
}