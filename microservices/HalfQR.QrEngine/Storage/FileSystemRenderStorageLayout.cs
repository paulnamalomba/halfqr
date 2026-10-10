namespace HalfQR.QrEngine.Storage;

internal static class FileSystemRenderStorageLayout
{
    public const string JobFileName = "job.json";

    public static string EnsureJobDirectory(string rootPath, Guid jobId)
    {
        Directory.CreateDirectory(rootPath);
        var jobDirectory = Path.Combine(rootPath, jobId.ToString("N"));
        Directory.CreateDirectory(jobDirectory);
        return jobDirectory;
    }

    public static string EnsureArtifactsDirectory(string rootPath, Guid jobId)
    {
        var jobDirectory = EnsureJobDirectory(rootPath, jobId);
        var artifactsDirectory = Path.Combine(jobDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);
        return artifactsDirectory;
    }

    public static string GetJobFilePath(string rootPath, Guid jobId)
        => Path.Combine(rootPath, jobId.ToString("N"), JobFileName);
}