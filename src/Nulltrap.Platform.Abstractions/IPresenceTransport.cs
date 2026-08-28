namespace Nulltrap.Platform.Abstractions;

public interface IPresenceTransport : IDisposable
{
    bool IsConnected { get; }

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    void Disconnect();
}

public interface IPresenceTransportFactory
{
    IPresenceTransport Create();
}
