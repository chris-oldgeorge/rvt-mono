namespace Rvt.Communication.Abstractions;

public enum DeliveryFailureKind { Transient, Permanent, Configuration }

public abstract class DeliveryException : Exception
{
    protected DeliveryException(string provider, string channel, DeliveryFailureKind failureKind, string? code, TimeSpan? retryAfter, Exception? innerException)
        : base(CreateMessage(provider, channel, failureKind, code), innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        Provider = provider;
        FailureKind = failureKind;
        Code = string.IsNullOrWhiteSpace(code) ? null : code;
        RetryAfter = retryAfter;
    }

    public string Provider { get; }
    public DeliveryFailureKind FailureKind { get; }
    public string? Code { get; }
    public TimeSpan? RetryAfter { get; }

    private static string CreateMessage(string provider, string channel, DeliveryFailureKind failureKind, string? code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        string codeText = string.IsNullOrWhiteSpace(code) ? string.Empty : $", code {code}";
        return $"{provider} {channel} delivery failed ({failureKind}{codeText}).";
    }
}

public sealed class EmailDeliveryException(string provider, DeliveryFailureKind failureKind, string? code = null, TimeSpan? retryAfter = null, Exception? innerException = null) : DeliveryException(provider, "email", failureKind, code, retryAfter, innerException)
{
}

public sealed class SmsDeliveryException(string provider, DeliveryFailureKind failureKind, string? code = null, TimeSpan? retryAfter = null, Exception? innerException = null) : DeliveryException(provider, "SMS", failureKind, code, retryAfter, innerException)
{
}
