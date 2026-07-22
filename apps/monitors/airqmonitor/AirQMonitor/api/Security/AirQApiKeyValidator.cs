using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace AirQ.Api.Security;

public sealed class AirQApiKeyValidator
{
    private readonly byte[] expectedKey;

    private AirQApiKeyValidator(string configuredKey) => expectedKey = Encoding.UTF8.GetBytes(configuredKey);

    public static AirQApiKeyValidator Create(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("Monitor API requires RVT__MONITOR_API_KEY when enabled.");
        }

        return new AirQApiKeyValidator(configuredKey);
    }

    public bool IsAuthorized(StringValues suppliedKeys)
    {
        if (suppliedKeys.Count != 1 || string.IsNullOrWhiteSpace(suppliedKeys[0]))
        {
            return false;
        }

        var suppliedKey = Encoding.UTF8.GetBytes(suppliedKeys[0]!);
        return CryptographicOperations.FixedTimeEquals(expectedKey, suppliedKey);
    }
}
