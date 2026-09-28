using Bus.Net.Registries;
using Bus.Net.Test.TestMessageTypes;
using Bus.Net.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Bus.Net.Test;

[TestFixture]
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Builds a minimal service provider (without starting a hosted service) that contains
    /// the core Bus.Net registrations so the <see cref="BusConfigurator"/> singleton
    /// (and therefore the <see cref="HandlerRegistry"/>) can be resolved synchronously.
    /// </summary>
    private static ServiceProvider BuildProvider(Action<IServiceCollection> setup)
    {
        var services = new ServiceCollection();
        // Register a no-op ILoggerFactory so HandlerRegistry can be constructed.
        services.AddLogging();
        // Register a stub transport so BusConfigurator.RegisterTransports does not blow up.
        var transport = new Mock<ITransport>();
        transport.Setup(t => t.Name).Returns("stub");
        services.AddSingleton(transport.Object);
        setup(services);
        var provider = services.BuildServiceProvider();
        // Resolve BusConfigurator to trigger its lazy singleton factory, which runs
        // the configure callback and auto-discovers IHandler<> implementations.
        _ = provider.GetRequiredService<BusConfigurator>();
        return provider;
    }

    // ---------------------------------------------------------------------------
    // Auto-discovery from DI
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_AutoDiscovers_HandlerRegisteredAsConcrete()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
        Assert.That(registry.Handlers[route], Has.Exactly(1).Items);
    }

    [Test]
    public void AddBus_AutoDiscovers_HandlerRegisteredAsInterface()
    {
        using var provider = BuildProvider(sc =>
        {
            // Register as the IHandler<> interface for DI injection (e.g. via constructor).
            // Also register by concrete type so InboundMessageHandler can resolve it with
            // GetRequiredService(handlerType) — Bus always resolves handlers by their concrete type.
            sc.AddScoped<IHandler<TestMessage>, SimpleTestHandler>();
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void AddBus_NoConfigure_NoHandlers_RegistryIsEmpty()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        Assert.That(registry.Handlers, Is.Empty);
    }

    // ---------------------------------------------------------------------------
    // Duplicate prevention
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_DoesNotDuplicate_WhenHandlerAlreadyExplicitlyRegistered()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBus(cfg =>
            {
                // Explicit registration for the same handler with the same default route.
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>();
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        // Should be exactly one subscription, not two.
        Assert.That(registry.Handlers[route], Has.Exactly(1).Items);
    }

    [Test]
    public void AddBus_DoesNotDuplicate_WhenHandlerExplicitlyRegisteredWithCustomRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBus(cfg =>
            {
                // Explicit registration for the handler with a custom route.
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>(topic: "custom-topic");
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();

        // The handler was registered explicitly, so auto-discovery should skip it.
        // Only the custom route should be present.
        var customRoute = new MessageRoute(Topic: "custom-topic", Broker: null);
        var defaultRoute = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Handlers, Does.ContainKey(customRoute));
            Assert.That(registry.Handlers, Does.Not.ContainKey(defaultRoute));
        });
    }

    // ---------------------------------------------------------------------------
    // MessageRouteAttribute honoured during auto-discovery
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_AutoDiscovers_HandlerForAttributedMessage_UsesAttributeRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<TopicOnlyMessageTestHandler>();
            sc.AddBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: "topic-only", Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // configure callback still works independently
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_ExplicitConfigure_WorksWithoutDiRegistration()
    {
        using var provider = BuildProvider(sc =>
        {
            // Handler is NOT registered in DI as a service — only declared via configure.
            sc.AddBus(cfg =>
            {
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>();
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // IHostedService is registered
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_RegistersBusService_AsHostedService()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBus();
        });

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.That(hostedServices, Has.Exactly(1).InstanceOf<BusService>());
    }

    // ---------------------------------------------------------------------------
    // AddBusHandlers + AddBus integration — call order
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBusHandlers_BeforeAddBus_AutoSubscribesScannedHandlers()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBusHandlers(typeof(SimpleTestHandler).Assembly);
            sc.AddBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void AddBus_BeforeAddBusHandlers_AutoSubscribesScannedHandlers()
    {
        // Handlers scanned after AddBus is called — order must not matter.
        using var provider = BuildProvider(sc =>
        {
            sc.AddBus();
            sc.AddBusHandlers(typeof(SimpleTestHandler).Assembly);
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // IMessageSerializer registration
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBus_RegistersJsonMessageSerializer_AsDefaultIMessageSerializer()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBus();
        });

        var serializer = provider.GetRequiredService<IMessageSerializer>();
        Assert.That(serializer, Is.InstanceOf<JsonMessageSerializer>());
    }

    [Test]
    public void AddBus_DoesNotOverride_CustomIMessageSerializerRegisteredBeforehand()
    {
        var customSerializer = new Mock<IMessageSerializer>().Object;
        using var provider = BuildProvider(sc =>
        {
            sc.AddSingleton(customSerializer);
            sc.AddBus();
        });

        var serializer = provider.GetRequiredService<IMessageSerializer>();
        Assert.That(serializer, Is.SameAs(customSerializer));
    }

    // ---------------------------------------------------------------------------
    // Test handler types
    // ---------------------------------------------------------------------------

    private sealed class SimpleTestHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TopicOnlyMessageTestHandler : IHandler<TopicOnlyMessage>
    {
        public Task HandleAsync(MessageContext<TopicOnlyMessage> context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
