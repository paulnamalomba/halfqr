using System.Text.Json;
using System.Text.Json.Serialization;

namespace HalfQR.Billing.Storage;

internal static class BillingJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
