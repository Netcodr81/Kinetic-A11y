using System.Threading.Channels;

namespace Kinetic;

/// <summary>Provides an in-memory FIFO queue of discovery-run identifiers for the background worker.</summary>
public sealed class DiscoveryQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    /// <summary>Adds a discovery run to the queue.</summary>
    /// <param name="runId">The identifier of the run to enqueue.</param>
    /// <param name="ct">A token that cancels the enqueue operation.</param>
    public ValueTask EnqueueAsync(Guid runId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(runId, ct);

    /// <summary>Waits for and removes the next discovery-run identifier from the queue.</summary>
    /// <param name="ct">A token that cancels the wait.</param>
    /// <returns>The identifier of the next queued run.</returns>
    public ValueTask<Guid> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
