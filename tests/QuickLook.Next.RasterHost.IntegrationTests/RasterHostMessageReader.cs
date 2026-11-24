using System.Threading.Channels;
using QuickLook.Next.Core;

namespace QuickLook.Next.RasterHost.IntegrationTests;

// Match the App's independent receive loop even while a test is sending a burst of controls.
// In particular, a host publishing under its surface gate must be able to finish its pipe write
// before its control loop can acquire that gate for close/reopen. Keep queued output bounded.
internal sealed class RasterHostMessageReader : IAsyncDisposable
{
    private readonly Channel<ControlMessage> _messages = Channel.CreateBounded<ControlMessage>(
        new BoundedChannelOptions(16) { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _shutdown;
    private readonly Task _pump;

    public RasterHostMessageReader(PipeChannel channel, CancellationToken cancellationToken)
    {
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pump = PumpAsync(channel);
    }

    public async Task<ControlMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (await _messages.Reader.WaitToReadAsync(cancellationToken))
            return await _messages.Reader.ReadAsync(cancellationToken);
        return null;
    }

    private async Task PumpAsync(PipeChannel channel)
    {
        Exception? failure = null;
        try
        {
            while (await channel.ReceiveAsync(_shutdown.Token) is { } message)
                await _messages.Writer.WriteAsync(message, _shutdown.Token);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception) { failure = exception; }
        finally { _messages.Writer.TryComplete(failure); }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        await _pump;
        _shutdown.Dispose();
    }
}
