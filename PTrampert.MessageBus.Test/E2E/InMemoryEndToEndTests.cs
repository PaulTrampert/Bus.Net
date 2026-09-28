using PTrampert.MessageBus.Transports.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace PTrampert.MessageBus.Test.E2E;

public class InMemoryEndToEndTests : EndToEndTestFixture
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddBusInMemoryTransport();
    }
}