using Microsoft.Extensions.Configuration;
using Rvt.Communication.SendGridMail;

namespace Rvt.Communication.SendGridMailTests;

[TestClass]
public sealed class SendGridMailOptionsTests
{
    [TestMethod]
    public void FromConfiguration_UsesColonKeysForAllSendGridSettings()
    {
        var options = Load(
            ("RVT:EMAIL_ENABLED", "false"),
            ("RVT:SENDGRID_API_KEY", "api-key"),
            ("RVT:EMAIL_ALERT_FROM_EMAIL", "alerts@example.test"),
            ("RVT:EMAIL_ALERT_FROM_NAME", "RVT Alerts"));

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual("api-key", options.ApiKey);
        Assert.AreEqual("alerts@example.test", options.FromEmail);
        Assert.AreEqual("RVT Alerts", options.FromName);
    }

    [TestMethod]
    public void FromConfiguration_FallsBackToLiteralDoubleUnderscoreKeys()
    {
        var options = Load(
            ("RVT__EMAIL_ENABLED", "false"),
            ("RVT__SENDGRID_API_KEY", "api-key"),
            ("RVT__EMAIL_ALERT_FROM_EMAIL", "alerts@example.test"),
            ("RVT__EMAIL_ALERT_FROM_NAME", "RVT Alerts"));

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual("api-key", options.ApiKey);
        Assert.AreEqual("alerts@example.test", options.FromEmail);
        Assert.AreEqual("RVT Alerts", options.FromName);
    }

    [TestMethod]
    public void FromConfiguration_PrefersColonKeysOverLiteralDoubleUnderscoreKeys()
    {
        var options = Load(
            ("RVT:EMAIL_ENABLED", "true"),
            ("RVT__EMAIL_ENABLED", "false"),
            ("RVT:SENDGRID_API_KEY", "colon-key"),
            ("RVT__SENDGRID_API_KEY", "fallback-key"));

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("colon-key", options.ApiKey);
    }

    [TestMethod]
    public void Validate_DisabledEmailPermitsMissingCredentials()
    {
        new SendGridMailOptions { Enabled = false }.Validate();
    }

    [TestMethod]
    public void Validate_EnabledEmailReportsRequiredKeysWithoutConfiguredValues()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new SendGridMailOptions
            {
                FromEmail = string.Empty,
                FromName = string.Empty
            }.Validate());

        Assert.Contains("RVT__SENDGRID_API_KEY", exception.Message);
        Assert.Contains("RVT__EMAIL_ALERT_FROM_EMAIL", exception.Message);
        Assert.Contains("RVT__EMAIL_ALERT_FROM_NAME", exception.Message);
        Assert.DoesNotContain("api-key", exception.Message);
    }

    private static SendGridMailOptions Load(params (string Key, string Value)[] values) =>
        SendGridMailOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build());
}
