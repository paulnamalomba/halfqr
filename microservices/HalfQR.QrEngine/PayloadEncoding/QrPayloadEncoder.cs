using HalfQR.Contracts.Enums;

namespace HalfQR.QrEngine.PayloadEncoding;

public sealed class QrPayloadEncoder : IQrPayloadEncoder
{
    public string Encode(QrContentType contentType, string? targetUrl, IReadOnlyDictionary<string, string?> payload)
        => contentType == QrContentType.WhatsApp
            ? BuildWhatsAppPayload(targetUrl, payload)
            : BuildUrlPayload(targetUrl);

    private static string BuildUrlPayload(string? targetUrl)
        => NormalizeAbsoluteUrl(RequireTargetUrl(targetUrl));

    private static string BuildWhatsAppPayload(string? targetUrl, IReadOnlyDictionary<string, string?> payload)
    {
        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            return NormalizeAbsoluteUrl(targetUrl);
        }

        if (payload.TryGetValue("url", out var payloadUrl) && !string.IsNullOrWhiteSpace(payloadUrl))
        {
            return NormalizeAbsoluteUrl(payloadUrl);
        }

        var phone = RequirePayload(payload, "phone");
        var normalizedPhone = new string(phone.Where(static character => char.IsDigit(character) || character == '+').ToArray());

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new InvalidOperationException("Payload field 'phone' must contain at least one digit.");
        }

        var message = payload.TryGetValue("message", out var rawMessage) && !string.IsNullOrWhiteSpace(rawMessage)
            ? rawMessage.Trim()
            : null;
        var baseUrl = $"https://wa.me/{normalizedPhone}";

        return string.IsNullOrWhiteSpace(message)
            ? baseUrl
            : $"{baseUrl}?text={Uri.EscapeDataString(message)}";
    }

    private static string RequireTargetUrl(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            throw new InvalidOperationException("TargetUrl is required for this content type.");
        }

        return targetUrl.Trim();
    }

    private static string RequirePayload(IReadOnlyDictionary<string, string?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Payload field '{key}' is required.");
        }

        return value.Trim();
    }

    private static string NormalizeAbsoluteUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("TargetUrl must be a valid absolute URL.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("TargetUrl must use http or https.");
        }

        return uri.ToString();
    }
}