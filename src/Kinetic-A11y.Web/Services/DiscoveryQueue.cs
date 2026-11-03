using System.Threading.Channels;

namespace Kinetic;

public sealed class DiscoveryQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    public ValueTask EnqueueAsync(Guid runId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(runId, ct);
    public ValueTask<Guid> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
