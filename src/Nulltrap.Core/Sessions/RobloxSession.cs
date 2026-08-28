namespace Nulltrap.Core.Sessions;

public enum SessionState
{
    Idle,
    Joining,
    Playing,
}

public sealed record RobloxSession
{
    public string? JobId { get; init; }

    public long PlaceId { get; init; }

    public long UniverseId { get; init; }

    public long UserId { get; init; }

    public string? MachineAddress { get; init; }

    public string? ServerAddress { get; init; }

    public int ServerPort { get; init; }

    public string? ReferralPage { get; init; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; init; }

    public TimeSpan Duration => (EndedAt ?? DateTimeOffset.UtcNow) - StartedAt;

    public bool IsIdentified => PlaceId > 0;

    public string? ServerEndpoint =>
        ServerAddress is null ? null : $"{ServerAddress}:{ServerPort}";
}
