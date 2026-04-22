using HalfQR.Contracts.Enums;

namespace HalfQR.QrEngine.PayloadEncoding;

public interface IQrPayloadEncoder
{
    string Encode(QrContentType contentType, string? targetUrl, IReadOnlyDictionary<string, string?> payload);
}