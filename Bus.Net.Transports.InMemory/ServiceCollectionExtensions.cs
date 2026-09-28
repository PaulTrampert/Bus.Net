using Bus.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bus.Net.Transports.InMemory;

/// <summary>
/// Extension methods for registering the in-memory Bus.Net transport with the <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Bus.Net core services together with the in-memory transport.
    /// Any <see cref="IHandler{TMessage}"/> implementations already registered in the
    /// <see cref="IServiceCollection"/> are automatically subscribed.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">
    /// An optional delegate for additional configuration (e.g. handlers with non-default routes).
    /// When <see langword="null"/>, only handlers discovered from the service collection are registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddBussyInMemoryTransport(this IServiceCollection services, Action<BussyConfigurator>? configure = null)
    {
        services.AddBussy(configure);
        
        services.AddSingleton<ITransport, InMemoryTransport>();
        return services;
    }
}