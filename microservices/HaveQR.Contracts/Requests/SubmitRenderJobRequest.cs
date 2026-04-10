using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Models;

namespace HaveQR.Contracts.Requests;

public sealed record SubmitRenderJobRequest
{
    public QrContentType ContentType { get; init; } = QrContentType.Link;

    public string? TargetUrl { get; init; }

    public Dictionary<string, string?> Payload { get; init; } = [];

    public QrRenderMode Mode { get; init; } = QrRenderMode.Static;

    public QrErrorCorrectionLevel ErrorCorrectionLevel { get; init; } = QrErrorCorrectionLevel.H;

    public QrOutputOptions Output { get; init; } = new();

    public QrLogoOptions? Logo { get; init; }

    public QrFinderOptions Finder { get; init; } = new();

    public QrColorOptions Colors { get; init; } = new();

    public QrDataOptions Data { get; init; } = new();
}