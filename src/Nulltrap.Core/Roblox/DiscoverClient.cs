using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nulltrap.Core.Roblox;

public sealed record DiscoveredGame
{
    public required long UniverseId { get; init; }

    public required long RootPlaceId { get; init; }

    public required string Name { get; init; }

    public int Playing { get; init; }

    public int Likes { get; init; }

    public int Dislikes { get; init; }

    public string? Genre { get; init; }
}

public sealed class DiscoverClient
{
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(30);

    private const string SortsUrl =
        "https://apis.roblox.com/explore-api/v1/get-sorts?device=computer&country=all&sessionId=";

    private static readonly string[] Wanted = ["top-trending", "top-playing-now", "up-and-coming", "top-revisited"];

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<DiscoveredGame> _cached = [];
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public DiscoverClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<IReadOnlyList<DiscoveredGame>> PopularAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow - _fetchedAt < Freshness && _cached.Count > 0)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (DateTimeOffset.UtcNow - _fetchedAt < Freshness && _cached.Count > 0)
            {
                return _cached;
            }

            SortsResponse? response = await FetchAsync(cancellationToken).ConfigureAwait(false);

            if (response is null)
            {
                return _cached;
            }

            _cached = Flatten(response);
            _fetchedAt = DateTimeOffset.UtcNow;

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static IReadOnlyList<DiscoveredGame> Flatten(SortsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var seen = new HashSet<long>();
        var games = new List<DiscoveredGame>();

        foreach (SortRecord sort in response.Sorts.Where(sort => Wanted.Contains(sort.SortId)))
        {
            foreach (GameCardRecord card in sort.Games)
            {
                if (card.Sponsored
                    || card.UniverseId <= 0
                    || card.RootPlaceId <= 0
                    || string.IsNullOrWhiteSpace(card.Name)
                    || !seen.Add(card.UniverseId))
                {
                    continue;
                }

                games.Add(new DiscoveredGame
                {
                    UniverseId = card.UniverseId,
                    RootPlaceId = card.RootPlaceId,
                    Name = card.Name,
                    Playing = card.PlayerCount,
                    Likes = card.UpVotes,
                    Dislikes = card.DownVotes,
                    Genre = string.IsNullOrWhiteSpace(card.Genre) ? null : card.Genre,
                });
            }
        }

        return games;
    }

    private async Task<SortsResponse?> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .GetAsync(SortsUrl + Guid.NewGuid().ToString("d"), cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync<SortsResponse>(body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }
}

public sealed record SortsResponse
{
    [JsonPropertyName("sorts")]
    public List<SortRecord> Sorts { get; init; } = [];
}

public sealed record SortRecord
{
    [JsonPropertyName("sortId")]
    public string? SortId { get; init; }

    [JsonPropertyName("games")]
    public List<GameCardRecord> Games { get; init; } = [];
}

public sealed record GameCardRecord
{
    [JsonPropertyName("universeId")]
    public long UniverseId { get; init; }

    [JsonPropertyName("rootPlaceId")]
    public long RootPlaceId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; init; }

    [JsonPropertyName("totalUpVotes")]
    public int UpVotes { get; init; }

    [JsonPropertyName("totalDownVotes")]
    public int DownVotes { get; init; }

    [JsonPropertyName("isSponsored")]
    public bool Sponsored { get; init; }

    [JsonPropertyName("genreL1")]
    public string? Genre { get; init; }
}
