using System.Text.Json;
using HalfQR.Contracts.Models;
using HalfQR.Contracts.Options;
using Microsoft.Extensions.Options;

namespace HalfQR.QrEngine.Storage;

public sealed class FileSystemRenderJobStateStore(IOptions<RenderStorageOptions> options) : IRenderJobStateStore
{
    private readonly string _rootPath = options.Value.RootPath;

    public async Task SaveAsync(RenderJobState state, CancellationToken cancellationToken)
    {
        var jobDirectory = FileSystemRenderStorageLayout.EnsureJobDirectory(_rootPath, state.JobId);
        var jobFilePath = Path.Combine(jobDirectory, FileSystemRenderStorageLayout.JobFileName);
        var json = JsonSerializer.Serialize(state, RenderJobStateJson.SerializerOptions);
        await File.WriteAllTextAsync(jobFilePath, json, cancellationToken);
    }

    public async Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var jobFilePath = FileSystemRenderStorageLayout.GetJobFilePath(_rootPath, jobId);

        if (!File.Exists(jobFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(jobFilePath, cancellationToken);
        return JsonSerializer.Deserialize<RenderJobState>(json, RenderJobStateJson.SerializerOptions);
    }
}