using AirQ.Api;
using AirQ.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AirQMonitorTests;

[TestClass]
public sealed class AirQImportOptionsTests
{
    [TestMethod]
    public void Validate_UsesTheSameSevenDayCapAsSvantek()
    {
        AirQImportOptions options = new();

        options.Validate();

        Assert.AreEqual(TimeSpan.FromDays(7), options.MaximumInitialBackfill);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public void Validate_RejectsNonPositiveBackfill(long ticks)
    {
        AirQImportOptions options = new() { MaximumInitialBackfill = TimeSpan.FromTicks(ticks) };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void AddAirQMonitor_BindsTheConfiguredBackfill()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AirQImportOptions.SectionName}:MaximumInitialBackfill"] = "3.00:00:00"
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAirQMonitor(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        AirQImportOptions options = provider.GetRequiredService<AirQImportOptions>();

        Assert.AreEqual(TimeSpan.FromDays(3), options.MaximumInitialBackfill);
        Assert.AreSame(options, provider.GetRequiredService<AirQImportOptions>());
    }

    [TestMethod]
    public void AddAirQMonitor_RejectsAnInvalidBoundBackfillWhenResolved()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AirQImportOptions.SectionName}:MaximumInitialBackfill"] = "00:00:00"
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAirQMonitor(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.ThrowsExactly<OptionsValidationException>(
            () => provider.GetRequiredService<AirQImportOptions>());
    }
}
