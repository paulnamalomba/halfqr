using HaveQR.Contracts.Enums;

namespace HaveQR.QrEngine.PayloadEncoding;

public interface IQrPayloadEncoder
{
    string Encode(QrContentType contentType, string? targetUrl, IReadOnlyDictionary<string, string?> payload);
}