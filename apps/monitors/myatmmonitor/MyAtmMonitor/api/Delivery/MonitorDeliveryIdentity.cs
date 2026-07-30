using System.Security.Cryptography;
using System.Text;

namespace MyAtm.Delivery;

public static class MonitorDeliveryIdentity
{
    public static Guid CreateGuid(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    }
}
