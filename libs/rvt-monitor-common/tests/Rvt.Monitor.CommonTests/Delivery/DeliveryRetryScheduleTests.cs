using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Delivery;

/// <summary>
/// The alert dispatcher and the monitor delivery dispatcher each carried their
/// own copy of this backoff — one in ticks, one in whole seconds — so the same
/// policy could drift apart. Both now call this single schedule, and these
/// tests pin the behaviour they share.
/// </summary>
[TestClass]
public sealed class DeliveryRetryScheduleTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Cap = TimeSpan.FromMinutes(30);

    [TestMethod]
    [DataRow(1, 30)]
    [DataRow(2, 60)]
    [DataRow(3, 120)]
    [DataRow(4, 240)]
    public void NextDelay_DoublesFromTheInitialDelay(int attemptCount, int expectedSeconds)
    {
        var delay = DeliveryRetrySchedule.NextDelay(attemptCount, Initial, Cap);

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void NextDelay_TreatsTheFirstAttemptAsTheInitialDelay(int attemptCount)
    {
        Assert.AreEqual(Initial, DeliveryRetrySchedule.NextDelay(attemptCount, Initial, Cap));
    }

    [TestMethod]
    public void NextDelay_IsBoundedByTheCap()
    {
        Assert.AreEqual(Cap, DeliveryRetrySchedule.NextDelay(20, Initial, Cap));
    }

    [TestMethod]
    public void NextDelay_WithAVeryLargeAttemptCount_SaturatesInsteadOfOverflowing()
    {
        // The exponent is computed in double so a runaway attempt count reaches
        // the cap rather than wrapping the tick arithmetic into a negative.
        var delay = DeliveryRetrySchedule.NextDelay(int.MaxValue, Initial, Cap);

        Assert.AreEqual(Cap, delay);
    }

    [TestMethod]
    public void NextDelay_HonoursALongerProviderRetryAfter()
    {
        var exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromMinutes(5));

        var delay = DeliveryRetrySchedule.NextDelay(1, Initial, Cap, exception);

        Assert.AreEqual(TimeSpan.FromMinutes(5), delay);
    }

    [TestMethod]
    public void NextDelay_IgnoresAShorterProviderRetryAfter()
    {
        var exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromSeconds(1));

        var delay = DeliveryRetrySchedule.NextDelay(3, Initial, Cap, exception);

        Assert.AreEqual(TimeSpan.FromSeconds(120), delay);
    }

    [TestMethod]
    public void NextDelay_BoundsAProviderRetryAfterByTheCap()
    {
        // A mistaken or hostile provider value must not park a message.
        var exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromDays(7));

        var delay = DeliveryRetrySchedule.NextDelay(1, Initial, Cap, exception);

        Assert.AreEqual(Cap, delay);
    }

    [TestMethod]
    public void NextDelay_WithANonDeliveryException_UsesTheExponentialSchedule()
    {
        var delay = DeliveryRetrySchedule.NextDelay(2, Initial, Cap, new InvalidOperationException());

        Assert.AreEqual(TimeSpan.FromSeconds(60), delay);
    }

    [TestMethod]
    public void NextDelay_WithANonPositiveCap_IsZero()
    {
        Assert.AreEqual(TimeSpan.Zero, DeliveryRetrySchedule.NextDelay(3, Initial, TimeSpan.Zero));
    }

    [TestMethod]
    public void NextDelay_MatchesForEquivalentAlertAndDeliveryConfiguration()
    {
        // The two dispatchers express the same policy in different units; the
        // shared schedule must produce identical results for both.
        var alertOptions = new DurableAlertOptions { InitialRetrySeconds = 30, MaxRetrySeconds = 1800 };
        var deliveryOptions = new MonitorDeliveryOptions
        {
            InitialRetryDelay = TimeSpan.FromSeconds(30),
            RetryCap = TimeSpan.FromSeconds(1800),
        };

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var fromAlerts = DeliveryRetrySchedule.NextDelay(
                attempt,
                TimeSpan.FromSeconds(alertOptions.InitialRetrySeconds),
                TimeSpan.FromSeconds(alertOptions.MaxRetrySeconds));
            var fromDelivery = DeliveryRetrySchedule.NextDelay(
                attempt,
                deliveryOptions.InitialRetryDelay,
                deliveryOptions.RetryCap);

            Assert.AreEqual(fromAlerts, fromDelivery, $"Attempt {attempt} diverged.");
        }
    }
}
