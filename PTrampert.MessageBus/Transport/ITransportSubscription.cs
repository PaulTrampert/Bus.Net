using System;

namespace PTrampert.MessageBus.Transport;

/// <summary>
/// Handle returned by an active subscription.
/// </summary>
public interface ITransportSubscription : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Logical subscription name for diagnostics.
    /// </summary>
    string Name { get; }
}

