namespace HaveQR.Contracts.Options;

public sealed class R2StorageOptions
{
    public string BucketName { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "render-jobs";
}