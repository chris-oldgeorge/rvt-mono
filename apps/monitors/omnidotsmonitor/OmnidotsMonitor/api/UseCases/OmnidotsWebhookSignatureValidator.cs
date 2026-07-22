using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Omnidots.Model.Config;

namespace Omnidots.Api.UseCases;

public sealed class OmnidotsWebhookSignatureValidator
{
    private const string SignaturePrefix = "sha256=";
    private const int Sha256HexLength = 64;

    public bool IsValid(string body, string? signature, string secret) =>
        IsValid(Encoding.UTF8.GetBytes(body), signature, secret);

    public bool IsValid(ReadOnlySpan<byte> body, string? signature, string secret)
    {
        if (!OmnidotsApiSecurityValidation.TryGetSecretBytes(secret, out var secretBytes))
        {
            return false;
        }

        try
        {
            if (signature is null ||
                signature.Length != SignaturePrefix.Length + Sha256HexLength ||
                !signature.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            Span<byte> suppliedDigest = stackalloc byte[SHA256.HashSizeInBytes];
            try
            {
                var status = Convert.FromHexString(
                    signature.AsSpan(SignaturePrefix.Length),
                    suppliedDigest,
                    out var charsConsumed,
                    out var bytesWritten);
                if (status != OperationStatus.Done ||
                    charsConsumed != Sha256HexLength ||
                    bytesWritten != SHA256.HashSizeInBytes)
                {
                    return false;
                }
            }
            catch (FormatException)
            {
                return false;
            }

            var expectedDigest = HMACSHA256.HashData(secretBytes, body);
            return CryptographicOperations.FixedTimeEquals(expectedDigest, suppliedDigest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }
}
