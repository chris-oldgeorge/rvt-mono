using System.Security.Cryptography;
using System.Text;

namespace Rvt.Monitor.Common.Alerts;

public static class AlertDeliveryIdentity
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Create(
        Guid occurrenceId,
        string kind,
        string canonicalDestination)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(canonicalDestination);

        var identity = string.Concat(
            occurrenceId.ToString("D"),
            "\0",
            kind,
            "\0",
            canonicalDestination);
        return Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(identity)));
    }

    public static string CanonicalEmail(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.Trim().ToLowerInvariant();
    }

    public static string CanonicalSms(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.Trim();
    }
}
