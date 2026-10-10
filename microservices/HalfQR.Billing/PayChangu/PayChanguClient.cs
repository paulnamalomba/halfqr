using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HalfQR.Billing.Models;
using Microsoft.Extensions.Options;

namespace HalfQR.Billing.PayChangu;

public enum ProviderPaymentStatus
{
    Success,
    Cancelled,
    Failed,
    Pending,
    Unknown,
}

public sealed record ProviderInitiation(bool Success, string? ChargeId, string? ReferenceId, string? ErrorMessage);

public sealed record ProviderVerification(ProviderPaymentStatus Status, bool DetailsMatch, string? ChargeId);

// PayChangu mobile-money Direct Charge, mirrored from the Ecoride payments service.
// Docs: https://developer.paychangu.com/reference/charge-mobile-money
public sealed class PayChanguClient(
    HttpClient httpClient,
    IOptions<PayChanguOptions> options,
    IHostEnvironment environment,
    ILogger<PayChanguClient> logger)
{
    private readonly PayChanguOptions _options = options.Value;

    public bool IsMockMode
    {
        get
        {
            if (!_options.MockMode)
            {
                return false;
            }

            if (environment.IsProduction())
            {
                logger.LogError("PayChangu mock mode is configured in Production; refusing to simulate provider calls.");
                return false;
            }

            return true;
        }
    }

    public bool IsConfigured => IsMockMode || !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<ProviderInitiation> InitiateMobileMoneyAsync(BillingPayment payment, string operatorId, string mobile9Digits, CancellationToken cancellationToken)
    {
        if (IsMockMode)
        {
            logger.LogInformation("PayChangu mock mode: simulating mobile-money initiation for {TxRef}.", payment.TxRef);
            return new ProviderInitiation(true, payment.TxRef, $"mock-ref-{payment.Id:N}", null);
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            logger.LogError("PayChangu secret key is not configured; refusing payment initiation.");
            return new ProviderInitiation(false, null, null, "Payments are not configured yet. Please try again later.");
        }

        var payload = new
        {
            mobile = mobile9Digits,
            amount = payment.Amount,
            currency = payment.Currency,
            charge_id = payment.TxRef,
            tx_ref = payment.TxRef,
            callback_url = _options.WebhookUrl,
            mobile_money_operator_ref_id = operatorId,
            meta = new
            {
                user_id = payment.UserId.ToString(),
                payment_id = payment.Id.ToString(),
                plan_id = payment.PlanId,
            },
        };

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/mobile-money/payments/initialize");
            request.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayChangu initialization failed with {StatusCode} for {TxRef}.", (int)response.StatusCode, payment.TxRef);
                return new ProviderInitiation(false, null, null, ReadProviderMessage(body) ?? "The payment could not be started. Check the number and try again.");
            }

            var data = ReadData(body);
            return new ProviderInitiation(true, GetString(data, "charge_id") ?? payment.TxRef, GetString(data, "ref_id"), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Calling PayChangu failed for {TxRef}.", payment.TxRef);
            return new ProviderInitiation(false, null, null, "Could not reach the payment provider. Please try again.");
        }
    }

    // Direct-charge verify by charge_id first, then /verify-payment/{tx_ref}. Webhook bodies are never trusted on their own.
    public async Task<ProviderVerification?> VerifyAsync(BillingPayment payment, CancellationToken cancellationToken)
    {
        if (IsMockMode)
        {
            return new ProviderVerification(ProviderPaymentStatus.Success, true, payment.ProviderChargeId ?? payment.TxRef);
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return null;
        }

        try
        {
            string? body = null;

            if (!string.IsNullOrWhiteSpace(payment.ProviderChargeId))
            {
                var (statusCode, directBody) = await GetAsync($"/mobile-money/payments/{Uri.EscapeDataString(payment.ProviderChargeId)}/verify", cancellationToken);

                if (statusCode == HttpStatusCode.OK)
                {
                    body = directBody;
                }
                else if (statusCode is not (HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity))
                {
                    logger.LogWarning("PayChangu direct verify returned {StatusCode} for {TxRef}.", (int)statusCode, payment.TxRef);
                    return null;
                }
            }

            if (body is null)
            {
                var (statusCode, fallbackBody) = await GetAsync($"/verify-payment/{Uri.EscapeDataString(payment.TxRef)}", cancellationToken);

                if (statusCode != HttpStatusCode.OK)
                {
                    logger.LogWarning("PayChangu fallback verify returned {StatusCode} for {TxRef}.", (int)statusCode, payment.TxRef);
                    return null;
                }

                body = fallbackBody;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object ? dataElement : (JsonElement?)null;
            var transaction = data is { } dataValue && dataValue.TryGetProperty("transaction", out var transactionElement) && transactionElement.ValueKind == JsonValueKind.Object
                ? transactionElement
                : data;

            var rawStatus = (data is { } statusSource ? GetString(statusSource, "status") : null) ?? GetString(root, "status");
            var status = NormalizeStatus(rawStatus);
            var chargeId = transaction is { } chargeSource ? GetString(chargeSource, "charge_id") : null;

            return new ProviderVerification(status, transaction is { } details && DetailsMatch(payment, details), chargeId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Verifying PayChangu payment {TxRef} failed.", payment.TxRef);
            return null;
        }
    }

    // PayChangu sends "Signature": hex HMAC-SHA256 of the raw body. "sha256=" prefixes are accepted.
    public static bool IsValidSignature(string rawBody, string secret, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var provided = signatureHeader.Trim();

        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            provided = provided["sha256=".Length..];
        }

        var computed = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(provided.ToLowerInvariant()));
    }

    // Reads tx_ref from flat ({ tx_ref, charge_id }) or nested ({ data: { tx_ref } }) webhook payloads.
    public static string? ReadWebhookReference(string rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object ? dataElement : root;

            return GetString(data, "tx_ref") ?? GetString(root, "tx_ref") ?? GetString(data, "charge_id") ?? GetString(root, "charge_id") ?? GetString(root, "reference");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string NormalizeMalawiMobile(string? phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    private static ProviderPaymentStatus NormalizeStatus(string? status)
        => (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "successful" or "success" or "completed" => ProviderPaymentStatus.Success,
            "cancelled" or "canceled" or "dismissed" or "dismiss" or "user_cancelled" or "cancelled_by_user" => ProviderPaymentStatus.Cancelled,
            "failed" or "failure" or "rejected" or "declined" => ProviderPaymentStatus.Failed,
            "pending" or "processing" => ProviderPaymentStatus.Pending,
            _ => ProviderPaymentStatus.Unknown,
        };

    // Amount, currency and reference must match exactly. PayChangu reports MWK as "MK" on direct-charge verify.
    private static bool DetailsMatch(BillingPayment payment, JsonElement transaction)
    {
        var currency = GetString(transaction, "currency");
        var chargeId = GetString(transaction, "charge_id");
        var reference = GetString(transaction, "tx_ref");
        var currencyMatches = currency == payment.Currency || (currency == "MK" && payment.Currency == "MWK");
        var referenceMatches = (chargeId is null || chargeId == payment.ProviderChargeId || chargeId == payment.TxRef)
            && (reference is null || reference == payment.TxRef)
            && (chargeId is not null || reference is not null);

        return currencyMatches
            && referenceMatches
            && decimal.TryParse(GetString(transaction, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && amount == payment.Amount;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, _options.BaseUrl.TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<(HttpStatusCode StatusCode, string Body)> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static JsonElement ReadData(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data.Clone()
            : document.RootElement.Clone();
    }

    private static string? ReadProviderMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return GetString(document.RootElement, "message") is { Length: > 0 and <= 200 } message ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;
}
