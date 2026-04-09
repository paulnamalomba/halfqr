using HaveQR.Contracts.Models;
using HaveQR.Contracts.Options;
using Microsoft.Extensions.Options;

namespace HaveQR.QrEngine.Storage;

public sealed class FileSystemRenderArtifactStore(IOptions<RenderStorageOptions> options) : IRenderArtifactStore
{
    private readonly string _rootPath = options.Value.RootPath;

    public async Task<RenderArtifactState> SaveAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var artifactsDirectory = FileSystemRenderStorageLayout.EnsureArtifactsDirectory(_rootPath, jobId);
        var normalizedFormat = RenderArtifactNaming.NormalizeFormat(format);
        var fileName = RenderArtifactNaming.GetFileName(normalizedFormat);
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

    public async Task<(byte[] Content, string ContentType)?> GetAsync(RenderArtifactState artifact, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifact.RelativePath))
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
}