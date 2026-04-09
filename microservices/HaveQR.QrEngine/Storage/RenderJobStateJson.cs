using System.Text.Json;
using System.Text.Json.Serialization;

namespace HaveQR.QrEngine.Storage;

internal static class RenderJobStateJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    static RenderJobStateJson()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
}