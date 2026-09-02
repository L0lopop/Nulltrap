using System.Text.Json.Serialization;

using Nulltrap.Core.Localization;

namespace Nulltrap.Core.Sessions;

public sealed record PlayedSession
{
    [JsonPropertyName("placeId")]
    public long PlaceId { get; init; }

    [JsonPropertyName("universeId")]
    public long UniverseId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("creator")]
    public string? Creator { get; init; }

    [JsonPropertyName("server")]
    public string? Server { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset EndedAt { get; init; }

    [JsonIgnore]
    public TimeSpan Duration => EndedAt > StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;
}

public sealed record PlayedGame(long UniverseId, string Name, TimeSpan Total, int Visits, DateTimeOffset LastPlayed);

public sealed class SessionHistory
{
    public const int Capacity = 250;

    [JsonPropertyName("sessions")]
    public List<PlayedSession> Sessions { get; init; } = [];

    public void Add(PlayedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Sessions.Insert(0, session);

        if (Sessions.Count > Capacity)
        {
            Sessions.RemoveRange(Capacity, Sessions.Count - Capacity);
        }
    }

    public TimeSpan Total() => Sessions.Aggregate(TimeSpan.Zero, (sum, played) => sum + played.Duration);

    public TimeSpan Since(DateTimeOffset moment) => Sessions
        .Where(played => played.EndedAt >= moment)
        .Aggregate(TimeSpan.Zero, (sum, played) => sum + played.Duration);

    public IReadOnlyList<PlayedGame> ByGame(int take) => Sessions
        .Where(played => played.UniverseId > 0)
        .GroupBy(played => played.UniverseId)
        .Select(group => new PlayedGame(
            group.Key,
            group.Select(played => played.Name).FirstOrDefault(name => name is not null)
                ?? Strings.Get("activity.unknownGame"),
            group.Aggregate(TimeSpan.Zero, (sum, played) => sum + played.Duration),
            group.Count(),
            group.Max(played => played.EndedAt)))
        .OrderByDescending(game => game.Total)
        .Take(take)
        .ToList();
}

public static class Clocks
{
    public static string Describe(TimeSpan span) => Spell(span, int.MaxValue);

    public static string Short(TimeSpan span) => Spell(span, 2);

    private static string Spell(TimeSpan span, int units)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        (int Amount, string Key)[] parts =
        [
            ((int)span.TotalDays, "time.days"),
            (span.Hours, "time.hours"),
            (span.Minutes, "time.minutes"),
            (span.Seconds, "time.seconds"),
        ];

        string[] said = parts
            .Where(part => part.Amount > 0)
            .Take(units)
            .Select(part => Strings.Get(part.Key, part.Amount))
            .ToArray();

        return said.Length == 0 ? Strings.Get("time.seconds", 0) : string.Join(' ', said);
    }
}
