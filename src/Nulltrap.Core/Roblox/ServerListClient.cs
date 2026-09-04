using System.Text.Json;

namespace Nulltrap.Core.Roblox;

public sealed record ServerFacts(int Playing, int MaxPlayers, int Ping, int Fps);

public sealed class ServerListClient
{
    public const int PagesToWalk = 3;

    private const string Endpoint = "https://games.roblox.com/v1/games/{0}/servers/Public?limit=100";

    private readonly HttpClient _http;

    public ServerListClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static ServerFacts? Read(string payload, string? jobId, out string? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        try
        {
            using JsonDocument page = JsonDocument.Parse(payload);
            JsonElement root = page.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("nextPageCursor", out JsonElement next) && next.ValueKind == JsonValueKind.String)
            {
                cursor = next.GetString();
            }

            if (!root.TryGetProperty("data", out JsonElement servers) || servers.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement server in servers.EnumerateArray())
            {
                if (!server.TryGetProperty("id", out JsonElement id)
                    || id.ValueKind != JsonValueKind.String
                    || !string.Equals(id.GetString(), jobId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new ServerFacts(
                    Whole(server, "playing"),
                    Whole(server, "maxPlayers"),
                    Whole(server, "ping"),
                    Whole(server, "fps"));
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<ServerFacts?> FindAsync(long placeId, string? jobId, CancellationToken cancellationToken = default)
    {
        if (placeId <= 0 || string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        string address = string.Format(System.Globalization.CultureInfo.InvariantCulture, Endpoint, placeId);
        string? cursor = null;

        for (int page = 0; page < PagesToWalk; page++)
        {
            string wanted = cursor is null ? address : $"{address}&cursor={Uri.EscapeDataString(cursor)}";

            try
            {
                using HttpResponseMessage answer = await _http.GetAsync(wanted, cancellationToken).ConfigureAwait(false);

                if (!answer.IsSuccessStatusCode)
                {
                    return null;
                }

                string payload = await answer.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                ServerFacts? found = Read(payload, jobId, out cursor);

                if (found is not null)
                {
                    return found;
                }

                if (string.IsNullOrWhiteSpace(cursor))
                {
                    return null;
                }
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static int Whole(JsonElement server, string field) =>
        server.TryGetProperty(field, out JsonElement found) && found.ValueKind == JsonValueKind.Number
            ? (int)Math.Round(found.GetDouble())
            : 0;
}
