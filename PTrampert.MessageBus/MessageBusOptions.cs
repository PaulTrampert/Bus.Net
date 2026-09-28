namespace PTrampert.MessageBus;

/// <summary>
/// Transport-independent options for PTrampert.MessageBus. Configure with
/// <c>services.Configure&lt;MessageBusOptions&gt;(...)</c>.
/// </summary>
public sealed class MessageBusOptions
{
    /// <summary>
    /// Gets or sets the maximum number of times a message is delivered to a handler. A message whose handler
    /// still fails on the last attempt is dead-lettered instead of being retried again. Defaults to 5.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;
}
