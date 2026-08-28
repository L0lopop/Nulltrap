using Nulltrap.Core.Localization;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;

namespace Nulltrap.Core.Presence;

public sealed class PresenceService : IDisposable
{
    private const string PlaceUrl = "https://www.roblox.com/games/";

    private readonly DiscordPresenceClient _discord;
    private readonly GameInfoClient _games;
    private readonly SessionTracker _tracker;

    private CancellationTokenSource _work = new();

    public PresenceService(DiscordPresenceClient discord, GameInfoClient games, SessionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(discord);
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(tracker);

        _discord = discord;
        _games = games;
        _tracker = tracker;
    }

    public bool ShowGameButton { get; set; } = true;

    public PresenceActivity? Last { get; private set; }

    public void Start()
    {
        _tracker.Joining += OnJoining;
        _tracker.Joined += OnJoined;
        _tracker.Left += OnLeft;
    }

    public void Dispose()
    {
        _tracker.Joining -= OnJoining;
        _tracker.Joined -= OnJoined;
        _tracker.Left -= OnLeft;

        _work.Cancel();
        _work.Dispose();
    }

    internal async Task<PresenceActivity> DescribeAsync(
        RobloxSession session,
        CancellationToken cancellationToken)
    {
        GameInfo? game = await _games.DescribeAsync(session.UniverseId, cancellationToken)
            .ConfigureAwait(false);

        var buttons = new List<PresenceButton>();

        if (ShowGameButton && session.PlaceId > 0)
        {
            buttons.Add(new PresenceButton(
                Strings.Get("presence.viewGame"),
                PlaceUrl + session.PlaceId));
        }

        return new PresenceActivity
        {
            Details = game?.Name ?? Strings.Get("presence.unknownGame"),
            State = game?.CreatorName is null
                ? Strings.Get("presence.playing")
                : Strings.Get("presence.byCreator", game.CreatorName),
            StartedAt = session.StartedAt,
            LargeImage = game?.IconUrl,
            LargeText = game?.Name,
            Buttons = buttons,
        };
    }

    private void OnJoining(object? sender, RobloxSession session) => Push(new PresenceActivity
    {
        Details = Strings.Get("presence.joining"),
        StartedAt = session.StartedAt,
    });

    private void OnJoined(object? sender, RobloxSession session)
    {
        CancellationToken token = Restart();

        _ = Task.Run(
            async () =>
            {
                PresenceActivity activity = await DescribeAsync(session, token).ConfigureAwait(false);
                Last = activity;
                await _discord.SetAsync(activity, token).ConfigureAwait(false);
            },
            token);
    }

    private void OnLeft(object? sender, RobloxSession session)
    {
        CancellationToken token = Restart();
        Last = null;

        _ = Task.Run(() => _discord.ClearAsync(token), token);
    }

    private void Push(PresenceActivity activity)
    {
        CancellationToken token = Restart();
        Last = activity;

        _ = Task.Run(() => _discord.SetAsync(activity, token), token);
    }

    private CancellationToken Restart()
    {
        CancellationTokenSource previous = _work;
        _work = new CancellationTokenSource();

        previous.Cancel();
        previous.Dispose();

        return _work.Token;
    }
}
