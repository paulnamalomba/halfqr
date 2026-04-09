using System.Text.Json;

namespace HaveQR.QrEngine.Storage;

internal static class RenderJobStateJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}