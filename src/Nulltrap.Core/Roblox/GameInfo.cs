using System.Text.Json.Serialization;

namespace Nulltrap.Core.Roblox;

public sealed record GameInfo
{
    public required long UniverseId { get; init; }

    public required string Name { get; init; }

    public string? CreatorName { get; init; }

    public long RootPlaceId { get; init; }

    public int Playing { get; init; }

    public string? IconUrl { get; init; }

    public int Likes { get; init; }

    public int Dislikes { get; init; }

    public string? Genre { get; init; }
}

internal sealed record GamesResponse
{
    [JsonPropertyName("data")]
    public List<GameRecord> Data { get; init; } = [];
}

internal sealed record GameRecord
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("rootPlaceId")]
    public long RootPlaceId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("playing")]
    public int Playing { get; init; }

    [JsonPropertyName("creator")]
    public CreatorRecord? Creator { get; init; }

    [JsonPropertyName("genre_l1")]
    public string? Genre { get; init; }
}

internal sealed record CreatorRecord
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed record VotesResponse
{
    [JsonPropertyName("data")]
    public List<VoteRecord> Data { get; init; } = [];
}

internal sealed record VoteRecord
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("upVotes")]
    public int UpVotes { get; init; }

    [JsonPropertyName("downVotes")]
    public int DownVotes { get; init; }
}

internal sealed record ThumbnailsResponse
{
    [JsonPropertyName("data")]
    public List<ThumbnailRecord> Data { get; init; } = [];
}

internal sealed record ThumbnailRecord
{
    [JsonPropertyName("targetId")]
    public long TargetId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }
}
