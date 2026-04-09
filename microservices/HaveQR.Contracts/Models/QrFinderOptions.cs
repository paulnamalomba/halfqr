using HaveQR.Contracts.Enums;

namespace HaveQR.Contracts.Models;

public sealed record QrFinderOptions
{
    public QrFinderShape BorderShape { get; init; } = QrFinderShape.Rounded;

    public QrFinderShape CenterShape { get; init; } = QrFinderShape.Circle;
}