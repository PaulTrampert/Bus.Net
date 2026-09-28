using System.Collections.Concurrent;
using PTrampert.MessageBus.Transport;

namespace PTrampert.MessageBus.Registries;

internal class TransportRegistry
{
    public ConcurrentDictionary<string, ITransport> Transports { get; } = new();

    public void Register(ITransport transport)
    {
        Transports.TryAdd(transport.Name, transport);
    }
}