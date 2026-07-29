using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication;

public static class CommunicationServiceCollectionExtensions
{
    public static IServiceCollection AddRvtCommunication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<INotificationMessageComposer, NotificationMessageComposer>();
        services.TryAddSingleton<INotificationDeliveryService, NotificationDeliveryService>();
        return services;
    }
}
