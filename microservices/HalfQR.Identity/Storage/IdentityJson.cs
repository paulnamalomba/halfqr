using System.Text.Json;
using System.Text.Json.Serialization;

namespace HalfQR.Identity.Storage;

internal static class IdentityJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
