using System.Security.Cryptography;
using System.Text;

namespace Omnidots.Api.UseCases;

public static class OmnidotsFixedTimeSecretComparer
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static bool Matches(string? suppliedSecret, string? configuredSecret)
    {
        if (suppliedSecret is null || configuredSecret is null)
        {
            return false;
        }

        byte[] suppliedBytes = [];
        byte[] configuredBytes = [];
        byte[] suppliedDigest = [];
        byte[] configuredDigest = [];

        try
        {
            suppliedBytes = StrictUtf8.GetBytes(suppliedSecret);
            configuredBytes = StrictUtf8.GetBytes(configuredSecret);
            suppliedDigest = SHA256.HashData(suppliedBytes);
            configuredDigest = SHA256.HashData(configuredBytes);
            return CryptographicOperations.FixedTimeEquals(suppliedDigest, configuredDigest);
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedBytes);
            CryptographicOperations.ZeroMemory(configuredBytes);
            CryptographicOperations.ZeroMemory(suppliedDigest);
            CryptographicOperations.ZeroMemory(configuredDigest);
        }
    }
}
