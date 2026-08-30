namespace Nulltrap.Core.Roblox;

public static class Recommendations
{
    public const int MinimumPlayers = 200;

    public static IReadOnlyList<DiscoveredGame> Pick(
        IReadOnlyList<DiscoveredGame> pool,
        IReadOnlyCollection<long> alreadyPlayed,
        IReadOnlyCollection<string> favouriteGenres,
        int take)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(alreadyPlayed);
        ArgumentNullException.ThrowIfNull(favouriteGenres);

        if (take <= 0)
        {
            return [];
        }

        var played = new HashSet<long>(alreadyPlayed);
        var genres = new HashSet<string>(favouriteGenres, StringComparer.OrdinalIgnoreCase);

        return pool
            .Where(game => !played.Contains(game.UniverseId) && game.Playing >= MinimumPlayers)
            .OrderByDescending(game => game.Genre is not null && genres.Contains(game.Genre))
            .ThenByDescending(Approval)
            .ThenByDescending(game => game.Playing)
            .Take(take)
            .ToList();
    }

    private static double Approval(DiscoveredGame game)
    {
        int votes = game.Likes + game.Dislikes;

        return votes == 0 ? 0 : (double)game.Likes / votes;
    }
}
