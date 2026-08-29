using Nulltrap.Core.Localization;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;

namespace Nulltrap.Core.Presence;

public sealed class PresenceService : IDisposable
{
    public const string BuiltInApplicationId = "1542960701226622976";

    private const string PlaceUrl = "https://www.roblox.com/games/";
    private const string JoinUrl = "https://www.roblox.com/games/start?placeId=";
    private const string FallbackImage = "nulltrap";

    private readonly DiscordPresenceClient _discord;
    private readonly GameInfoClient _games;
    private readonly SessionTracker _tracker;
    private readonly AccountInfoClient? _accounts;

    private CancellationTokenSource _work = new();

    public PresenceService(
        DiscordPresenceClient discord,
        GameInfoClient games,
        SessionTracker tracker,
        AccountInfoClient? accounts = null)
    {
        ArgumentNullException.ThrowIfNull(discord);
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(tracker);

        _discord = discord;
        _games = games;
        _tracker = tracker;
        _accounts = accounts;
    }

    public PresenceOptions Options { get; set; } = PresenceOptions.Default;

    public PresenceActivity? Last { get; private set; }

    public static string ApplicationId(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? BuiltInApplicationId : configured.Trim();

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

    public async Task<PresenceActivity> DescribeAsync(
        RobloxSession session,
        CancellationToken cancellationToken = default)
    {
        GameInfo? game = await _games.DescribeAsync(session.UniverseId, cancellationToken)
            .ConfigureAwait(false);

        AccountInfo? account = Options.ShowAccount && _accounts is not null
            ? await _accounts.DescribeAsync(session.UserId, cancellationToken).ConfigureAwait(false)
            : null;

        return Compose(session, game, Options, account);
    }

    public static PresenceActivity Compose(
        RobloxSession session,
        GameInfo? game,
        PresenceOptions options,
        AccountInfo? account = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);

        var buttons = new List<PresenceButton>();

        if (options.AllowJoin && session.PlaceId > 0 && session.JobId is not null)
        {
            buttons.Add(new PresenceButton(
                Strings.Get("presence.joinServer"),
                $"{JoinUrl}{session.PlaceId}&gameInstanceId={session.JobId}"));
        }

        if (options.ShowGameButton && session.PlaceId > 0)
        {
            buttons.Add(new PresenceButton(Strings.Get("presence.viewGame"), PlaceUrl + session.PlaceId));
        }

        string headline = Headline(game, options);

        bool named = options.ShowAccount && account is not null;

        return new PresenceActivity
        {
            Details = headline,
            State = Subline(session, game, options),
            StartedAt = options.ShowElapsed ? session.StartedAt : null,
            LargeImage = options.ShowGameIcon ? game?.IconUrl ?? FallbackImage : null,
            LargeText = game?.Name,
            SmallImage = named ? account!.AvatarUrl : null,
            SmallText = named ? account!.Name : null,
            Buttons = buttons,
        };
    }

    private static string Headline(GameInfo? game, PresenceOptions options)
    {
        var parts = new List<string>();

        if (options.Headline.HasFlag(PresenceHeadline.GameName) && game is not null)
        {
            parts.Add(game.Name);
        }

        if (options.Headline.HasFlag(PresenceHeadline.PlayingRoblox) || parts.Count == 0)
        {
            parts.Add(Strings.Get("presence.playing"));
        }

        return string.Join(" · ", parts);
    }

    private static string? Subline(RobloxSession session, GameInfo? game, PresenceOptions options)
    {
        var parts = new List<string>();

        if (options.Subline.HasFlag(PresenceSubline.Creator) && game?.CreatorName is not null)
        {
            parts.Add(Strings.Get("presence.byCreator", game.CreatorName));
        }

        if (options.Subline.HasFlag(PresenceSubline.PlayerCount) && game is { Playing: > 0 })
        {
            parts.Add(Strings.Get("presence.playerCount", game.Playing.ToString("N0")));
        }

        if (options.Subline.HasFlag(PresenceSubline.ServerRegion) && session.ServerAddress is not null)
        {
            parts.Add(Strings.Get("presence.onServer", session.ServerAddress));
        }

        if (options.AllowJoin && session.JobId is not null)
        {
            parts.Add(Strings.Get("presence.publicServer"));
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private void OnJoining(object? sender, RobloxSession session) => Push(new PresenceActivity
    {
        Details = Strings.Get("presence.joining"),
        StartedAt = Options.ShowElapsed ? session.StartedAt : null,
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
