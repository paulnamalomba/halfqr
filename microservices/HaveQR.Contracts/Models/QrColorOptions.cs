namespace HaveQR.Contracts.Models;

public sealed record QrColorOptions
{
    public string Dark { get; init; } = "#000000";

    public string Light { get; init; } = "#FFFFFF";
}