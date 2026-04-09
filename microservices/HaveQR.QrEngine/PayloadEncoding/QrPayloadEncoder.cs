using System.Globalization;
using System.Text;
using HaveQR.Contracts.Enums;

namespace HaveQR.QrEngine.PayloadEncoding;

public sealed class QrPayloadEncoder : IQrPayloadEncoder
{
    public string Encode(QrContentType contentType, IReadOnlyDictionary<string, string?> payload)
    {
        return contentType switch
        {
            QrContentType.Link => BuildUrlPayload(payload),
            QrContentType.Text => Require(payload, "text"),
            QrContentType.Email => BuildEmailPayload(payload),
            QrContentType.Call => $"tel:{NormalizePhone(Require(payload, "phone"))}",
            QrContentType.Sms => BuildSmsPayload(payload),
            QrContentType.WhatsApp => BuildWhatsAppPayload(payload),
            QrContentType.VCard => BuildVCardPayload(payload),
            QrContentType.WiFi => BuildWifiPayload(payload),
            QrContentType.Event => BuildEventPayload(payload),
            QrContentType.App => BuildUrlPayload(payload),
            QrContentType.Social => BuildUrlPayload(payload),
            QrContentType.Pdf => BuildUrlPayload(payload),
            QrContentType.Image => BuildUrlPayload(payload),
            QrContentType.Video => BuildUrlPayload(payload),
            _ => throw new InvalidOperationException($"Unsupported QR content type: {contentType}"),
        };
    }

    private static string BuildUrlPayload(IReadOnlyDictionary<string, string?> payload)
        => NormalizeAbsoluteUrl(Require(payload, "url"));

    private static string BuildEmailPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var email = Require(payload, "email");
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["subject"] = Optional(payload, "subject"),
            ["body"] = Optional(payload, "body"),
        });

        return string.IsNullOrEmpty(query)
            ? $"mailto:{email}"
            : $"mailto:{email}?{query}";
    }

    private static string BuildSmsPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var phone = NormalizePhone(Require(payload, "phone"));
        var message = Optional(payload, "message");
        return string.IsNullOrWhiteSpace(message)
            ? $"SMSTO:{phone}"
            : $"SMSTO:{phone}:{message}";
    }

    private static string BuildWhatsAppPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var phone = NormalizePhone(Require(payload, "phone"));
        var message = Optional(payload, "message");
        var baseUrl = $"https://wa.me/{phone}";

        return string.IsNullOrWhiteSpace(message)
            ? baseUrl
            : $"{baseUrl}?text={Uri.EscapeDataString(message)}";
    }

    private static string BuildVCardPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var firstName = Require(payload, "firstName");
        var lastName = Optional(payload, "lastName") ?? string.Empty;
        var organization = Optional(payload, "organization") ?? string.Empty;
        var phone = Optional(payload, "phone") ?? string.Empty;
        var email = Optional(payload, "email") ?? string.Empty;
        var url = Optional(payload, "url") ?? string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCARD");
        builder.AppendLine("VERSION:3.0");
        builder.AppendLine($"N:{EscapeVCard(lastName)};{EscapeVCard(firstName)};;;");
        builder.AppendLine($"FN:{EscapeVCard($"{firstName} {lastName}".Trim())}");

        if (!string.IsNullOrWhiteSpace(organization))
        {
            builder.AppendLine($"ORG:{EscapeVCard(organization)}");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            builder.AppendLine($"TEL;TYPE=CELL:{EscapeVCard(phone)}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            builder.AppendLine($"EMAIL:{EscapeVCard(email)}");
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            builder.AppendLine($"URL:{EscapeVCard(NormalizeAbsoluteUrl(url))}");
        }

        builder.Append("END:VCARD");
        return builder.ToString();
    }

    private static string BuildWifiPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var security = Optional(payload, "security") ?? "WPA";
        var ssid = Require(payload, "ssid");
        var password = Optional(payload, "password") ?? string.Empty;
        var hidden = Optional(payload, "hidden")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        return $"WIFI:T:{EscapeWifi(security)};S:{EscapeWifi(ssid)};P:{EscapeWifi(password)};H:{hidden.ToString().ToLowerInvariant()};;";
    }

    private static string BuildEventPayload(IReadOnlyDictionary<string, string?> payload)
    {
        var title = Require(payload, "title");
        var startsAt = ParseDate(Require(payload, "startsAtUtc"))
            ?? throw new InvalidOperationException("Payload field 'startsAtUtc' is required.");
        var endsAt = ParseDate(Optional(payload, "endsAtUtc")) ?? startsAt.AddHours(1);
        var description = Optional(payload, "description") ?? string.Empty;
        var location = Optional(payload, "location") ?? string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("BEGIN:VEVENT");
        builder.AppendLine($"SUMMARY:{EscapeCalendar(title)}");
        builder.AppendLine($"DTSTART:{startsAt:yyyyMMdd'T'HHmmss'Z'}");
        builder.AppendLine($"DTEND:{endsAt:yyyyMMdd'T'HHmmss'Z'}");

        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine($"DESCRIPTION:{EscapeCalendar(description)}");
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            builder.AppendLine($"LOCATION:{EscapeCalendar(location)}");
        }

        builder.AppendLine("END:VEVENT");
        builder.Append("END:VCALENDAR");
        return builder.ToString();
    }

    private static string Require(IReadOnlyDictionary<string, string?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Payload field '{key}' is required.");
        }

        return value.Trim();
    }

    private static string? Optional(IReadOnlyDictionary<string, string?> payload, string key)
        => payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string NormalizeAbsoluteUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Payload field 'url' must be a valid absolute URL.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Payload field 'url' must use http or https.");
        }

        return uri.ToString();
    }

    private static string NormalizePhone(string phone)
    {
        var normalized = new string(phone.Where(static character => char.IsDigit(character) || character == '+').ToArray());

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Payload field 'phone' must contain at least one digit.");
        }

        return normalized;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            throw new InvalidOperationException("Event payload dates must be valid ISO-8601 timestamps.");
        }

        return parsed.ToUniversalTime();
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> values)
    {
        var pairs = values
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Value))
            .Select(static entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value!)}")
            .ToArray();

        return string.Join("&", pairs);
    }

    private static string EscapeVCard(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string EscapeCalendar(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string EscapeWifi(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}