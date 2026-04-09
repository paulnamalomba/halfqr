namespace HaveQR.Contracts.Options;

public sealed class RenderStorageOptions
{
    public const string FileSystemProvider = "FileSystem";

    public const string PostgreSqlProvider = "PostgreSql";

    public const string R2Provider = "R2";

    public string JobStateProvider { get; set; } = FileSystemProvider;

    public string ArtifactProvider { get; set; } = FileSystemProvider;

    public string RootPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), ".data", "render-jobs");
}