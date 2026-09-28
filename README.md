# PTrampert.MessageBus [![NuGet Version](https://img.shields.io/nuget/v/PTrampert.MessageBus.svg?style=flat-square)](https://www.nuget.org/packages/PTrampert.MessageBus)

PTrampert.MessageBus is a lightweight .NET messaging abstraction for publishing messages and handling them through pluggable transports. It provides a simple `IPublisher` API, handler-based message processing, and transport integrations like in-memory and RabbitMQ.

## Installation

Install the core library and the in-memory transport package:

```bash
dotnet add package PTrampert.MessageBus
dotnet add package PTrampert.MessageBus.Transports.InMemory
```

## Basic usage (InMemory)

```csharp
using PTrampert.MessageBus;
using PTrampert.MessageBus.Transports.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<GreetingHandler>();
builder.Services.AddPTrampertMessageBusInMemoryTransport(configure =>
{
    configure.RegisterHandler<GreetingHandler, GreetingMessage>();
});

using var host = builder.Build();
await host.StartAsync();

var publisher = host.Services.GetRequiredService<IPublisher>();
await publisher.PublishAsync(new GreetingMessage("Hello from PTrampert.MessageBus"));

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
