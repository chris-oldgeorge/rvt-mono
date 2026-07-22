using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Scheduling;

public enum MonitorInfrastructure
{
    Local,
    Azure
}

public sealed record MonitorInfrastructureOptions
{
    public MonitorInfrastructure Infrastructure { get; init; } = MonitorInfrastructure.Local;

    public bool AllowsQuartzScheduler => Infrastructure == MonitorInfrastructure.Local;

    public static MonitorInfrastructureOptions Bind(IConfiguration configuration)
    {
        var configuredValue = configuration["Infrastructure"]
            ?? configuration["RVT:Infrastructure"];

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return new MonitorInfrastructureOptions();
        }

        if (!Enum.TryParse<MonitorInfrastructure>(configuredValue, ignoreCase: true, out var infrastructure))
        {
            throw new InvalidOperationException(
                $"Unsupported infrastructure value '{configuredValue}'. Allowed values are 'local' and 'azure'.");
        }

        return new MonitorInfrastructureOptions
        {
            Infrastructure = infrastructure
        };
    }

    public static bool IsQuartzSchedulerEnabled(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("MonitorScheduler:Enabled")
            && Bind(configuration).AllowsQuartzScheduler;
    }
}
