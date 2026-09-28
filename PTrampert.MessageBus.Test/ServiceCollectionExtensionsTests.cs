using PTrampert.MessageBus.Registries;
using PTrampert.MessageBus.Test.TestMessageTypes;
using PTrampert.MessageBus.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace PTrampert.MessageBus.Test;

[TestFixture]
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Builds a minimal service provider (without starting a hosted service) that contains
    /// the core PTrampert.MessageBus registrations so the <see cref="BusConfigurator"/> singleton
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
    public void AddPTrampertMessageBus_AutoDiscovers_HandlerRegisteredAsConcrete()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddPTrampertMessageBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
        Assert.That(registry.Handlers[route], Has.Exactly(1).Items);
    }

    [Test]
    public void AddPTrampertMessageBus_AutoDiscovers_HandlerRegisteredAsInterface()
    {
        using var provider = BuildProvider(sc =>
        {
            // Register as the IHandler<> interface for DI injection (e.g. via constructor).
            // Also register by concrete type so InboundMessageHandler can resolve it with
            // GetRequiredService(handlerType) — Bus always resolves handlers by their concrete type.
            sc.AddScoped<IHandler<TestMessage>, SimpleTestHandler>();
            sc.AddScoped<SimpleTestHandler>();
            sc.AddPTrampertMessageBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void AddPTrampertMessageBus_NoConfigure_NoHandlers_RegistryIsEmpty()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddPTrampertMessageBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        Assert.That(registry.Handlers, Is.Empty);
    }

    // ---------------------------------------------------------------------------
    // Duplicate prevention
    // ---------------------------------------------------------------------------

    [Test]
    public void AddPTrampertMessageBus_DoesNotDuplicate_WhenHandlerAlreadyExplicitlyRegistered()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddPTrampertMessageBus(cfg =>
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
    public void AddPTrampertMessageBus_DoesNotDuplicate_WhenHandlerExplicitlyRegisteredWithCustomRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddPTrampertMessageBus(cfg =>
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
    public void AddPTrampertMessageBus_AutoDiscovers_HandlerForAttributedMessage_UsesAttributeRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<TopicOnlyMessageTestHandler>();
            sc.AddPTrampertMessageBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: "topic-only", Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // configure callback still works independently
    // ---------------------------------------------------------------------------

    [Test]
    public void AddPTrampertMessageBus_ExplicitConfigure_WorksWithoutDiRegistration()
    {
        using var provider = BuildProvider(sc =>
        {
            // Handler is NOT registered in DI as a service — only declared via configure.
            sc.AddPTrampertMessageBus(cfg =>
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
    public void AddPTrampertMessageBus_RegistersBusService_AsHostedService()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddPTrampertMessageBus();
        });

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.That(hostedServices, Has.Exactly(1).InstanceOf<BusService>());
    }

    // ---------------------------------------------------------------------------
    // AddPTrampertMessageBusHandlers + AddPTrampertMessageBus integration — call order
    // ---------------------------------------------------------------------------

    [Test]
    public void AddPTrampertMessageBusHandlers_BeforeAddPTrampertMessageBus_AutoSubscribesScannedHandlers()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddPTrampertMessageBusHandlers(typeof(SimpleTestHandler).Assembly);
            sc.AddPTrampertMessageBus();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void AddPTrampertMessageBus_BeforeAddPTrampertMessageBusHandlers_AutoSubscribesScannedHandlers()
    {
        // Handlers scanned after AddPTrampertMessageBus is called — order must not matter.
        using var provider = BuildProvider(sc =>
        {
            sc.AddPTrampertMessageBus();
            sc.AddPTrampertMessageBusHandlers(typeof(SimpleTestHandler).Assembly);
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // IMessageSerializer registration
    // ---------------------------------------------------------------------------

    [Test]
    public void AddPTrampertMessageBus_RegistersJsonMessageSerializer_AsDefaultIMessageSerializer()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddPTrampertMessageBus();
        });

        var serializer = provider.GetRequiredService<IMessageSerializer>();
        Assert.That(serializer, Is.InstanceOf<JsonMessageSerializer>());
    }

    [Test]
    public void AddPTrampertMessageBus_DoesNotOverride_CustomIMessageSerializerRegisteredBeforehand()
    {
        var customSerializer = new Mock<IMessageSerializer>().Object;
        using var provider = BuildProvider(sc =>
        {
            sc.AddSingleton(customSerializer);
            sc.AddPTrampertMessageBus();
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
