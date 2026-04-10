using HaveQR.Contracts.Enums;

namespace HaveQR.Contracts.Models;

public sealed record QrLogoOptions
{
    public QrLogoSourceType SourceType { get; init; } = QrLogoSourceType.Svg;

    public string? Svg { get; init; }

    public string? ContentBase64 { get; init; }

    public string? ContentType { get; init; }

    public int SizePercent { get; init; } = 18;

    public bool RemoveBackground { get; init; } = true;

    public int BackdropPaddingPercent { get; init; } = 40;
}