using Microsoft.Extensions.Configuration;
using Rvt.Communication.TransmitSms;

namespace Rvt.Communication.TransmitSmsTests;

[TestClass]
public sealed class TransmitSmsOptionsTests
{
    [TestMethod]
    public void FromConfiguration_UsesColonKeysForAllTransmitSmsSettings()
    {
        var options = Load(
            ("RVT:SMS_ENABLED", "true"),
            ("RVT:SMS_API_KEY", "api-key"),
            ("RVT:SMS_API_SECRET", "api-secret"),
            ("RVT:SMS_SENDER", "RVT Alerts"));

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("api-key", options.ApiKey);
        Assert.AreEqual("api-secret", options.ApiSecret);
        Assert.AreEqual("RVT Alerts", options.Sender);
    }

    [TestMethod]
    public void FromConfiguration_FallsBackToLiteralDoubleUnderscoreKeys()
    {
        var options = Load(
            ("RVT__SMS_ENABLED", "true"),
            ("RVT__SMS_API_KEY", "api-key"),
            ("RVT__SMS_API_SECRET", "api-secret"),
            ("RVT__SMS_SENDER", "RVT Alerts"));

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("api-key", options.ApiKey);
        Assert.AreEqual("api-secret", options.ApiSecret);
        Assert.AreEqual("RVT Alerts", options.Sender);
    }

    [TestMethod]
    public void FromConfiguration_PrefersColonKeysAndAppliesDefaults()
    {
        var options = Load(
            ("RVT:SMS_ENABLED", "true"),
            ("RVT__SMS_ENABLED", "false"),
            ("RVT:SMS_API_KEY", "colon-key"),
            ("RVT__SMS_API_KEY", "fallback-key"),
            ("RVT:SMS_API_SECRET", "colon-secret"),
            ("RVT__SMS_API_SECRET", "fallback-secret"));

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("colon-key", options.ApiKey);
        Assert.AreEqual("colon-secret", options.ApiSecret);
        Assert.AreEqual("KrakenAlert", options.Sender);
    }

    [TestMethod]
    public void FromConfiguration_UsesDisabledEmptyCredentialAndDefaultSenderValues()
    {
        var options = Load();

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(string.Empty, options.ApiKey);
        Assert.AreEqual(string.Empty, options.ApiSecret);
        Assert.AreEqual("KrakenAlert", options.Sender);
    }

    [TestMethod]
    public void Validate_DisabledSmsPermitsMissingCredentials()
    {
        new TransmitSmsOptions { Enabled = false }.Validate();
    }

    [TestMethod]
    public void Validate_EnabledSmsNamesEveryMissingSetting()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new TransmitSmsOptions { Enabled = true, Sender = string.Empty }.Validate());

        Assert.Contains("RVT__SMS_API_KEY", exception.Message);
        Assert.Contains("RVT__SMS_API_SECRET", exception.Message);
        Assert.Contains("RVT__SMS_SENDER", exception.Message);
    }

    [TestMethod]
    public void Validate_EnabledSmsReportsMissingSettingsWithoutExposingConfiguredSecrets()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new TransmitSmsOptions
            {
                Enabled = true,
                ApiKey = "api-key-secret",
                ApiSecret = "api-secret-value",
                Sender = string.Empty
            }.Validate());

        Assert.Contains("RVT__SMS_SENDER", exception.Message);
        Assert.DoesNotContain("api-key-secret", exception.Message);
        Assert.DoesNotContain("api-secret-value", exception.Message);
    }

    private static TransmitSmsOptions Load(params (string Key, string Value)[] values) =>
        TransmitSmsOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build());
}
