using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using HaveQR.Contracts.Models;
using HaveQR.Contracts.Options;
using Microsoft.Extensions.Options;

namespace HaveQR.QrEngine.Storage;

public sealed class R2RenderArtifactStore(IOptions<R2StorageOptions> options) : IRenderArtifactStore, IDisposable
{
    private readonly R2StorageOptions _options = options.Value;
    private readonly IAmazonS3 _client = CreateClient(options.Value);

    public async Task<RenderArtifactState> SaveAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var normalizedFormat = RenderArtifactNaming.NormalizeFormat(format);
        var objectKey = BuildObjectKey(jobId, normalizedFormat);

        using var stream = new MemoryStream(content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };

        await _client.PutObjectAsync(request, cancellationToken);

        return new RenderArtifactState
        {
            Format = normalizedFormat,
            RelativePath = objectKey,
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

        try
        {
            using var response = await _client.GetObjectAsync(_options.BucketName, artifact.RelativePath, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);

            return (
                buffer.ToArray(),
                string.IsNullOrWhiteSpace(response.Headers.ContentType) ? artifact.ContentType : response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public void Dispose()
        => _client.Dispose();

    private string BuildObjectKey(Guid jobId, string normalizedFormat)
    {
        var prefix = _options.KeyPrefix.Trim('/');
        var fileName = RenderArtifactNaming.GetFileName(normalizedFormat);
        return string.IsNullOrWhiteSpace(prefix)
            ? $"{jobId:N}/artifacts/{fileName}"
            : $"{prefix}/{jobId:N}/artifacts/{fileName}";
    }

    private static IAmazonS3 CreateClient(R2StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException("R2 artifact storage is enabled but R2Storage:BucketName is empty.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId) || string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            throw new InvalidOperationException("R2 artifact storage is enabled but R2Storage credentials are incomplete.");
        }

        var serviceUrl = ResolveServiceUrl(options);
        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
        };

        return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
    }

    private static string ResolveServiceUrl(R2StorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return options.Endpoint.Trim().TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(options.AccountId))
        {
            throw new InvalidOperationException("R2 artifact storage is enabled but neither R2Storage:Endpoint nor R2Storage:AccountId is configured.");
        }

        return $"https://{options.AccountId.Trim()}.r2.cloudflarestorage.com";
    }
}