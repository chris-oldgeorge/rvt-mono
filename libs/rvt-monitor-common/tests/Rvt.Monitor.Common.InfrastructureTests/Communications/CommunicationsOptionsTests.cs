using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Infrastructure.Communications;

namespace Rvt.Monitor.Common.InfrastructureTests.Communications;

[TestClass]
public sealed class CommunicationsOptionsTests
{
    [TestMethod]
    public void FromConfiguration_UsesBackwardCompatibleDefaults()
    {
        var options = Load();

        Assert.AreEqual(EmailProvider.SendGrid, options.EmailProvider);
        Assert.IsTrue(options.EmailEnabled);
        Assert.AreEqual("NoReply@rvtgroup.co.uk", options.FromEmail);
        Assert.AreEqual("RVT Cloud", options.FromName);
        Assert.IsFalse(options.SmsEnabled);
        Assert.AreEqual("KrakenAlert", options.SmsSender);
    }

    [DataTestMethod]
    [DataRow("sendgrid", EmailProvider.SendGrid)]
    [DataRow("MICROSOFTGRAPH", EmailProvider.MicrosoftGraph)]
    public void FromConfiguration_ParsesProviderCaseInsensitively(
        string configured,
        EmailProvider expected)
    {
        var options = Load(("RVT:EMAIL_PROVIDER", configured));

        Assert.AreEqual(expected, options.EmailProvider);
    }

    [TestMethod]
    public void FromConfiguration_UsesLiteralDoubleUnderscoreFallback()
    {
        var options = Load(
            ("RVT__EMAIL_PROVIDER", "MicrosoftGraph"),
            ("RVT__MICROSOFT_TENANT_ID", "tenant"),
            ("RVT__MICROSOFT_CLIENT_ID", "client"),
            ("RVT__MICROSOFT_CLIENT_SECRET", "secret"),
            ("RVT__MICROSOFT_SENDER_ADDRESS", "sender@example.test"));

        Assert.AreEqual(EmailProvider.MicrosoftGraph, options.EmailProvider);
        Assert.AreEqual("tenant", options.MicrosoftTenantId);
        Assert.AreEqual("sender@example.test", options.MicrosoftSenderAddress);
    }

    [TestMethod]
    public void FromConfiguration_PrefersStandardEnvironmentMapping()
    {
        var options = Load(
            ("RVT:EMAIL_PROVIDER", "SendGrid"),
            ("RVT__EMAIL_PROVIDER", "MicrosoftGraph"));

        Assert.AreEqual(EmailProvider.SendGrid, options.EmailProvider);
    }

    [TestMethod]
    public void FromConfiguration_InvalidProviderNamesOnlyTheSetting()
    {
        const string invalidValue = "provider-secret-value";

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Load(("RVT:EMAIL_PROVIDER", invalidValue)));

        Assert.Contains("RVT__EMAIL_PROVIDER", exception.Message);
        Assert.DoesNotContain(invalidValue, exception.Message);
    }

    [TestMethod]
    public void Validate_SendGridRequiresApiKeyWhenEmailEnabled()
    {
        var options = Load();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        Assert.Contains("RVT__SENDGRID_API_KEY", exception.Message);
    }

    [DataTestMethod]
    [DataRow("MICROSOFT_TENANT_ID")]
    [DataRow("MICROSOFT_CLIENT_ID")]
    [DataRow("MICROSOFT_CLIENT_SECRET")]
    [DataRow("MICROSOFT_SENDER_ADDRESS")]
    public void Validate_MicrosoftGraphRequiresEverySelectedProviderSetting(string missingSetting)
    {
        var values = new Dictionary<string, string?>
        {
            ["RVT:EMAIL_PROVIDER"] = "MicrosoftGraph",
            ["RVT:MICROSOFT_TENANT_ID"] = "tenant-secret",
            ["RVT:MICROSOFT_CLIENT_ID"] = "client-secret",
            ["RVT:MICROSOFT_CLIENT_SECRET"] = "credential-secret",
            ["RVT:MICROSOFT_SENDER_ADDRESS"] = "sender@example.test"
        };
        values[$"RVT:{missingSetting}"] = string.Empty;
        var options = Load(values);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        Assert.Contains($"RVT__{missingSetting}", exception.Message);
        Assert.DoesNotContain("tenant-secret", exception.Message);
        Assert.DoesNotContain("client-secret", exception.Message);
        Assert.DoesNotContain("credential-secret", exception.Message);
        Assert.DoesNotContain("sender@example.test", exception.Message);
    }

    [TestMethod]
    public void Validate_EmailDisabledAllowsMissingProviderCredentials()
    {
        var options = Load(("RVT:EMAIL_ENABLED", "false"));

        options.Validate();
    }

    [TestMethod]
    public void Validate_SmsEnabledRequiresKeyAndSecretWithoutLeakingConfiguredValues()
    {
        const string configuredKey = "configured-api-key-secret";
        var options = Load(
            ("RVT:EMAIL_ENABLED", "false"),
            ("RVT:SMS_ENABLED", "true"),
            ("RVT:SMS_API_KEY", configuredKey));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        Assert.Contains("RVT__SMS_API_SECRET", exception.Message);
        Assert.DoesNotContain(configuredKey, exception.Message);
    }

    [TestMethod]
    public void Validate_SmsDisabledAllowsMissingCredentials()
    {
        var options = Load(
            ("RVT:EMAIL_ENABLED", "false"),
            ("RVT:SMS_ENABLED", "false"));

        options.Validate();
    }

    private static CommunicationsOptions Load(params (string Key, string? Value)[] values) =>
        Load(values.ToDictionary(item => item.Key, item => item.Value));

    private static CommunicationsOptions Load(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return CommunicationsOptions.FromConfiguration(configuration);
    }
}
