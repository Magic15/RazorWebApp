using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RazorPagesWebApp;

public sealed record ScanStatusChanged(long DocumentId, string Status);

public sealed class ScanStatusEvents
{
    private readonly ConcurrentDictionary<Guid, Channel<ScanStatusChanged>> _subscribers = new();

    public (Guid Id, ChannelReader<ScanStatusChanged> Reader) Subscribe()
    {
        var channel = Channel.CreateBounded<ScanStatusChanged>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(ScanStatusChanged update)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(update);
    }
}
