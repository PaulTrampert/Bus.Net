using PTrampert.MessageBus.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace PTrampert.MessageBus.Transports.RabbitMq;

/// <summary>
/// Extension methods for registering the RabbitMQ PTrampert.MessageBus transport with the <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PTrampert.MessageBus core services together with the RabbitMQ transport.
    /// Any <see cref="IHandler{TMessage}"/> implementations already registered in the
    /// <see cref="IServiceCollection"/> are automatically subscribed.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">
    /// An optional delegate for additional configuration (e.g. handlers with non-default routes).
    /// When <see langword="null"/>, only handlers discovered from the service collection are registered.
    /// </param>
    /// <param name="configureRabbitMq">An optional delegate to configure <see cref="RabbitMqTransportOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddPTrampertMessageBusRabbitMqTransport(
        this IServiceCollection services,
        Action<BusConfigurator>? configure = null,
        Action<RabbitMqTransportOptions>? configureRabbitMq = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RabbitMqTransportOptions();
        configureRabbitMq?.Invoke(options);

        services.AddPTrampertMessageBus(configure);
        services.AddSingleton(options);
        services.AddSingleton<IRabbitMqMessageMapper, RabbitMqMessageMapper>();
        services.AddSingleton<ITransport, RabbitMqTransport>();

        return services;
    }
}

