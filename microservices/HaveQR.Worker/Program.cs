using HaveQR.Worker;
using HaveQR.Contracts.Options;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;
using HaveQR.QrEngine.Rendering;
using HaveQR.QrEngine.Storage;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<RenderStorageOptions>(builder.Configuration.GetSection("RenderStorage"));
builder.Services.AddSingleton<IQrPayloadEncoder, QrPayloadEncoder>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();
builder.Services.AddSingleton<IQrRenderService, QrRenderService>();
builder.Services.AddSingleton<IRenderJobStore, FileSystemRenderJobStore>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
