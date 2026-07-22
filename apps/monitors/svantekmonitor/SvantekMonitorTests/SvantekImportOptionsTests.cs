using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Svantek.Api;
using Svantek.Model.Config;

namespace SvantekMonitorTests;

[TestClass]
public sealed class SvantekImportOptionsTests
{
    [TestMethod]
    public void Validate_UsesExactImportDefaults()
    {
        var options = new SvantekImportOptions();

        options.Validate();

        Assert.AreEqual(TimeSpan.FromDays(7), options.MaximumInitialBackfill);
        Assert.AreEqual(TimeSpan.FromHours(12), options.MaximumRequestWindow);
        Assert.AreEqual(TimeSpan.FromMinutes(5), options.WatermarkOverlap);
    }

    [DataTestMethod]
    [DataRow(nameof(SvantekImportOptions.MaximumInitialBackfill), 0L)]
    [DataRow(nameof(SvantekImportOptions.MaximumInitialBackfill), -1L)]
    [DataRow(nameof(SvantekImportOptions.MaximumRequestWindow), 0L)]
    [DataRow(nameof(SvantekImportOptions.MaximumRequestWindow), -1L)]
    [DataRow(nameof(SvantekImportOptions.WatermarkOverlap), 0L)]
    [DataRow(nameof(SvantekImportOptions.WatermarkOverlap), -1L)]
    public void Validate_RejectsNonPositiveDurations(string propertyName, long ticks)
    {
        var invalidValue = TimeSpan.FromTicks(ticks);
        var options = propertyName switch
        {
            nameof(SvantekImportOptions.MaximumInitialBackfill) => new SvantekImportOptions
            {
                MaximumInitialBackfill = invalidValue
            },
            nameof(SvantekImportOptions.MaximumRequestWindow) => new SvantekImportOptions
            {
                MaximumRequestWindow = invalidValue
            },
            nameof(SvantekImportOptions.WatermarkOverlap) => new SvantekImportOptions
            {
                WatermarkOverlap = invalidValue
            },
            _ => throw new AssertFailedException($"Unexpected property {propertyName}.")
        };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsRequestWindowLongerThanInitialBackfill()
    {
        var options = new SvantekImportOptions
        {
            MaximumInitialBackfill = TimeSpan.FromHours(12),
            MaximumRequestWindow = TimeSpan.FromHours(12).Add(TimeSpan.FromTicks(1))
        };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_AllowsRequestWindowEqualToInitialBackfill()
    {
        var options = new SvantekImportOptions
        {
            MaximumInitialBackfill = TimeSpan.FromHours(12),
            MaximumRequestWindow = TimeSpan.FromHours(12)
        };

        options.Validate();
    }

    [TestMethod]
    public void AddSvantekMonitor_BindsValidatedOptionsAndRegistersCalculator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SvantekImportOptions.SectionName}:MaximumInitialBackfill"] = "3.00:00:00",
                [$"{SvantekImportOptions.SectionName}:MaximumRequestWindow"] = "06:00:00",
                [$"{SvantekImportOptions.SectionName}:WatermarkOverlap"] = "00:02:00"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSvantekMonitor();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SvantekImportOptions>();

        Assert.AreEqual(TimeSpan.FromDays(3), options.MaximumInitialBackfill);
        Assert.AreEqual(TimeSpan.FromHours(6), options.MaximumRequestWindow);
        Assert.AreEqual(TimeSpan.FromMinutes(2), options.WatermarkOverlap);
        Assert.AreSame(options, provider.GetRequiredService<SvantekImportOptions>());
        var calculator = provider.GetRequiredService<NoiseRequestWindowCalculator>();
        Assert.AreSame(calculator, provider.GetRequiredService<NoiseRequestWindowCalculator>());
    }

    [TestMethod]
    public void AddSvantekMonitor_RejectsInvalidBoundOptionsWhenResolved()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SvantekImportOptions.SectionName}:MaximumRequestWindow"] = "00:00:00"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSvantekMonitor();

        using var provider = services.BuildServiceProvider();

        Assert.ThrowsExactly<OptionsValidationException>(
            () => provider.GetRequiredService<SvantekImportOptions>());
    }

    [TestMethod]
    public async Task AddSvantekMonitor_InvalidBoundOptionsFailHostStartupBeforeCalculatorResolution()
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RVT:EMAIL_ENABLED"] = "false",
                    ["RVT:SMS_ENABLED"] = "false",
                    [$"{SvantekImportOptions.SectionName}:MaximumRequestWindow"] = "00:00:00"
                }))
            .ConfigureServices(services => services.AddSvantekMonitor())
            .Build();

        await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());
    }
}
