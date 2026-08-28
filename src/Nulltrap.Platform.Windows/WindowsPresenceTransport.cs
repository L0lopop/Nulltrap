using System.IO.Pipes;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsPresenceTransportFactory : IPresenceTransportFactory
{
    public IPresenceTransport Create() => new WindowsPresenceTransport();
}

public sealed class WindowsPresenceTransport : IPresenceTransport
{
    private const int PipeCount = 10;
    private const int ConnectTimeoutMilliseconds = 500;

    private NamedPipeClientStream? _pipe;

    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        Disconnect();

        for (int index = 0; index < PipeCount; index++)
        {
            var pipe = new NamedPipeClientStream(
                ".", $"discord-ipc-{index}", PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(ConnectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                _pipe = pipe;
                return true;
            }
            catch (Exception failure) when (failure is TimeoutException or IOException or UnauthorizedAccessException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        return false;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (_pipe is null)
        {
            throw new InvalidOperationException("Not connected to Discord.");
        }

        await _pipe.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_pipe is null)
        {
            return 0;
        }

        return await _pipe.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose() => Disconnect();
}
