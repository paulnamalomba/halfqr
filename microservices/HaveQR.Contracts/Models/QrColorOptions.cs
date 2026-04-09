namespace HaveQR.Contracts.Models;

public sealed record QrColorOptions
{
    public string Dark { get; init; } = "#0B1F3A";

    public string Light { get; init; } = "#FFFFFF";
}