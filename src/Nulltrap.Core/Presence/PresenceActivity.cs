using System.Text.Json.Serialization;

namespace Nulltrap.Core.Presence;

public sealed record PresenceButton(string Label, string Url);

public sealed record PresenceActivity
{
    public string? Details { get; init; }

    public string? State { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public string? LargeImage { get; init; }

    public string? LargeText { get; init; }

    public string? SmallImage { get; init; }

    public string? SmallText { get; init; }

    public IReadOnlyList<PresenceButton> Buttons { get; init; } = [];
}

internal sealed record ActivityPayload
{
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; init; }

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    [JsonPropertyName("timestamps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimestampsPayload? Timestamps { get; init; }

    [JsonPropertyName("assets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssetsPayload? Assets { get; init; }

    [JsonPropertyName("buttons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ButtonPayload>? Buttons { get; init; }
}

internal sealed record TimestampsPayload
{
    [JsonPropertyName("start")]
    public long Start { get; init; }
}

internal sealed record AssetsPayload
{
    [JsonPropertyName("large_image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargeImage { get; init; }

    [JsonPropertyName("large_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargeText { get; init; }

    [JsonPropertyName("small_image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SmallImage { get; init; }

    [JsonPropertyName("small_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SmallText { get; init; }
}

internal sealed record ButtonPayload
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
