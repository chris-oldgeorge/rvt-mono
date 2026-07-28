using Microsoft.Extensions.Configuration;
using Rvt.Communication.MicrosoftGraphMail;

namespace Rvt.Communication.MicrosoftGraphMailTests;

[TestClass]
public sealed class MicrosoftGraphMailOptionsTests
{
    [TestMethod]
    public void FromConfiguration_UsesColonKeysForAllMicrosoftGraphSettings()
    {
        MicrosoftGraphMailOptions options = Load(
            ("RVT:EMAIL_ENABLED", "false"),
            ("RVT:MICROSOFT_TENANT_ID", "tenant-id"),
            ("RVT:MICROSOFT_CLIENT_ID", "client-id"),
            ("RVT:MICROSOFT_CLIENT_SECRET", "client-secret"),
            ("RVT:MICROSOFT_SENDER_ADDRESS", "sender@example.test"));

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual("tenant-id", options.TenantId);
        Assert.AreEqual("client-id", options.ClientId);
        Assert.AreEqual("client-secret", options.ClientSecret);
        Assert.AreEqual("sender@example.test", options.SenderAddress);
    }

    [TestMethod]
    public void FromConfiguration_FallsBackToLiteralDoubleUnderscoreKeys()
    {
        MicrosoftGraphMailOptions options = Load(
            ("RVT__EMAIL_ENABLED", "false"),
            ("RVT__MICROSOFT_TENANT_ID", "tenant-id"),
            ("RVT__MICROSOFT_CLIENT_ID", "client-id"),
            ("RVT__MICROSOFT_CLIENT_SECRET", "client-secret"),
            ("RVT__MICROSOFT_SENDER_ADDRESS", "sender@example.test"));

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual("tenant-id", options.TenantId);
        Assert.AreEqual("client-id", options.ClientId);
        Assert.AreEqual("client-secret", options.ClientSecret);
        Assert.AreEqual("sender@example.test", options.SenderAddress);
    }

    [TestMethod]
    public void FromConfiguration_PrefersColonKeysOverLiteralDoubleUnderscoreKeys()
    {
        MicrosoftGraphMailOptions options = Load(
            ("RVT:EMAIL_ENABLED", "true"),
            ("RVT__EMAIL_ENABLED", "false"),
            ("RVT:MICROSOFT_TENANT_ID", "colon-tenant"),
            ("RVT__MICROSOFT_TENANT_ID", "fallback-tenant"),
            ("RVT:MICROSOFT_CLIENT_SECRET", "colon-secret"),
            ("RVT__MICROSOFT_CLIENT_SECRET", "fallback-secret"));

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("colon-tenant", options.TenantId);
        Assert.AreEqual("colon-secret", options.ClientSecret);
    }

    [TestMethod]
    public void Validate_DisabledEmailPermitsMissingGraphCredentials()
    {
        new MicrosoftGraphMailOptions { Enabled = false }.Validate();
    }

    [TestMethod]
    public void Validate_EnabledEmailReportsAllFourRequiredKeys()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MicrosoftGraphMailOptions().Validate());

        Assert.Contains("RVT__MICROSOFT_TENANT_ID", exception.Message);
        Assert.Contains("RVT__MICROSOFT_CLIENT_ID", exception.Message);
        Assert.Contains("RVT__MICROSOFT_CLIENT_SECRET", exception.Message);
        Assert.Contains("RVT__MICROSOFT_SENDER_ADDRESS", exception.Message);
    }

    [TestMethod]
    public void Validate_DoesNotExposeConfiguredClientSecret()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MicrosoftGraphMailOptions { ClientSecret = "super-secret" }.Validate());

        Assert.DoesNotContain("super-secret", exception.Message);
    }

    [TestMethod]
    public void Validate_EnabledEmailReportsMissingClientSecretWithoutValue()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MicrosoftGraphMailOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                SenderAddress = "sender@example.test"
            }.Validate());

        Assert.Contains("RVT__MICROSOFT_CLIENT_SECRET", exception.Message);
        Assert.DoesNotContain("client-id", exception.Message);
    }

    private static MicrosoftGraphMailOptions Load(params (string Key, string Value)[] values) =>
        MicrosoftGraphMailOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build());
}
