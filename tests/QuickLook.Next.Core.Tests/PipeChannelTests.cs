using System.IO.Pipes;
using System.Text;
using QuickLook.Next.Core;
using Xunit;

namespace QuickLook.Next.Core.Tests;

public sealed class PipeChannelTests
{
    [Fact]
    public async Task Round_trips_pdf_page_error()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var sender = new PipeChannel(pipes.Server);
        using var receiver = new PipeChannel(pipes.Client);
        var expected = new PreviewPageError("request", 7, 42, true, "render timed out");

        Task<ControlMessage?> receive = receiver.ReceiveAsync();
        await sender.SendAsync(expected).WaitAsync(Timeout);

        Assert.Equal(expected, Assert.IsType<PreviewPageError>(await receive.WaitAsync(Timeout)));
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Channels_send_and_receive_in_both_directions()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var left = new PipeChannel(pipes.Server);
        using var right = new PipeChannel(pipes.Client);
        var fromLeft = new Hello(1234, "token");
        var fromRight = new PreviewError("request", "failed");

        Task<ControlMessage?> receiveLeft = left.ReceiveAsync();
        Task<ControlMessage?> receiveRight = right.ReceiveAsync();
        await Task.WhenAll(left.SendAsync(fromLeft), right.SendAsync(fromRight)).WaitAsync(Timeout);

        Assert.Equal(fromRight, Assert.IsType<PreviewError>(await receiveLeft.WaitAsync(Timeout)));
        Assert.Equal(fromLeft, Assert.IsType<Hello>(await receiveRight.WaitAsync(Timeout)));
    }

    [Fact]
    public async Task Preview_error_round_trips_stable_codec_fields()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var sender = new PipeChannel(pipes.Server);
        using var receiver = new PipeChannel(pipes.Client);
        var expected = new PreviewError("request", "A Windows image codec is required.")
        {
            Code = PreviewErrorCodes.ImageCodecRequired,
            Format = "avif",
        };

        Task<ControlMessage?> receive = receiver.ReceiveAsync();
        await sender.SendAsync(expected).WaitAsync(Timeout);

        Assert.Equal(expected, Assert.IsType<PreviewError>(await receive.WaitAsync(Timeout)));
    }

    [Fact]
    public async Task Internal_preview_error_round_trips_bounded_wire_fields()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var sender = new PipeChannel(pipes.Server);
        using var receiver = new PipeChannel(pipes.Client);
        var error = PreviewError.InternalFailure("request", "handle-open", ".PNG");
        Task<ControlMessage?> receive = receiver.ReceiveAsync();
        await sender.SendAsync(error).WaitAsync(Timeout);
        var received = Assert.IsType<PreviewError>(await receive.WaitAsync(Timeout));

        Assert.Equal(error, received);
        Assert.Equal("Preview failed.", received.Message);
        Assert.Equal(PreviewErrorCodes.ContentFailed, received.Code);
        Assert.Equal("png", received.Format);
        Assert.Equal("handle-open", received.Phase);
    }

    [Theory]
    [InlineData("C:\\Users\\alice\\private.png")]
    [InlineData("private\r\npath=secret")]
    [InlineData("用户秘密")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Internal_preview_error_drops_invalid_metadata(string hostileValue)
    {
        var error = PreviewError.InternalFailure("request", hostileValue, hostileValue);
        string payload = ProtocolJson.Serialize(error);

        Assert.Equal("unknown", error.Phase);
        Assert.Equal("unknown", error.Format);
        Assert.DoesNotContain(hostileValue, payload, StringComparison.Ordinal);
        Assert.InRange(payload.Length, 1, 256);
    }

    [Fact]
    public void Legacy_preview_error_without_phase_remains_readable()
    {
        const string legacyJson = """{"type":"preview.error","requestId":"request","message":"failed"}""";
        var error = Assert.IsType<PreviewError>(ProtocolJson.Deserialize(legacyJson));

        Assert.Equal("request", error.RequestId);
        Assert.Equal("failed", error.Message);
        Assert.Null(error.Phase);
    }

    [Fact]
    public async Task Receive_accepts_crlf_and_unterminated_final_line()
    {
        var first = new PreviewError("first", "one");
        var second = new PreviewClose("second");
        byte[] bytes = Encoding.UTF8.GetBytes(ProtocolJson.Serialize(first) + "\r\n" + ProtocolJson.Serialize(second));
        using var receiver = new PipeChannel(new MemoryStream(bytes, writable: true));

        Assert.Equal(first, Assert.IsType<PreviewError>(await receiver.ReceiveAsync().WaitAsync(Timeout)));
        Assert.Equal(second, Assert.IsType<PreviewClose>(await receiver.ReceiveAsync().WaitAsync(Timeout)));
        Assert.Null(await receiver.ReceiveAsync().WaitAsync(Timeout));
    }

    [Fact]
    public async Task Send_rejects_oversized_output_and_channel_remains_usable()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var sender = new PipeChannel(pipes.Server);
        using var receiver = new PipeChannel(pipes.Client);
        var oversized = new PreviewError("large", new string('x', PipeChannel.MaxControlLineChars));

        await Assert.ThrowsAsync<InvalidDataException>(() => sender.SendAsync(oversized));
        var valid = new PreviewClose("valid");
        Task<ControlMessage?> receive = receiver.ReceiveAsync();
        await sender.SendAsync(valid).WaitAsync(Timeout);
        Assert.Equal(valid, Assert.IsType<PreviewClose>(await receive.WaitAsync(Timeout)));
    }

    [Fact]
    public async Task Receive_honors_cancellation_and_channel_remains_usable()
    {
        await using var pipes = await ConnectedPipePair.CreateAsync();
        using var receiver = new PipeChannel(pipes.Server);
        using var sender = new PipeChannel(pipes.Client);
        using var cancellation = new CancellationTokenSource();
        Task<ControlMessage?> canceled = receiver.ReceiveAsync(cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        var valid = new PreviewError("valid", "ok");
        Task<ControlMessage?> receive = receiver.ReceiveAsync();
        await sender.SendAsync(valid).WaitAsync(Timeout);
        Assert.Equal(valid, Assert.IsType<PreviewError>(await receive.WaitAsync(Timeout)));
    }

    [Fact]
    public async Task Send_cancellation_after_a_write_prefix_closes_the_channel()
    {
        using var stream = new PausingWriteStream();
        using var sender = new PipeChannel(stream);
        using var cancellation = new CancellationTokenSource();
        var thumbnail = new PreviewImageThumbnail("canceled", 32, 32, new byte[32 * 32 * 4]);
        Task send = sender.SendAsync(thumbnail, cancellation.Token);
        await stream.Started.Task.WaitAsync(Timeout);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(Timeout));
        Assert.True(stream.WasDisposed, "A partially written frame must close the transport.");
        await Assert.ThrowsAsync<IOException>(() => sender.SendAsync(new PreviewClose("replacement")).WaitAsync(Timeout));
    }

    [Fact]
    public async Task Send_cancels_a_queued_frame_without_writing_it()
    {
        using var stream = new PausingWriteStream();
        using var sender = new PipeChannel(stream);
        Task first = sender.SendAsync(new PreviewClose("first"));
        await stream.Started.Task.WaitAsync(Timeout);

        using var cancellation = new CancellationTokenSource();
        Task queued = sender.SendAsync(new PreviewClose("canceled"), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Timeout));
        stream.Resume.TrySetResult();
        await first.WaitAsync(Timeout);
        await sender.SendAsync(new PreviewClose("last")).WaitAsync(Timeout);

        using var receiver = new PipeChannel(new MemoryStream(stream.ToArray()));
        Assert.Equal("first", Assert.IsType<PreviewClose>(await receiver.ReceiveAsync().WaitAsync(Timeout)).RequestId);
        Assert.Equal("last", Assert.IsType<PreviewClose>(await receiver.ReceiveAsync().WaitAsync(Timeout)).RequestId);
        Assert.Null(await receiver.ReceiveAsync().WaitAsync(Timeout));
    }

    private sealed class PausingWriteStream : Stream
    {
        private readonly MemoryStream _bytes = new();
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasDisposed { get; private set; }
        public byte[] ToArray() => _bytes.ToArray();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _bytes.Length;
        public override long Position { get => _bytes.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => _bytes.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _bytes.Write(buffer, offset, count);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!Started.Task.IsCompleted)
            {
                int prefixLength = Math.Max(1, buffer.Length / 2);
                await _bytes.WriteAsync(buffer[..prefixLength], cancellationToken);
                Started.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
                buffer = buffer[prefixLength..];
            }
            await _bytes.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                _bytes.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ConnectedPipePair : IAsyncDisposable
    {
        private bool _clientDisposed;
        private ConnectedPipePair(NamedPipeServerStream server, NamedPipeClientStream client) => (Server, Client) = (server, client);
        public NamedPipeServerStream Server { get; }
        public NamedPipeClientStream Client { get; }

        public static async Task<ConnectedPipePair> CreateAsync()
        {
            string name = $"QuickLook.Next.Tests.{Environment.ProcessId}.{Guid.NewGuid():N}";
            var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                using var cancellation = new CancellationTokenSource(Timeout);
                await Task.WhenAll(server.WaitForConnectionAsync(cancellation.Token), client.ConnectAsync(cancellation.Token)).WaitAsync(Timeout);
                return new ConnectedPipePair(server, client);
            }
            catch
            {
                client.Dispose();
                server.Dispose();
                throw;
            }
        }

        public void DisposeClient()
        {
            if (!_clientDisposed)
            {
                _clientDisposed = true;
                Client.Dispose();
            }
        }

        public ValueTask DisposeAsync()
        {
            DisposeClient();
            Server.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
