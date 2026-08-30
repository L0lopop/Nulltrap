using System.Collections.Concurrent;
using System.Text.Json;

namespace Nulltrap.Core.Roblox;

public sealed class GameInfoClient
{
    private const string GamesEndpoint = "https://games.roblox.com/v1/games?universeIds=";
    private const string IconsEndpoint =
        "https://thumbnails.roblox.com/v1/games/icons?size=512x512&format=Png&isCircular=false&universeIds=";
    private const string VotesEndpoint = "https://games.roblox.com/v1/games/votes?universeIds=";

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<long, GameInfo> _cache = new();

    public GameInfoClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<GameInfo?> DescribeAsync(long universeId, CancellationToken cancellationToken = default)
    {
        if (universeId <= 0)
        {
            return null;
        }

        if (_cache.TryGetValue(universeId, out GameInfo? cached))
        {
            return cached;
        }

        GameRecord? game = await FetchAsync<GamesResponse>(GamesEndpoint + universeId, cancellationToken)
            .ConfigureAwait(false) is { Data.Count: > 0 } games
            ? games.Data[0]
            : null;

        if (game is null || game.RootPlaceId == 0 || string.IsNullOrWhiteSpace(game.Name))
        {
            return null;
        }

        VoteRecord? votes = await FetchAsync<VotesResponse>(VotesEndpoint + universeId, cancellationToken)
            .ConfigureAwait(false) is { Data.Count: > 0 } counted
            ? counted.Data[0]
            : null;

        var info = new GameInfo
        {
            UniverseId = universeId,
            Name = game.Name,
            CreatorName = game.Creator?.Name,
            RootPlaceId = game.RootPlaceId,
            Playing = game.Playing,
            Likes = votes?.UpVotes ?? 0,
            Dislikes = votes?.DownVotes ?? 0,
            Genre = string.IsNullOrWhiteSpace(game.Genre) ? null : game.Genre,
            IconUrl = await IconAsync(universeId, cancellationToken).ConfigureAwait(false),
        };

        _cache[universeId] = info;
        return info;
    }

    private async Task<string?> IconAsync(long universeId, CancellationToken cancellationToken)
    {
        ThumbnailsResponse? response =
            await FetchAsync<ThumbnailsResponse>(IconsEndpoint + universeId, cancellationToken)
                .ConfigureAwait(false);

        ThumbnailRecord? icon = response?.Data.FirstOrDefault();

        return string.Equals(icon?.State, "Completed", StringComparison.OrdinalIgnoreCase)
            ? icon?.ImageUrl
            : null;
    }

    private async Task<T?> FetchAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync<T>(body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }
}
