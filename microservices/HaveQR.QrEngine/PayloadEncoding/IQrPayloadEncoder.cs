using HaveQR.Contracts.Enums;

namespace HaveQR.QrEngine.PayloadEncoding;

public interface IQrPayloadEncoder
{
    string Encode(QrContentType contentType, IReadOnlyDictionary<string, string?> payload);
}