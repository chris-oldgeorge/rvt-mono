using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.Common.Alerts;

public static class AlertServiceCollectionExtensions
{
    public static IServiceCollection AddDurableAlerts<TContext>(this IServiceCollection services)
        where TContext : MonitorDbContextBase
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DurableAlertOptions>()
            .BindConfiguration(DurableAlertOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DurableAlertOptions>, DurableAlertOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Unspecified));
        services.AddSingleton<IAlertAcceptancePolicy, CautionAlertAcceptancePolicy>();
        services.AddSingleton<IAlertCommitStore, EfAlertCommitStore<TContext>>();
        services.AddSingleton<IAlertOutboxStore, EfAlertOutboxStore<TContext>>();
        services.AddSingleton<IAlertIngressPort, DurableAlertService>();
        services.AddSingleton<IAlertDeliveryAdapter, MqttAlertDeliveryAdapter>();
        services.AddSingleton<IAlertDeliveryAdapter, EmailAlertDeliveryAdapter>();
        services.AddSingleton<IAlertDeliveryAdapter, SmsAlertDeliveryAdapter>();
        services.AddSingleton<DurableAlertDispatcher>();
        services.AddSingleton<DurableAlertCleanupService>();
        services.AddSingleton<IHostedService, DurableAlertBackgroundService>();

        return services;
    }
}
