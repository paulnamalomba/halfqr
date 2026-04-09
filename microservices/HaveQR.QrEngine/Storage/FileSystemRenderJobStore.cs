using System.Text.Json;
using HaveQR.Contracts.Models;
using HaveQR.Contracts.Options;
using Microsoft.Extensions.Options;

namespace HaveQR.QrEngine.Storage;

public sealed class FileSystemRenderJobStore(IOptions<RenderStorageOptions> options) : IRenderJobStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _rootPath = options.Value.RootPath;

    public async Task SaveAsync(RenderJobState state, CancellationToken cancellationToken)
    {
        var jobDirectory = EnsureJobDirectory(state.JobId);
        var jobFilePath = Path.Combine(jobDirectory, "job.json");
        var json = JsonSerializer.Serialize(state, SerializerOptions);
        await File.WriteAllTextAsync(jobFilePath, json, cancellationToken);
    }

    public async Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var jobFilePath = GetJobFilePath(jobId);

        if (!File.Exists(jobFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(jobFilePath, cancellationToken);
        return JsonSerializer.Deserialize<RenderJobState>(json, SerializerOptions);
    }

    public async Task<RenderArtifactState> SaveArtifactAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var artifactsDirectory = EnsureArtifactsDirectory(jobId);
        var normalizedFormat = format.Trim().ToLowerInvariant();
        var fileName = normalizedFormat switch
        {
            "svg" => "qr.svg",
            "png" => "qr.png",
            _ => $"artifact.{normalizedFormat}",
        };

        var absolutePath = Path.Combine(artifactsDirectory, fileName);
        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);

        return new RenderArtifactState
        {
            Format = normalizedFormat,
            RelativePath = Path.GetRelativePath(_rootPath, absolutePath),
            ContentType = contentType,
            SizeBytes = content.LongLength,
        };
    }

    public async Task<(byte[] Content, string ContentType)?> GetArtifactAsync(Guid jobId, string format, CancellationToken cancellationToken)
    {
        var state = await GetAsync(jobId, cancellationToken);
        var artifact = state?.Artifacts.FirstOrDefault(existing => existing.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

        if (artifact is null)
        {
            return null;
        }

        var absolutePath = Path.Combine(_rootPath, artifact.RelativePath);

        if (!File.Exists(absolutePath))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        return (content, artifact.ContentType);
    }

    private string EnsureJobDirectory(Guid jobId)
    {
        Directory.CreateDirectory(_rootPath);
        var jobDirectory = Path.Combine(_rootPath, jobId.ToString("N"));
        Directory.CreateDirectory(jobDirectory);
        return jobDirectory;
    }

    private string EnsureArtifactsDirectory(Guid jobId)
    {
        var jobDirectory = EnsureJobDirectory(jobId);
        var artifactsDirectory = Path.Combine(jobDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);
        return artifactsDirectory;
    }

    private string GetJobFilePath(Guid jobId)
        => Path.Combine(_rootPath, jobId.ToString("N"), "job.json");
}