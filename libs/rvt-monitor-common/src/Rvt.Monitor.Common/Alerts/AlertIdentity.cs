using System.Security.Cryptography;
using System.Text;

namespace Rvt.Monitor.Common.Alerts;

public static class AlertIdentity
{
    // Changing this namespace changes every deterministic notification ID.
    private const string NotificationNamespace = "Rvt.Monitor.Common.Alerts.Notification/v1";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly byte[] NotificationNamespaceBytes = StrictUtf8.GetBytes(NotificationNamespace);

    public static byte[] CreateSourceKeyHash(string sourceKey)
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        return SHA256.HashData(StrictUtf8.GetBytes(sourceKey));
    }

    public static Guid CreateNotificationId(string source, byte[] sourceKeyHash)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceKeyHash);

        var sourceBytes = StrictUtf8.GetBytes(source);
        var identityBytes = new byte[
            NotificationNamespaceBytes.Length + sourceBytes.Length + 1 + sourceKeyHash.Length];
        var offset = 0;

        NotificationNamespaceBytes.CopyTo(identityBytes, offset);
        offset += NotificationNamespaceBytes.Length;
        sourceBytes.CopyTo(identityBytes, offset);
        offset += sourceBytes.Length + 1;
        sourceKeyHash.CopyTo(identityBytes, offset);

        var hash = SHA256.HashData(identityBytes);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
