namespace HaveQR.Contracts.Options;

public sealed class RenderStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), ".data", "render-jobs");
}