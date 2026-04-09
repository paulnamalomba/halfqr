using HaveQR.Worker;
using HaveQR.Contracts.Options;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;
using HaveQR.QrEngine.Rendering;
using HaveQR.QrEngine.Storage;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<RenderStorageOptions>(builder.Configuration.GetSection("RenderStorage"));
builder.Services.Configure<PostgresRenderStoreOptions>(builder.Configuration.GetSection("PostgresRenderStore"));
builder.Services.Configure<R2StorageOptions>(builder.Configuration.GetSection("R2Storage"));
builder.Services.AddSingleton<IQrPayloadEncoder, QrPayloadEncoder>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();
builder.Services.AddSingleton<IQrRenderService, QrRenderService>();

if (string.Equals(builder.Configuration["RenderStorage:JobStateProvider"], RenderStorageOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IRenderJobStateStore, PostgresRenderJobStateStore>();
}
else
{
	builder.Services.AddSingleton<IRenderJobStateStore, FileSystemRenderJobStateStore>();
}

if (string.Equals(builder.Configuration["RenderStorage:ArtifactProvider"], RenderStorageOptions.R2Provider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IRenderArtifactStore, R2RenderArtifactStore>();
}
else
{
	builder.Services.AddSingleton<IRenderArtifactStore, FileSystemRenderArtifactStore>();
}

builder.Services.AddSingleton<IRenderJobStore, CompositeRenderJobStore>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
