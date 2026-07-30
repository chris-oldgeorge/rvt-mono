// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
using System.Security.Cryptography;
using System.Text;

namespace Rvt.Monitor.Common.Delivery;

public static class MonitorDeliveryIdentity
{
    public static Guid CreateGuid(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    }
}
