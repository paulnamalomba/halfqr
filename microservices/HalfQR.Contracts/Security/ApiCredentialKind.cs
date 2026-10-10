using System.Text.Json.Serialization;

namespace HalfQR.Contracts.Security;

// Serialized as a string everywhere so services agree regardless of their JSON options.
[JsonConverter(typeof(JsonStringEnumConverter<ApiCredentialKind>))]
public enum ApiCredentialKind
{
    // Long-lived server credential, optional expiry.
    ApiKey,

    // Short-lived personal token, expiry required.
    AccessToken,
}
