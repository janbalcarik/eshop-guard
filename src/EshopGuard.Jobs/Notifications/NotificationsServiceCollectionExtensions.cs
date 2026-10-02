using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EshopGuard.Jobs.Notifications;

/// <summary>Registration of <see cref="NotificationDispatcher"/> (API and worker).</summary>
public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<NotificationsOptions>().BindConfiguration(NotificationsOptions.SectionName);
        services.TryAddSingleton<NotificationDispatcher>();
        return services;
    }
}
