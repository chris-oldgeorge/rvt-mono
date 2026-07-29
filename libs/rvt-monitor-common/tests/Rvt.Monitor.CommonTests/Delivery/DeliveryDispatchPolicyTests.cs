using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class DeliveryDispatchPolicyTests
{
    [TestMethod]
    public void SafeError_UsesDeliveryExceptionMessageAndTruncatesAtTheSharedBound()
    {
        LongCodeDeliveryException exception = new(new string('x', 5000));

        string error = DeliveryDispatchPolicy.SafeError(exception, "fallback");

        Assert.AreEqual(DeliveryDispatchPolicy.MaximumErrorLength, error.Length);
        Assert.StartsWith(exception.Message[..64], error);
    }

    [TestMethod]
    public void SafeError_ReducesOtherExceptionsToTheFallbackText()
    {
        string error = DeliveryDispatchPolicy.SafeError(
            new TimeoutException("raw provider secret"),
            "Delivery failed (TimeoutException).");

        Assert.AreEqual("Delivery failed (TimeoutException).", error);
    }

    [TestMethod]
    public void IsTerminal_MatchesNonTransientDeliveryFailuresAndAttemptExhaustion()
    {
        LongCodeDeliveryException permanent = new("code", DeliveryFailureKind.Permanent);
        LongCodeDeliveryException transient = new("code", DeliveryFailureKind.Transient);

        Assert.IsTrue(DeliveryDispatchPolicy.IsTerminal(permanent, attemptCount: 1, maxAttempts: 5));
        Assert.IsFalse(DeliveryDispatchPolicy.IsTerminal(transient, attemptCount: 1, maxAttempts: 5));
        Assert.IsTrue(DeliveryDispatchPolicy.IsTerminal(transient, attemptCount: 5, maxAttempts: 5));
        Assert.IsFalse(DeliveryDispatchPolicy.IsTerminal(new TimeoutException(), attemptCount: 1, maxAttempts: 5));
        Assert.IsTrue(DeliveryDispatchPolicy.IsTerminal(new TimeoutException(), attemptCount: 5, maxAttempts: 5));
    }

    private sealed class LongCodeDeliveryException(
        string code,
        DeliveryFailureKind failureKind = DeliveryFailureKind.Transient)
        : DeliveryException("test-provider", "email", failureKind, code, retryAfter: null, innerException: null)
    {
    }
}
