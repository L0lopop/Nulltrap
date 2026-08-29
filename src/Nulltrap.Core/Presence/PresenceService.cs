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

        string headline = options.Headline == PresenceHeadline.PlayingRoblox || game is null
            ? Strings.Get("presence.playing")
            : game.Name;

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

    private static string Where(RobloxSession session, PresenceOptions options) =>
        options.AllowJoin && session.JobId is not null
            ? " | " + Strings.Get("presence.publicServer")
            : string.Empty;

    private static string? Subline(RobloxSession session, GameInfo? game, PresenceOptions options) =>
        options.Subline switch
        {
            PresenceSubline.Creator when game?.CreatorName is not null =>
                Strings.Get("presence.byCreator", game.CreatorName) + Where(session, options),
            PresenceSubline.ServerRegion when session.ServerAddress is not null =>
                Strings.Get("presence.onServer", session.ServerAddress),
            PresenceSubline.PlayerCount when game is { Playing: > 0 } =>
                Strings.Get("presence.playerCount", game.Playing.ToString("N0")),
            PresenceSubline.Nothing => null,
            _ => game is null ? null : Strings.Get("presence.unknownGame"),
        };

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
