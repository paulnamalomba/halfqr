namespace HaveQR.QrEngine.Storage;

internal static class RenderArtifactNaming
{
    public static string NormalizeFormat(string format)
        => string.IsNullOrWhiteSpace(format)
            ? throw new InvalidOperationException("Artifact format is required.")
            : format.Trim().ToLowerInvariant();

    public static string GetFileName(string normalizedFormat)
        => normalizedFormat switch
        {
            "svg" => "qr.svg",
            "png" => "qr.png",
            _ => $"artifact.{normalizedFormat}",
        };
}