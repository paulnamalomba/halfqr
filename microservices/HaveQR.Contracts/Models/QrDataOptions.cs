using HaveQR.Contracts.Enums;

namespace HaveQR.Contracts.Models;

public sealed record QrDataOptions
{
    public QrDataPattern Pattern { get; init; } = QrDataPattern.Square;

    public QrGradientMode GradientMode { get; init; } = QrGradientMode.None;

    public string? GradientStart { get; init; }

    public string? GradientEnd { get; init; }

    public int GradientRotation { get; init; } = 135;
}