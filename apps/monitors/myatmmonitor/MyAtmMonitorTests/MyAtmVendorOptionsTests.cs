using Microsoft.Extensions.Options;
using MyAtm.Model.Config;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmVendorOptionsTests
{
    [TestMethod]
    public void Validate_DefaultLimitsWithCredentials_AreAccepted()
    {
        var options = ValidOptions();

        options.Validate();

        Assert.AreEqual(4 * 1024 * 1024, options.MaxResponseBytes);
        Assert.AreEqual(5, options.MaximumAttempts);
        Assert.AreEqual(500, options.MinimumRequestIntervalMilliseconds);
        Assert.AreEqual(30, options.MaximumRetryDelaySeconds);
    }

    [DataTestMethod]
    [DataRow(null, "key")]
    [DataRow("relative/path", "key")]
    [DataRow("https://vendor.example/", null)]
    public void Validate_RejectsMissingOrInvalidCredentials(string? baseUrl, string? apiKey)
    {
        var options = ValidOptions();
        options.BaseUrl = baseUrl ?? string.Empty;
        options.ApiKey = apiKey ?? string.Empty;

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsFallbackDelayGreaterThanMaximumRetryDelay()
    {
        var options = ValidOptions();
        options.FallbackRetryCapSeconds = 31;
        options.MaximumRetryDelaySeconds = 30;

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    private static MyAtmVendorOptions ValidOptions() => new()
    {
        BaseUrl = "https://vendor.example/",
        ApiKey = "test-key"
    };
}
