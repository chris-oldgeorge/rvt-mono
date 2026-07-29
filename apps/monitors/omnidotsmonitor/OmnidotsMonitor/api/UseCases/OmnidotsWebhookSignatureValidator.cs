using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Omnidots.Model.Config;

namespace Omnidots.Api.UseCases;

public sealed class OmnidotsWebhookSignatureValidator
{
    private const string _signaturePrefix = "sha256=";
    private const int _sha256HexLength = 64;

    public bool IsValid(string body, string? signature, string secret) =>
        IsValid(Encoding.UTF8.GetBytes(body), signature, secret);

    public bool IsValid(ReadOnlySpan<byte> body, string? signature, string secret)
    {
        if (!OmnidotsApiSecurityValidation.TryGetSecretBytes(secret, out byte[]? secretBytes))
        {
            return false;
        }

        try
        {
            if (signature is null ||
                signature.Length != _signaturePrefix.Length + _sha256HexLength ||
                !signature.StartsWith(_signaturePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            Span<byte> suppliedDigest = stackalloc byte[SHA256.HashSizeInBytes];
            try
            {
                OperationStatus status = Convert.FromHexString(
                    signature.AsSpan(_signaturePrefix.Length),
                    suppliedDigest,
                    out int charsConsumed,
                    out int bytesWritten);
                if (status != OperationStatus.Done ||
                    charsConsumed != _sha256HexLength ||
                    bytesWritten != SHA256.HashSizeInBytes)
                {
                    return false;
                }
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] expectedDigest = HMACSHA256.HashData(secretBytes, body);
            return CryptographicOperations.FixedTimeEquals(expectedDigest, suppliedDigest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }
}
