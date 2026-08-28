using Nulltrap.Core.Roblox;

namespace Nulltrap.Core.Sessions;

public sealed class SessionRecorder : IDisposable
{
    private static readonly TimeSpan Shortest = TimeSpan.FromSeconds(20);

    private readonly SessionTracker _tracker;
    private readonly GameInfoClient _games;
    private readonly SessionHistoryStore _store;
    private readonly Lock _gate = new();

    private Task<GameInfo?>? _lookup;
    private long _lookingUp;

    public SessionRecorder(SessionTracker tracker, GameInfoClient games, SessionHistoryStore store)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(store);

        _tracker = tracker;
        _games = games;
        _store = store;
    }

    public event EventHandler<PlayedSession>? Recorded;

    public void Start()
    {
        _tracker.Joined += OnJoined;
        _tracker.Left += OnLeft;
    }

    public void Dispose()
    {
        _tracker.Joined -= OnJoined;
        _tracker.Left -= OnLeft;
    }

    public static PlayedSession Describe(RobloxSession session, GameInfo? game)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new PlayedSession
        {
            PlaceId = session.PlaceId,
            UniverseId = session.UniverseId,
            Name = game?.Name,
            Creator = game?.CreatorName,
            Server = session.ServerAddress,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt ?? DateTimeOffset.UtcNow,
        };
    }

    private void OnJoined(object? sender, RobloxSession session)
    {
        if (session.UniverseId <= 0)
        {
            return;
        }

        lock (_gate)
        {
            _lookingUp = session.UniverseId;
            _lookup = _games.DescribeAsync(session.UniverseId);
        }
    }

    private void OnLeft(object? sender, RobloxSession session)
    {
        if (!session.IsIdentified)
        {
            return;
        }

        Task<GameInfo?>? lookup;

        lock (_gate)
        {
            lookup = _lookingUp == session.UniverseId ? _lookup : null;
            _lookup = null;
            _lookingUp = 0;
        }

        _ = Task.Run(async () =>
        {
            GameInfo? game = null;

            if (lookup is not null)
            {
                try
                {
                    game = await lookup.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is TimeoutException or HttpRequestException)
                {
                }
            }

            PlayedSession played = Describe(session, game);

            if (played.Duration < Shortest)
            {
                return;
            }

            _store.Add(played);
            Recorded?.Invoke(this, played);
        });
    }
}
