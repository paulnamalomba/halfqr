using System.Text.RegularExpressions;
using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Requests;

namespace HalfQR.QrEngine.Rendering;

public static partial class QrRenderRequestValidator
{
    public const int MinOutputSizePx = 256;
    public const int MaxOutputSizePx = 4096;
    public const int MaxSvgLogoCharacters = 120_000;
    public const int MaxRasterLogoBytes = 512 * 1024;
    public const int MinLogoSizePercent = 12;
    public const int MaxLogoSizePercent = 24;
    public const int MinBackdropPaddingPercent = 10;
    public const int MaxBackdropPaddingPercent = 80;
    public const int MaxTargetUrlCharacters = 2_048;
    public const int MaxPayloadEntries = 16;
    public const int MaxPayloadKeyCharacters = 64;
    public const int MaxPayloadValueCharacters = 2_048;

    public static Dictionary<string, string[]> Validate(SubmitRenderJobRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request.Output.SizePx < MinOutputSizePx || request.Output.SizePx > MaxOutputSizePx)
        {
            AddError(errors, "output.sizePx", $"PNG size must be between {MinOutputSizePx} and {MaxOutputSizePx} pixels.");
        }

        ValidateEnums(request, errors);
        ValidatePayloadShape(request, errors);
        ValidateTarget(request, errors);

        if (!IsValidHexColor(request.Colors.Dark))
        {
            AddError(errors, "colors.dark", "Dark color must be a valid hex value like #10243C.");
        }

        if (!IsValidHexColor(request.Colors.Light))
        {
            AddError(errors, "colors.light", "Light color must be a valid hex value like #FFFFFF.");
        }

        ValidateDataOptions(request, errors);
        ValidateLogo(request, errors);

        return errors.ToDictionary(static entry => entry.Key, static entry => entry.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public static void EnsureValid(SubmitRenderJobRequest request)
    {
        var errors = Validate(request);

        if (errors.Count == 0)
        {
            return;
        }

        var message = string.Join(" ", errors.SelectMany(static entry => entry.Value));
        throw new InvalidOperationException(message);
    }

    // Integer enum values bypass string parsing, so every enum is checked against its defined members.
    private static void ValidateEnums(SubmitRenderJobRequest request, Dictionary<string, List<string>> errors)
    {
        AddErrorIfUndefined(errors, "contentType", request.ContentType);
        AddErrorIfUndefined(errors, "mode", request.Mode);
        AddErrorIfUndefined(errors, "errorCorrectionLevel", request.ErrorCorrectionLevel);
        AddErrorIfUndefined(errors, "finder.borderShape", request.Finder.BorderShape);
        AddErrorIfUndefined(errors, "finder.centerShape", request.Finder.CenterShape);
        AddErrorIfUndefined(errors, "data.pattern", request.Data.Pattern);
        AddErrorIfUndefined(errors, "data.gradientMode", request.Data.GradientMode);

        if (request.Logo is not null)
        {
            AddErrorIfUndefined(errors, "logo.sourceType", request.Logo.SourceType);
        }

        if (request.Mode == QrRenderMode.Managed)
        {
            AddError(errors, "mode", "Managed (dynamic) QR codes are not available yet. Use Static.");
        }
    }

    private static void ValidatePayloadShape(SubmitRenderJobRequest request, Dictionary<string, List<string>> errors)
    {
        if (request.TargetUrl is { Length: > MaxTargetUrlCharacters })
        {
            AddError(errors, "targetUrl", $"Target URL must be {MaxTargetUrlCharacters} characters or fewer.");
        }

        if (request.Payload.Count > MaxPayloadEntries)
        {
            AddError(errors, "payload", $"Payload must contain {MaxPayloadEntries} entries or fewer.");
            return;
        }

        foreach (var (key, value) in request.Payload)
        {
            if (key.Length > MaxPayloadKeyCharacters || value is { Length: > MaxPayloadValueCharacters })
            {
                AddError(errors, "payload", $"Payload keys must be {MaxPayloadKeyCharacters} characters or fewer and values {MaxPayloadValueCharacters} or fewer.");
                return;
            }
        }
    }

    private static void ValidateTarget(SubmitRenderJobRequest request, Dictionary<string, List<string>> errors)
    {
        if (!string.IsNullOrWhiteSpace(request.TargetUrl) && !IsAbsoluteHttpUrl(request.TargetUrl))
        {
            AddError(errors, "targetUrl", "Target URL must be a valid absolute http or https URL.");
        }

        if (request.ContentType == QrContentType.WhatsApp)
        {
            var hasPhone = request.Payload.TryGetValue("phone", out var phone) && !string.IsNullOrWhiteSpace(phone);

            if (string.IsNullOrWhiteSpace(request.TargetUrl) && !hasPhone)
            {
                AddError(errors, "payload.phone", "WhatsApp renders require either a target URL or a phone payload value.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            AddError(errors, "targetUrl", "Target URL is required for this content type.");
        }
    }

    private static void ValidateDataOptions(SubmitRenderJobRequest request, Dictionary<string, List<string>> errors)
    {
        if (request.Data.GradientMode == QrGradientMode.None)
        {
            return;
        }

        if (request.Data.GradientMode != QrGradientMode.Linear)
        {
            AddError(errors, "data.gradientMode", "Only linear gradients are supported in this release.");
            return;
        }

        if (!IsValidHexColor(request.Data.GradientStart))
        {
            AddError(errors, "data.gradientStart", "Gradient start must be a valid hex value like #10243C.");
        }

        if (!IsValidHexColor(request.Data.GradientEnd))
        {
            AddError(errors, "data.gradientEnd", "Gradient end must be a valid hex value like #1F61C0.");
        }

        if (request.Data.GradientRotation is < 0 or > 360)
        {
            AddError(errors, "data.gradientRotation", "Gradient rotation must be between 0 and 360 degrees.");
        }
    }

    private static void ValidateLogo(SubmitRenderJobRequest request, Dictionary<string, List<string>> errors)
    {
        if (request.Logo is null)
        {
            return;
        }

        if (request.Logo.SizePercent is < MinLogoSizePercent or > MaxLogoSizePercent)
        {
            AddError(errors, "logo.sizePercent", $"Logo size must be between {MinLogoSizePercent} and {MaxLogoSizePercent} percent.");
        }

        if (request.Logo.BackdropPaddingPercent is < MinBackdropPaddingPercent or > MaxBackdropPaddingPercent)
        {
            AddError(errors, "logo.backdropPaddingPercent", $"Logo backdrop padding must be between {MinBackdropPaddingPercent} and {MaxBackdropPaddingPercent} percent.");
        }

        switch (request.Logo.SourceType)
        {
            case QrLogoSourceType.Svg:
                if (string.IsNullOrWhiteSpace(request.Logo.Svg))
                {
                    AddError(errors, "logo.svg", "SVG logo content is required when the source type is Svg.");
                }
                else if (request.Logo.Svg.Length > MaxSvgLogoCharacters)
                {
                    AddError(errors, "logo.svg", $"SVG logos must be {MaxSvgLogoCharacters} characters or fewer.");
                }

                if (!string.IsNullOrWhiteSpace(request.Logo.ContentBase64))
                {
                    AddError(errors, "logo.contentBase64", "Raster logo content cannot be supplied when the source type is Svg.");
                }
                break;

            case QrLogoSourceType.Png:
            case QrLogoSourceType.Jpeg:
                if (string.IsNullOrWhiteSpace(request.Logo.ContentBase64))
                {
                    AddError(errors, "logo.contentBase64", "Raster logos require a base64 payload.");
                }
                else
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(request.Logo.ContentBase64);

                        if (bytes.Length > MaxRasterLogoBytes)
                        {
                            AddError(errors, "logo.contentBase64", $"Raster logos must be {MaxRasterLogoBytes / 1024} KB or smaller.");
                        }
                    }
                    catch (FormatException)
                    {
                        AddError(errors, "logo.contentBase64", "Raster logo payload must be valid base64.");
                    }
                }

                var expectedContentType = request.Logo.SourceType == QrLogoSourceType.Png ? "image/png" : "image/jpeg";

                if (!string.Equals(request.Logo.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(errors, "logo.contentType", $"Raster logo content type must be {expectedContentType}.");
                }
                break;
        }
    }

    private static void AddErrorIfUndefined<TEnum>(Dictionary<string, List<string>> errors, string key, TEnum value)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            AddError(errors, key, $"'{value}' is not a supported value. Allowed: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }

    private static bool IsAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static bool IsValidHexColor(string? value)
        => !string.IsNullOrWhiteSpace(value) && HexColorRegex().IsMatch(value);

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}