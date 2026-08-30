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

    public const int BatchSize = 50;

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

        IReadOnlyDictionary<long, GameInfo> found =
            await DescribeManyAsync([universeId], cancellationToken).ConfigureAwait(false);

        return found.GetValueOrDefault(universeId);
    }

    public async Task<IReadOnlyDictionary<long, GameInfo>> DescribeManyAsync(
        IReadOnlyCollection<long> universeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(universeIds);

        var answer = new Dictionary<long, GameInfo>();
        var wanted = new List<long>();

        foreach (long universeId in universeIds.Where(id => id > 0).Distinct())
        {
            if (_cache.TryGetValue(universeId, out GameInfo? cached))
            {
                answer[universeId] = cached;
            }
            else
            {
                wanted.Add(universeId);
            }
        }

        foreach (long[] batch in wanted.Chunk(BatchSize))
        {
            string list = string.Join(',', batch);

            Task<GamesResponse?> games = FetchAsync<GamesResponse>(GamesEndpoint + list, cancellationToken);
            Task<VotesResponse?> votes = FetchAsync<VotesResponse>(VotesEndpoint + list, cancellationToken);
            Task<ThumbnailsResponse?> icons = FetchAsync<ThumbnailsResponse>(IconsEndpoint + list, cancellationToken);

            await Task.WhenAll(games, votes, icons).ConfigureAwait(false);

            Dictionary<long, VoteRecord> counted = (await votes.ConfigureAwait(false))?.Data
                .GroupBy(record => record.Id)
                .ToDictionary(group => group.Key, group => group.First()) ?? [];

            Dictionary<long, string> art = (await icons.ConfigureAwait(false))?.Data
                .Where(record => string.Equals(record.State, "Completed", StringComparison.OrdinalIgnoreCase)
                    && record.ImageUrl is not null)
                .GroupBy(record => record.TargetId)
                .ToDictionary(group => group.Key, group => group.First().ImageUrl!) ?? [];

            foreach (GameRecord game in (await games.ConfigureAwait(false))?.Data ?? [])
            {
                if (game.Id <= 0 || game.RootPlaceId == 0 || string.IsNullOrWhiteSpace(game.Name))
                {
                    continue;
                }

                var info = new GameInfo
                {
                    UniverseId = game.Id,
                    Name = game.Name,
                    CreatorName = game.Creator?.Name,
                    RootPlaceId = game.RootPlaceId,
                    Playing = game.Playing,
                    Likes = counted.GetValueOrDefault(game.Id)?.UpVotes ?? 0,
                    Dislikes = counted.GetValueOrDefault(game.Id)?.DownVotes ?? 0,
                    Genre = string.IsNullOrWhiteSpace(game.Genre) ? null : game.Genre,
                    IconUrl = art.GetValueOrDefault(game.Id),
                };

                _cache[game.Id] = info;
                answer[game.Id] = info;
            }
        }

        return answer;
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
