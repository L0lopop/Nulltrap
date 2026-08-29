namespace Nulltrap.Core.Presence;

[Flags]
public enum PresenceHeadline
{
    Nothing = 0,
    GameName = 1,
    PlayingRoblox = 2,
}

[Flags]
public enum PresenceSubline
{
    Nothing = 0,
    Creator = 1,
    PlayerCount = 2,
    ServerRegion = 4,
}

public sealed record PresenceOptions
{
    public static PresenceOptions Default { get; } = new();

    public PresenceHeadline Headline { get; init; } = PresenceHeadline.GameName;

    public PresenceSubline Subline { get; init; } = PresenceSubline.Creator;

    public bool ShowElapsed { get; init; } = true;

    public bool ShowGameIcon { get; init; } = true;

    public bool ShowGameButton { get; init; } = true;

    public bool ShowAccount { get; init; } = true;

    public bool AllowJoin { get; init; } = true;
}
