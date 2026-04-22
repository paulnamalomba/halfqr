namespace HalfQR.Contracts.Options;

public sealed class PostgresRenderStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string Schema { get; set; } = "public";

    public string TableName { get; set; } = "render_jobs";
}