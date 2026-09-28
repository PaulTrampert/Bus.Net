using Bus.Net.Transports.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Bus.Net.Test.E2E;

public class InMemoryEndToEndTests : EndToEndTestFixture
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddBussyInMemoryTransport();
    }
}