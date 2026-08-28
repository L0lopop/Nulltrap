namespace Nulltrap.Core.Presence;

public enum PresenceHeadline
{
    GameName,
    PlayingRoblox,
}

public enum PresenceSubline
{
    Creator,
    ServerRegion,
    PlayerCount,
    Nothing,
}

public sealed record PresenceOptions
{
    public static PresenceOptions Default { get; } = new();

    public PresenceHeadline Headline { get; init; } = PresenceHeadline.GameName;

    public PresenceSubline Subline { get; init; } = PresenceSubline.Creator;

    public bool ShowElapsed { get; init; } = true;

    public bool ShowGameIcon { get; init; } = true;

    public bool ShowGameButton { get; init; } = true;
}
