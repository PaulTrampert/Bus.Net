# Bus.Net [![NuGet Version](https://img.shields.io/nuget/v/Bus.Net.svg?style=flat-square)](https://www.nuget.org/packages/Bus.Net)

Bus.Net is a lightweight .NET messaging abstraction for publishing messages and handling them through pluggable transports. It provides a simple `IPublisher` API, handler-based message processing, and transport integrations like in-memory and RabbitMQ.

## Installation

Install the core library and the in-memory transport package:

```bash
dotnet add package Bus.Net
dotnet add package Bus.Net.Transports.InMemory
```

## Basic usage (InMemory)

```csharp
using Bus.Net;
using Bus.Net.Transports.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<GreetingHandler>();
builder.Services.AddBusInMemoryTransport(configure =>
{
    configure.RegisterHandler<GreetingHandler, GreetingMessage>();
});

using var host = builder.Build();
await host.StartAsync();

var publisher = host.Services.GetRequiredService<IPublisher>();
await publisher.PublishAsync(new GreetingMessage("Hello from Bus.Net"));

await host.StopAsync();

public sealed record GreetingMessage(string Text);

public sealed class GreetingHandler : IHandler<GreetingMessage>
{
    public Task HandleAsync(MessageContext<GreetingMessage> context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(context.Message.Text);
        return Task.CompletedTask;
    }
}
```

The message is routed by message type name by default unless a custom route is configured.
