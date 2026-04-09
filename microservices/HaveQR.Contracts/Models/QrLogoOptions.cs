namespace HaveQR.Contracts.Models;

public sealed record QrLogoOptions
{
    public string? Svg { get; init; }

    public int SizePercent { get; init; } = 18;
}