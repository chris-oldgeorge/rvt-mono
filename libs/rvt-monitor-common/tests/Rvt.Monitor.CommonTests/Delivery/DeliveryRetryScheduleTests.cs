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
    private static readonly TimeSpan _initial = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _cap = TimeSpan.FromMinutes(30);

    [TestMethod]
    [DataRow(1, 30)]
    [DataRow(2, 60)]
    [DataRow(3, 120)]
    [DataRow(4, 240)]
    public void NextDelay_DoublesFromTheInitialDelay(int attemptCount, int expectedSeconds)
    {
        TimeSpan delay = DeliveryRetrySchedule.NextDelay(attemptCount, _initial, _cap);

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void NextDelay_TreatsTheFirstAttemptAsTheInitialDelay(int attemptCount)
    {
        Assert.AreEqual(_initial, DeliveryRetrySchedule.NextDelay(attemptCount, _initial, _cap));
    }

    [TestMethod]
    public void NextDelay_IsBoundedByTheCap()
    {
        Assert.AreEqual(_cap, DeliveryRetrySchedule.NextDelay(20, _initial, _cap));
    }

    [TestMethod]
    public void NextDelay_WithAVeryLargeAttemptCount_SaturatesInsteadOfOverflowing()
    {
        // The exponent is computed in double so a runaway attempt count reaches
        // the cap rather than wrapping the tick arithmetic into a negative.
        TimeSpan delay = DeliveryRetrySchedule.NextDelay(int.MaxValue, _initial, _cap);

        Assert.AreEqual(_cap, delay);
    }

    [TestMethod]
    public void NextDelay_HonoursALongerProviderRetryAfter()
    {
        EmailDeliveryException exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromMinutes(5));

        TimeSpan delay = DeliveryRetrySchedule.NextDelay(1, _initial, _cap, exception);

        Assert.AreEqual(TimeSpan.FromMinutes(5), delay);
    }

    [TestMethod]
    public void NextDelay_IgnoresAShorterProviderRetryAfter()
    {
        EmailDeliveryException exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromSeconds(1));

        TimeSpan delay = DeliveryRetrySchedule.NextDelay(3, _initial, _cap, exception);

        Assert.AreEqual(TimeSpan.FromSeconds(120), delay);
    }

    [TestMethod]
    public void NextDelay_BoundsAProviderRetryAfterByTheCap()
    {
        // A mistaken or hostile provider value must not park a message.
        EmailDeliveryException exception = new EmailDeliveryException(
            "provider",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromDays(7));

        TimeSpan delay = DeliveryRetrySchedule.NextDelay(1, _initial, _cap, exception);

        Assert.AreEqual(_cap, delay);
    }

    [TestMethod]
    public void NextDelay_WithANonDeliveryException_UsesTheExponentialSchedule()
    {
        TimeSpan delay = DeliveryRetrySchedule.NextDelay(2, _initial, _cap, new InvalidOperationException());

        Assert.AreEqual(TimeSpan.FromSeconds(60), delay);
    }

    [TestMethod]
    public void NextDelay_WithANonPositiveCap_IsZero()
    {
        Assert.AreEqual(TimeSpan.Zero, DeliveryRetrySchedule.NextDelay(3, _initial, TimeSpan.Zero));
    }

    [TestMethod]
    public void NextDelay_MatchesForEquivalentAlertAndDeliveryConfiguration()
    {
        // The two dispatchers express the same policy in different units; the
        // shared schedule must produce identical results for both.
        DurableAlertOptions alertOptions = new DurableAlertOptions { InitialRetrySeconds = 30, MaxRetrySeconds = 1800 };
        MonitorDeliveryOptions deliveryOptions = new MonitorDeliveryOptions
        {
            InitialRetryDelay = TimeSpan.FromSeconds(30),
            RetryCap = TimeSpan.FromSeconds(1800),
        };

        for (int attempt = 1; attempt <= 10; attempt++)
        {
            TimeSpan fromAlerts = DeliveryRetrySchedule.NextDelay(
                attempt,
                TimeSpan.FromSeconds(alertOptions.InitialRetrySeconds),
                TimeSpan.FromSeconds(alertOptions.MaxRetrySeconds));
            TimeSpan fromDelivery = DeliveryRetrySchedule.NextDelay(
                attempt,
                deliveryOptions.InitialRetryDelay,
                deliveryOptions.RetryCap);

            Assert.AreEqual(fromAlerts, fromDelivery, $"Attempt {attempt} diverged.");
        }
    }
}
