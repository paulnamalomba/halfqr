using HalfQR.Contracts.Enums;

namespace HalfQR.Contracts.Models;

public sealed record QrFinderOptions
{
    public QrFinderShape BorderShape { get; init; } = QrFinderShape.Rounded;

    public QrFinderShape CenterShape { get; init; } = QrFinderShape.Circle;
}