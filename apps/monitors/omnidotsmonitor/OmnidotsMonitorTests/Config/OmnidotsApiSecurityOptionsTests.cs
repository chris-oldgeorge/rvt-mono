using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Omnidots.Api;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Hosting;

namespace OmnidotsAdapterTests.Config;

[TestClass]
public sealed class OmnidotsApiSecurityOptionsTests
{
    private const string ValidationFailure = "Omnidots API security configuration is invalid.";
    private const string WebhookUrl = "https://alerts.example.test/omnidots";
    private const string WebhookSecret = "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww";
    private const string ConfigSecret = "cccccccccccccccccccccccccccccccc";

    [TestMethod]
    public void Validate_ApiEnabledWithValidOptions_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateValidOptions());

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    [DataRow("missing-webhook")]
    [DataRow("blank-webhook")]
    [DataRow("short-webhook")]
    [DataRow("missing-config")]
    [DataRow("blank-config")]
    [DataRow("short-config")]
    [DataRow("equal-secrets")]
    public void Validate_ApiEnabledWithInvalidSecrets_FailsGenerically(string invalidCase)
    {
        var options = CreateValidOptions();
        switch (invalidCase)
        {
            case "missing-webhook":
                options.WebhookSecret = string.Empty;
                break;
            case "blank-webhook":
                options.WebhookSecret = "   ";
                break;
            case "short-webhook":
                options.WebhookSecret = new string('w', 31);
                break;
            case "missing-config":
                options.ConfigSecret = string.Empty;
                break;
            case "blank-config":
                options.ConfigSecret = "\t";
                break;
            case "short-config":
                options.ConfigSecret = new string('c', 31);
                break;
            case "equal-secrets":
                options.ConfigSecret = options.WebhookSecret;
                break;
            default:
                Assert.Fail($"Unknown test case '{invalidCase}'.");
                break;
        }

        AssertGenericFailure(CreateValidator().Validate(null, options));
    }

    [TestMethod]
    public void Validate_SecretsUseStrictUtf8ByteLength_SucceedsForSixteenTwoByteCharacters()
    {
        var options = CreateValidOptions();
        options.WebhookSecret = string.Concat(Enumerable.Repeat("é", 16));
        options.ConfigSecret = string.Concat(Enumerable.Repeat("ø", 16));

        var result = CreateValidator().Validate(null, options);

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void Validate_InvalidSurrogateInSecret_FailsGenerically()
    {
        var options = CreateValidOptions();
        options.WebhookSecret = new string('\ud800', 1) + new string('w', 32);

        AssertGenericFailure(CreateValidator().Validate(null, options));
    }

    [TestMethod]
    [DataRow("http://alerts.example.test/omnidots")]
    [DataRow("/omnidots")]
    [DataRow("alerts.example.test/omnidots")]
    public void Validate_ApiEnabledWithNonHttpsAbsoluteUrl_FailsGenerically(string webhookUrl)
    {
        var options = CreateValidOptions();
        options.WebhookUrl = webhookUrl;

        AssertGenericFailure(CreateValidator().Validate(null, options));
    }

    [TestMethod]
    [DataRow("notification-delay")]
    [DataRow("webhook-concurrency")]
    [DataRow("configure-concurrency")]
    public void Validate_ApiEnabledWithNonpositiveLimit_FailsGenerically(string invalidCase)
    {
        var options = CreateValidOptions();
        switch (invalidCase)
        {
            case "notification-delay":
                options.NotificationDelayMinutes = 0;
                break;
            case "webhook-concurrency":
                options.WebhookConcurrencyLimit = 0;
                break;
            case "configure-concurrency":
                options.ConfigureConcurrencyLimit = -1;
                break;
            default:
                Assert.Fail($"Unknown test case '{invalidCase}'.");
                break;
        }

        AssertGenericFailure(CreateValidator().Validate(null, options));
    }

    [TestMethod]
    public void Validate_ApiDisabledSchedulerConfiguration_SucceedsWithoutApiSecrets()
    {
        var validator = CreateValidator(MonitorExecutionMode.QuartzScheduler);

        var result = validator.Validate(null, new OmnidotsApiSecurityOptions());

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void Validate_ApiDisabledUnrelatedOneShotConfiguration_SucceedsWithoutApiSecrets()
    {
        var validator = CreateValidator(MonitorExecutionMode.OneShot);

        var result = validator.Validate(null, new OmnidotsApiSecurityOptions());

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    [DataRow(MonitorExecutionMode.OneShot)]
    [DataRow(MonitorExecutionMode.QuartzScheduler)]
    public void Validate_NonApiExecutionModeWithAmbientApiEnabled_SucceedsWithoutApiSecrets(
        MonitorExecutionMode mode)
    {
        var validator = CreateValidator(mode);

        var result = validator.Validate(null, new OmnidotsApiSecurityOptions());

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void AddOmnidotsMonitor_UsesLegacyAliasesWhenSectionValuesAreAbsent()
    {
        var options = ResolveRegisteredOptions(new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            ["RVT__OMNIDOTS_WEBHOOK_URL"] = WebhookUrl,
            ["RVT__OMNIDOTS_WEBHOOK_SECRET"] = WebhookSecret,
            ["RVT__OMNIDOTS_CONFIG_SECRET"] = ConfigSecret,
            ["RVT__NOTIFICATION_DELAY_MINUTES"] = "11"
        });

        Assert.AreEqual(WebhookUrl, options.WebhookUrl);
        Assert.AreEqual(WebhookSecret, options.WebhookSecret);
        Assert.AreEqual(ConfigSecret, options.ConfigSecret);
        Assert.AreEqual(11, options.NotificationDelayMinutes);
        Assert.AreEqual(8, options.WebhookConcurrencyLimit);
        Assert.AreEqual(2, options.ConfigureConcurrencyLimit);
    }

    [TestMethod]
    public void AddOmnidotsMonitor_UsesEnvironmentNormalizedLegacyAliases()
    {
        var options = ResolveRegisteredOptions(new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            ["RVT:OMNIDOTS_WEBHOOK_URL"] = WebhookUrl,
            ["RVT:OMNIDOTS_WEBHOOK_SECRET"] = WebhookSecret,
            ["RVT:OMNIDOTS_CONFIG_SECRET"] = ConfigSecret,
            ["RVT:NOTIFICATION_DELAY_MINUTES"] = "11"
        });

        Assert.AreEqual(WebhookUrl, options.WebhookUrl);
        Assert.AreEqual(WebhookSecret, options.WebhookSecret);
        Assert.AreEqual(ConfigSecret, options.ConfigSecret);
        Assert.AreEqual(11, options.NotificationDelayMinutes);
    }

    [TestMethod]
    public void AddOmnidotsMonitor_InvalidLegacyDelay_FailsWithoutExposingValue()
    {
        const string invalidDelay = "invalid-delay-value-marker";
        var configurationValues = new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            ["RVT__OMNIDOTS_WEBHOOK_URL"] = WebhookUrl,
            ["RVT__OMNIDOTS_WEBHOOK_SECRET"] = WebhookSecret,
            ["RVT__OMNIDOTS_CONFIG_SECRET"] = ConfigSecret,
            ["RVT__NOTIFICATION_DELAY_MINUTES"] = invalidDelay
        };

        var exception = Assert.ThrowsExactly<OptionsValidationException>(() =>
            ResolveRegisteredOptions(configurationValues));

        Assert.AreEqual(ValidationFailure, exception.Failures.Single());
        Assert.IsFalse(exception.Message.Contains(invalidDelay, StringComparison.Ordinal));
    }

    [TestMethod]
    public void AddOmnidotsMonitor_PrefersPresentSectionValuesOverLegacyAliases()
    {
        const string sectionUrl = "https://section.example.test/omnidots";
        const string sectionWebhookSecret = "ssssssssssssssssssssssssssssssss";
        const string sectionConfigSecret = "tttttttttttttttttttttttttttttttt";
        var options = ResolveRegisteredOptions(new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookUrl"] = sectionUrl,
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookSecret"] = sectionWebhookSecret,
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigSecret"] = sectionConfigSecret,
            [$"{OmnidotsApiSecurityOptions.SectionName}:NotificationDelayMinutes"] = "13",
            ["RVT__OMNIDOTS_WEBHOOK_URL"] = WebhookUrl,
            ["RVT__OMNIDOTS_WEBHOOK_SECRET"] = WebhookSecret,
            ["RVT__OMNIDOTS_CONFIG_SECRET"] = ConfigSecret,
            ["RVT__NOTIFICATION_DELAY_MINUTES"] = "11"
        });

        Assert.AreEqual(sectionUrl, options.WebhookUrl);
        Assert.AreEqual(sectionWebhookSecret, options.WebhookSecret);
        Assert.AreEqual(sectionConfigSecret, options.ConfigSecret);
        Assert.AreEqual(13, options.NotificationDelayMinutes);
    }

    [TestMethod]
    public void AddOmnidotsMonitor_BlankSectionValueFallsBackToLegacyAlias()
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookUrl"] = WebhookUrl,
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookSecret"] = string.Empty,
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigSecret"] = ConfigSecret,
            ["RVT__OMNIDOTS_WEBHOOK_SECRET"] = WebhookSecret
        };

        var options = ResolveRegisteredOptions(configurationValues);

        Assert.AreEqual(WebhookSecret, options.WebhookSecret);
    }

    [TestMethod]
    public void EnsureWebhookReady_InvalidDirectOptions_ThrowsValueFreeError()
    {
        var options = CreateValidOptions();
        options.WebhookSecret = "direct-webhook-secret-marker";

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            OmnidotsApiSecurityGuard.EnsureWebhookReady(options));

        Assert.AreEqual(ValidationFailure, exception.Message);
        Assert.IsFalse(exception.Message.Contains(options.WebhookSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("equal-secrets")]
    [DataRow("missing-config-secret")]
    [DataRow("weak-config-secret")]
    [DataRow("zero-concurrency")]
    [DataRow("negative-concurrency")]
    public void EnsureWebhookReady_InvalidEndpointSpecificOptions_ThrowsValueFreeError(string invalidCase)
    {
        var options = CreateValidOptions();
        switch (invalidCase)
        {
            case "equal-secrets":
                options.ConfigSecret = options.WebhookSecret;
                break;
            case "missing-config-secret":
                options.ConfigSecret = string.Empty;
                break;
            case "weak-config-secret":
                options.ConfigSecret = "direct-config-secret-marker";
                break;
            case "zero-concurrency":
                options.WebhookConcurrencyLimit = 0;
                break;
            case "negative-concurrency":
                options.WebhookConcurrencyLimit = -1;
                break;
            default:
                Assert.Fail($"Unknown test case '{invalidCase}'.");
                break;
        }

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            OmnidotsApiSecurityGuard.EnsureWebhookReady(options));

        Assert.AreEqual(ValidationFailure, exception.Message);
        if (options.ConfigSecret.Length > 0)
        {
            Assert.IsFalse(exception.Message.Contains(options.ConfigSecret, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void EnsureWebhookReady_NonpositiveDelay_ThrowsValueFreeError(int delayMinutes)
    {
        var options = CreateValidOptions();
        options.NotificationDelayMinutes = delayMinutes;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            OmnidotsApiSecurityGuard.EnsureWebhookReady(options));

        Assert.AreEqual(ValidationFailure, exception.Message);
    }

    [TestMethod]
    public void EnsureConfigurationReady_InvalidDirectOptions_ThrowsValueFreeError()
    {
        var options = CreateValidOptions();
        options.WebhookUrl = "http://direct-value-marker.example.test";

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            OmnidotsApiSecurityGuard.EnsureConfigurationReady(options));

        Assert.AreEqual(ValidationFailure, exception.Message);
        Assert.IsFalse(exception.Message.Contains(options.WebhookUrl, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("configure-concurrency", 0)]
    [DataRow("configure-concurrency", -1)]
    [DataRow("notification-delay", 0)]
    [DataRow("notification-delay", -1)]
    public void EnsureConfigurationReady_NonpositiveEndpointSpecificValue_ThrowsValueFreeError(
        string invalidCase,
        int value)
    {
        var options = CreateValidOptions();
        if (invalidCase == "configure-concurrency")
        {
            options.ConfigureConcurrencyLimit = value;
        }
        else
        {
            options.NotificationDelayMinutes = value;
        }

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            OmnidotsApiSecurityGuard.EnsureConfigurationReady(options));

        Assert.AreEqual(ValidationFailure, exception.Message);
    }

    [TestMethod]
    public void EnsureWebhookReady_DoesNotRequireConfigureConcurrencyLimit()
    {
        var options = CreateValidOptions();
        options.ConfigureConcurrencyLimit = 0;

        OmnidotsApiSecurityGuard.EnsureWebhookReady(options);
    }

    [TestMethod]
    public void EnsureConfigurationReady_DoesNotRequireWebhookConcurrencyLimit()
    {
        var options = CreateValidOptions();
        options.WebhookConcurrencyLimit = 0;

        OmnidotsApiSecurityGuard.EnsureConfigurationReady(options);
    }

    [TestMethod]
    public void DirectGuards_ValidOptions_DoNotThrow()
    {
        var options = CreateValidOptions();

        OmnidotsApiSecurityGuard.EnsureWebhookReady(options);
        OmnidotsApiSecurityGuard.EnsureConfigurationReady(options);
    }

    private static OmnidotsApiSecurityOptionsValidator CreateValidator(
        MonitorExecutionMode mode = MonitorExecutionMode.Api) =>
        new(new MonitorExecutionModeContext(mode));

    private static OmnidotsApiSecurityOptions CreateValidOptions() => new()
    {
        WebhookUrl = WebhookUrl,
        WebhookSecret = WebhookSecret,
        ConfigSecret = ConfigSecret,
        NotificationDelayMinutes = 5,
        WebhookConcurrencyLimit = 8,
        ConfigureConcurrencyLimit = 2
    };

    private static void AssertGenericFailure(ValidateOptionsResult result)
    {
        Assert.IsFalse(result.Succeeded);
        Assert.HasCount(1, result.Failures!);
        Assert.AreEqual(ValidationFailure, result.Failures!.Single());
    }

    private static OmnidotsApiSecurityOptions ResolveRegisteredOptions(
        Dictionary<string, string?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Api));
        services.AddLogging();
        services.AddOmnidotsMonitor();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<OmnidotsApiSecurityOptions>();
    }
}
